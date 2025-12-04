using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Arena.Core;
using Arena.Core.DependencyInjection;
using Arena.Logging;
using Arena.Network.Profiling;

namespace Arena.Network
{
    public class ConnectionManager : IConnectionManager
    {
        private readonly IGameLogger _logger;
        private readonly NetworkStatistics _stats = new();
        private readonly ByteArrayPool _bufferPool = new(GameConstants.Network.BufferSize, 50);
        
        private NetworkProfiler _profiler;
        
        // Server components
        private TcpListener _tcpListener;
        private UdpClient _udpServer;
        private readonly ConcurrentDictionary<int, ClientSession> _sessions = new();
        
        // Client components
        private TcpClient _tcpClient;
        private UdpClient _udpClient;
        private IPEndPoint _serverEndPoint;
        private ReliableUdpManager _clientReliableManager;
        
        private int _nextClientId = 1;
        private readonly object _clientIdLock = new();

        public event Action<NetworkPacket, int> OnPacketReceived;
        public event Action<int> OnClientConnected;
        public event Action<int> OnClientDisconnected;

        public ConnectionManager(IGameLogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            var container = GameInstaller.Container;
            if (container != null && container.IsRegistered<NetworkProfiler>())
            {
                _profiler = container.Resolve<NetworkProfiler>();
            }
        }

        public Task StartServerAsync(int port, CancellationToken ct)
        {
            try
            {
                _tcpListener = new TcpListener(IPAddress.Any, port);
                _tcpListener.Start();
                
                _udpServer = new UdpClient(port);
                ConfigureUdpSocket(_udpServer);
                
                // Individual threads
                _ = Task.Run(() => TcpAcceptLoopAsync(ct), ct);
                _ = Task.Run(() => UdpReceiveLoopAsync(ct), ct);
                _ = Task.Run(() => ReliableUpdateLoopAsync(ct), ct);
                
                _logger.Log(LogLevel.Info, "Connection", "Server started on port {0}", port);
                
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogException(ex, "StartServer");
                throw new NetworkException("Failed to start server", NetworkErrorCode.ServerStartFailed, ex);
            }
        }

        public async Task ConnectToServerAsync(string ip, int port, CancellationToken ct)
        {
            try
            {
                // TCP
                _tcpClient = new TcpClient();
                _tcpClient.NoDelay = true;
                
                var connectTask = _tcpClient.ConnectAsync(ip, port);
                var timeoutTask = Task.Delay(TimeSpan.FromSeconds(GameConstants.Network.ConnectionTimeoutSeconds), ct);
                
                var completedTask = await Task.WhenAny(connectTask, timeoutTask);
                
                if (completedTask == timeoutTask)
                {
                    throw new NetworkException("Connection timeout", NetworkErrorCode.ConnectionTimeout);
                }
                
                // UDP
                _serverEndPoint = new IPEndPoint(IPAddress.Parse(ip), port);
                
                _udpClient = new UdpClient(0);
                _udpClient.Connect(_serverEndPoint);
                ConfigureUdpSocket(_udpClient);
                
                _clientReliableManager = new ReliableUdpManager();
                
                // Receive loops
                _ = Task.Run(() => TcpReceiveLoopAsync(ct), ct);
                _ = Task.Run(() => UdpClientReceiveLoopAsync(ct), ct);
                _ = Task.Run(() => ReliableUpdateLoopAsync(ct), ct);
                
                await Task.Delay(50);
                SendUdpHandshake();
                
                _logger.Log(LogLevel.Info, "Connection", "Connected to {0}:{1}", ip, port);
            }
            catch (OperationCanceledException)
            {
                throw new NetworkException("Connection timeout", NetworkErrorCode.ConnectionTimeout);
            }
            catch (Exception ex)
            {
                _logger.LogException(ex, "ConnectToServer");
                throw new NetworkException("Connection failed", NetworkErrorCode.ConnectionFailed, ex);
            }
        }

        public void SendPackets(NetworkPacket[] packets)
        {
            if (packets == null || packets.Length == 0) return;

            foreach (var packet in packets)
            {
                try
                {
                    var data = packet.Serialize();
                    
                    if (RequiresTcp(packet.Header.Type))
                    {
                        SendViaTcp(data, packet.TargetId);
                        _stats.TcpPacketsSent++;
                    }
                    else
                    {
                        bool reliable = packet.Header.Flags.HasFlag(PacketFlags.Reliable);
                        SendViaUdp(data, packet.TargetId, reliable, packet.Header.SequenceNumber);
                        _stats.UdpPacketsSent++;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogException(ex, "SendPacket");
                }
            }
        }

        public NetworkStatistics GetStatistics() => _stats;

        private async Task ReliableUpdateLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(50, ct);
                    
                    foreach (var session in _sessions.Values)
                    {
                        session.ReliableManager.Update();
                    }
                    
                    _clientReliableManager?.Update();
                    
                    ProcessReliableRetries();
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogException(ex, "ReliableUpdateLoop");
                }
            }
        }

        private void ProcessReliableRetries()
        {
            if (_udpServer != null)
            {
                foreach (var session in _sessions.Values)
                {
                    var packetsToRetry = session.ReliableManager.GetPacketsToRetry();
                    
                    foreach (var packet in packetsToRetry)
                    {
                        try
                        {
                            if (session.UdpEndPoint != null)
                            {
                                _udpServer.Send(packet, packet.Length, session.UdpEndPoint);
                                _stats.RetransmissionCount++;
                                
                                _logger.Log(LogLevel.Trace, "Connection", 
                                    "Retransmitted to Client {0}", session.ClientId);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogException(ex, $"Retry-Client{session.ClientId}");
                        }
                    }
                }
            }
            
            if (_udpClient != null && _clientReliableManager != null)
            {
                var packetsToRetry = _clientReliableManager.GetPacketsToRetry();
                
                foreach (var packet in packetsToRetry)
                {
                    try
                    {
                        _udpClient.Send(packet, packet.Length);
                        _stats.RetransmissionCount++;
                        
                        _logger.Log(LogLevel.Trace, "Connection", "Client retransmitted packet");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogException(ex, "ClientRetry");
                    }
                }
            }
        }


        private async Task TcpAcceptLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    if (_tcpListener.Pending())
                    {
                        var tcpClient = await _tcpListener.AcceptTcpClientAsync();
                        tcpClient.NoDelay = true;
                        
                        int clientId = GetNextClientId();
                        
                        var session = new ClientSession(clientId, tcpClient, _logger);
                        _sessions.TryAdd(clientId, session);
                        
                        _ = Task.Run(() => HandleTcpClientAsync(session, ct), ct);
                        
                        OnClientConnected?.Invoke(clientId);
                        _logger.Log(LogLevel.Info, "Connection", "Client {0} connected", clientId);
                    }
                    
                    await Task.Delay(GameConstants.Network.ServerTickRate, ct);
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    if (!ct.IsCancellationRequested)
                        _logger.LogException(ex, "TcpAcceptLoop");
                }
            }
        }

        private async Task HandleTcpClientAsync(ClientSession session, CancellationToken ct)
        {
            try
            {
                var clientEndPoint = (IPEndPoint)session.TcpClient.Client.RemoteEndPoint;
                session.UdpEndPoint = clientEndPoint;
                
                _logger.Log(LogLevel.Debug, "Connection",
                    "UDP endpoint registered for client {0}: {1}",
                    session.ClientId, clientEndPoint);
                
                var stream = session.TcpClient.GetStream();
                
                await ProcessTcpStreamAsync(stream, session.ClientId, ct);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _logger.LogException(ex, $"HandleClient-{session.ClientId}");
            }
            finally
            {
                DisconnectClient(session.ClientId);
            }
        }

        private void SendViaTcp(byte[] data, int targetId)
        {
            try
            {
                byte[] lengthHeader = BitConverter.GetBytes(data.Length);
                byte[] frameData = new byte[4 + data.Length];
                Buffer.BlockCopy(lengthHeader, 0, frameData, 0, 4);
                Buffer.BlockCopy(data, 0, frameData, 4, data.Length);

                _profiler?.RecordPacketSent(frameData.Length);
                
                _logger.Log(LogLevel.Debug, "Connection", 
                    "📡 TCP Send: {0} bytes to Target={1}", frameData.Length, targetId);
                
                if (_tcpListener != null) // Server
                {
                    if (targetId == -1) // Broadcast
                    {
                        foreach (var session in _sessions.Values)
                        {
                            if (session.IsConnected)
                            {
                                var stream = session.TcpClient.GetStream();
                                stream.Write(frameData, 0, frameData.Length);
                            }
                        }
                    }
                    else // Unicast
                    {
                        if (_sessions.TryGetValue(targetId, out var session))
                        {
                            if (session.IsConnected)
                            {
                                var stream = session.TcpClient.GetStream();
                                stream.Write(frameData, 0, frameData.Length);
                            }
                        }
                    }
                }
                else if (_tcpClient?.Connected == true) // Client
                {
                    var stream = _tcpClient.GetStream();
                    stream.Write(frameData, 0, frameData.Length);
                }
            }
            catch (Exception ex)
            {
                _logger.LogException(ex, "SendViaTcp");
            }
        }

        private void SendViaUdp(byte[] data, int targetId, bool reliable, uint sequenceNumber)
        {
            try
            {
                _profiler?.RecordPacketSent(data.Length);
        
                if (_udpServer != null)  // Server
                {
                    if (targetId == -1)  // Broadcast
                    {
                        foreach (var session in _sessions.Values)
                        {
                            if (reliable)
                            {
                                session.ReliableManager.TrackPacket(sequenceNumber, data);
                            }
                            
                            if (session.UdpEndPoint != null)
                            {
                                _udpServer.Send(data, data.Length, session.UdpEndPoint);
                            }
                        }
                    }
                    else  // Unicast
                    {
                        if (_sessions.TryGetValue(targetId, out var session))
                        {
                            if (reliable)
                            {
                                session.ReliableManager.TrackPacket(sequenceNumber, data);
                            }
                            
                            if (session.UdpEndPoint != null)
                            {
                                _udpServer.Send(data, data.Length, session.UdpEndPoint);
                            }
                        }
                        else
                        {
                            _logger.Log(LogLevel.Warning, "Connection", 
                                "UDP endpoint not found for client {0}", targetId);
                        }
                    }
                }
                else if (_udpClient != null)  // Client
                {
                    if (reliable)
                    {
                        _clientReliableManager?.TrackPacket(sequenceNumber, data);
                    }
                    
                    _udpClient.Send(data, data.Length);
                }
            }
            catch (Exception ex)
            {
                _logger.LogException(ex, "SendViaUdp");
            }
        }
        
        private async Task TcpReceiveLoopAsync(CancellationToken ct)
        {
            try
            {
                var stream = _tcpClient.GetStream();
                await ProcessTcpStreamAsync(stream, 0, ct);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _logger.LogException(ex, "TcpReceiveLoop");
            }
        }

        private async Task UdpReceiveLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var result = await _udpServer.ReceiveAsync();
                    
                    // ACK = [magic number(4 bytes)][Seq number (4 bytes)]
                    bool isAck = false;
                    if (result.Buffer.Length == 8)
                    {
                        uint magic = BitConverter.ToUInt32(result.Buffer, 0);
                        if (magic == 0x41434B00)
                        {
                            uint seq = BitConverter.ToUInt32(result.Buffer, 4);
                            
                            int ackSenderId = -1;
                            foreach (var kvp in _sessions)
                            {
                                if (kvp.Value.UdpEndPoint?.Equals(result.RemoteEndPoint) == true)
                                {
                                    ackSenderId = kvp.Key;
                                    break;
                                }
                            }

                            if (ackSenderId != -1 && _sessions.TryGetValue(ackSenderId, out var ackSession))
                            {
                                if (ackSession.ReliableManager.ProcessAck(result.Buffer))
                                {
                                    _logger.Log(LogLevel.Trace, "Connection", 
                                        "ACK seq={0} processed for Client {1}", seq, ackSenderId);
                                }
                            }
                            else
                            {
                                _logger.Log(LogLevel.Warning, "Connection", 
                                    "ACK from unknown endpoint: {0}", result.RemoteEndPoint);
                            }
                            isAck = true;
                        }
                    }
                    
                    if (isAck) continue;
                    
                    // UDP Handshake
                    if (result.Buffer.Length == 2 && result.Buffer[0] == 0x48 && result.Buffer[1] == 0x49)
                    {
                        _logger.Log(LogLevel.Debug, "Connection", "Received UDP handshake from {0}", result.RemoteEndPoint);
                        continue;
                    }
                    
                    // General data
                    NetworkPacket packet;
                    try
                    {
                        packet = NetworkPacket.Deserialize(result.Buffer, result.Buffer.Length);
                    }
                    catch (Exception ex)
                    {
                        _logger.Log(LogLevel.Warning, "Connection", "Invalid UDP packet from {0}: {1}", 
                            result.RemoteEndPoint, ex.Message);
                        continue;
                    }
                    
                    int clientId = packet.Header.ClientId;
                    
                    if (clientId < 0)
                    {
                        _logger.Log(LogLevel.Warning, "Connection", "Packet without Client ID from {0}", result.RemoteEndPoint);
                        continue;
                    }
                    
                    if (_sessions.TryGetValue(clientId, out var session))
                    {
                        if (!result.RemoteEndPoint.Equals(session.UdpEndPoint))
                        {
                            session.UdpEndPoint = result.RemoteEndPoint;
                            _logger.Log(LogLevel.Info, "Connection", 
                                "Updated UDP endpoint for Client {0}: {1}", clientId, result.RemoteEndPoint);
                        }
                        
                        session.UpdateActivity();
                    }
                    
                    // Reliable ACK
                    if (packet.Header.Flags.HasFlag(PacketFlags.Reliable))
                    {
                        if (_sessions.ContainsKey(clientId))
                        {
                            SendAck(packet.Header.SequenceNumber, result.RemoteEndPoint);
                        }
                    }
                    
                    OnPacketReceived?.Invoke(packet, clientId);
                    _stats.UdpPacketsReceived++;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (SocketException ex) when (ex.SocketErrorCode == SocketError.ConnectionReset)
                {
                    _logger.Log(LogLevel.Trace, "Connection", "UDP ICMP error (harmless): {0}", ex.Message);
                    await Task.Delay(10, ct);
                }
                catch (Exception ex)
                {
                    if (!ct.IsCancellationRequested)
                    {
                        _logger.LogException(ex, "UdpReceiveLoop");
                        await Task.Delay(100, ct);
                    }
                }
            }
        }

        private async Task UdpClientReceiveLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var result = await _udpClient.ReceiveAsync();
            
                    if (_clientReliableManager != null && _clientReliableManager.ProcessAck(result.Buffer))
                    {
                        _logger.Log(LogLevel.Trace, "Connection", "Client ACK processed");
                        continue;
                    }
            
                    try
                    {
                        var packet = NetworkPacket.Deserialize(result.Buffer, result.Buffer.Length);
                        
                        if (packet.Header.Flags.HasFlag(PacketFlags.Reliable))
                        {
                            SendAckToServer(packet.Header.SequenceNumber);
                        }
                
                        _profiler?.RecordPacketReceived(result.Buffer.Length);
                        OnPacketReceived?.Invoke(packet, 0);
                        _stats.UdpPacketsReceived++;
                    }
                    catch (Exception ex)
                    {
                        _logger.Log(LogLevel.Warning, "Connection", "Invalid UDP packet: {0}", ex.Message);
                    }
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (SocketException ex) when (ex.SocketErrorCode == SocketError.ConnectionReset)
                {
                    _logger.Log(LogLevel.Trace, "Connection", "UDP ICMP error (harmless): {0}", ex.Message);
                    await Task.Delay(10, ct);
                }
                catch (Exception ex)
                {
                    if (!ct.IsCancellationRequested)
                    {
                        _logger.LogException(ex, "UdpClientReceiveLoop");
                        await Task.Delay(100, ct);
                    }
                }
            }
        }

        private async Task ProcessTcpStreamAsync(NetworkStream stream, int senderId, CancellationToken ct)
        {
            byte[] lengthBuffer = new byte[4];
            byte[] dataBuffer = _bufferPool.Rent();
    
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    if (!await ReadExactAsync(stream, lengthBuffer, 0, 4, ct))
                        break;
            
                    int packetLength = BitConverter.ToInt32(lengthBuffer, 0);
            
                    if (packetLength <= 0 || packetLength > GameConstants.Network.MaxPacketSize)
                    {
                        _logger.Log(LogLevel.Error, "Connection", 
                            "Invalid packet length: {0} from client {1}", packetLength, senderId);
                        break;
                    }
            
                    if (dataBuffer.Length < packetLength)
                    {
                        _bufferPool.Return(dataBuffer);
                        dataBuffer = new byte[packetLength];
                    }
            
                    if (!await ReadExactAsync(stream, dataBuffer, 0, packetLength, ct))
                        break;
            
                    _profiler?.RecordPacketReceived(packetLength);
            
                    var packet = NetworkPacket.Deserialize(dataBuffer, packetLength);
            
                    OnPacketReceived?.Invoke(packet, senderId);
                    _stats.TcpPacketsReceived++;
                }
            }
            finally
            {
                _bufferPool.Return(dataBuffer);
            }
        }

        private async Task<bool> ReadExactAsync(NetworkStream stream, byte[] buffer, int offset, int count, CancellationToken ct)
        {
            int totalRead = 0;
            while (totalRead < count)
            {
                int bytesRead = await stream.ReadAsync(buffer, offset + totalRead, count - totalRead, ct);
                if (bytesRead == 0)
                    return false;
                
                totalRead += bytesRead;
            }
            return true;
        }

        private void SendAck(uint sequenceNumber, IPEndPoint endpoint)
        {
            try
            {
                var ackPacket = new byte[8];
                BitConverter.GetBytes(0x41434B00).CopyTo(ackPacket, 0);
                BitConverter.GetBytes(sequenceNumber).CopyTo(ackPacket, 4);
                
                _udpServer?.Send(ackPacket, ackPacket.Length, endpoint);
                
                _logger.Log(LogLevel.Trace, "Connection", "Sent ACK for seq {0}", sequenceNumber);
            }
            catch (Exception ex)
            {
                _logger.LogException(ex, "SendAck");
            }
        }

        private void SendAckToServer(uint sequenceNumber)
        {
            try
            {
                var ackPacket = new byte[8];
                BitConverter.GetBytes(0x41434B00).CopyTo(ackPacket, 0);
                BitConverter.GetBytes(sequenceNumber).CopyTo(ackPacket, 4);
                
                _udpClient?.Send(ackPacket, ackPacket.Length);
                
                _logger.Log(LogLevel.Trace, "Connection", "Sent ACK for seq {0}", sequenceNumber);
            }
            catch (Exception ex)
            {
                _logger.LogException(ex, "SendAckToServer");
            }
        }

        private void DisconnectClient(int clientId)
        {
            if (_sessions.TryRemove(clientId, out var session))
            {
                session.Dispose();
                
                OnClientDisconnected?.Invoke(clientId);
                _logger.Log(LogLevel.Info, "Connection", "Client {0} disconnected", clientId);
            }
        }

        private int GetNextClientId()
        {
            lock (_clientIdLock)
            {
                return _nextClientId++;
            }
        }

        private void ConfigureUdpSocket(UdpClient client)
        {
            client.Client.ReceiveBufferSize = GameConstants.Network.BufferSize * 4;
            client.Client.SendBufferSize = GameConstants.Network.BufferSize * 4;
            
            try
            {
                const int SIO_UDP_CONNRESET = -1744830452;
                client.Client.IOControl(
                    (IOControlCode)SIO_UDP_CONNRESET,
                    new byte[] { 0, 0, 0, 0 },
                    null
                );
                _logger.Log(LogLevel.Debug, "Connection", "Windows UDP ICMP errors suppressed");
            }
            catch (Exception ex)
            {
                _logger.Log(LogLevel.Debug, "Connection", "UDP socket configuration: {0}", ex.Message);
            }
        }

        private bool RequiresTcp(PacketType type)
        {
            return type == PacketType.Handshake
                   || type == PacketType.Disconnect
                   || type == PacketType.PlayerLifecycle;
        }

        private void SendUdpHandshake()
        {
            try
            {
                byte[] handshake = new byte[] { 0x48, 0x49 };
                _udpClient?.Send(handshake, handshake.Length);
                _logger.Log(LogLevel.Debug, "Connection", "Sent UDP handshake to server");
            }
            catch (Exception ex)
            {
                _logger.LogException(ex, "SendUdpHandshake");
            }
        }

        public void Shutdown()
        {
            try
            {
                foreach (var session in _sessions.Values)
                {
                    session.Dispose();
                }
                _sessions.Clear();
                
                _tcpListener?.Stop();
                _tcpClient?.Close();
                
                _udpServer?.Close();
                _udpClient?.Close();
                
                _clientReliableManager?.Clear();
                _bufferPool.Clear();
                
                _logger.Log(LogLevel.Info, "Connection", "Connection manager shutdown complete");
            }
            catch (Exception ex)
            {
                _logger.LogException(ex, "Shutdown");
            }
        }
    }
}