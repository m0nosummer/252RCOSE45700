using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using UnityEngine;
using Arena.Core;
using Arena.Core.Utilities;
using Arena.Logging;
using Arena.Network.Profiling;

namespace Arena.Network
{
    public class NetworkService : MonoBehaviour, INetworkService
    {
        private IGameLogger _logger;
        private NetworkConfig _config;
        private NetworkProfiler _profiler;
        
        private IConnectionManager _connectionManager;
        private IPacketProcessor _packetProcessor;
        private IMessageRouter _messageRouter;
        
        private readonly ConcurrentQueue<ReceivedMessage> _incomingMessages = new();
        private CancellationTokenSource _cts;

        public bool IsConnected { get; private set; }
        public bool IsServer { get; private set; }
        public int LocalClientId { get; private set; } = -1;
        
        public IMessageRouter MessageRouter => _messageRouter;

        public event Action<int> OnClientConnected;
        public event Action<int> OnClientDisconnected;
        public event Action<string> OnConnectionFailed;

        public void Initialize(NetworkConfig config, IGameLogger logger)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            var container = Arena.Core.DependencyInjection.GameInstaller.Container;
            if (container == null)
            {
                throw new InvalidOperationException("DIContainer not initialized!");
            }
            
            // Factory
            _connectionManager = container.Resolve<IConnectionManager>();
            _packetProcessor = container.Resolve<IPacketProcessor>();
            _messageRouter = container.Resolve<IMessageRouter>();
            
            if (container.IsRegistered<NetworkProfiler>())
            {
                _profiler = container.Resolve<NetworkProfiler>();
                _logger.Log(LogLevel.Debug, "Network", "NetworkProfiler integrated");
            }
            
            _logger.Log(LogLevel.Info, "Network", "NetworkService initialized via DI Container");
        }

        private void Update()
        {
            ProcessIncomingMessages();
        }

        public async Task StartServerAsync(int port)
        {
            if (IsConnected)
            {
                _logger.Log(LogLevel.Warning, "Network", "Already connected!");
                return;
            }

            try
            {
                _cts = new CancellationTokenSource();
                
                await _connectionManager.StartServerAsync(port, _cts.Token);
                
                IsConnected = true;
                IsServer = true;
                LocalClientId = 0;
                
                SubscribeToConnectionEvents();
                
                _logger.Log(LogLevel.Info, "Network", "Server started on port {0} (LocalClientId=0)", port);
            }
            catch (Exception ex)
            {
                _logger.LogException(ex, "StartServer");
                OnConnectionFailed?.Invoke($"Server start failed: {ex.Message}");
            }
        }

        public async Task ConnectToServerAsync(string ip, int port)
        {
            if (IsConnected)
            {
                _logger.Log(LogLevel.Warning, "Network", "Already connected!");
                return;
            }

            try
            {
                _cts = new CancellationTokenSource();
                
                await _connectionManager.ConnectToServerAsync(ip, port, _cts.Token);
                
                IsConnected = true;
                IsServer = false;
                
                SubscribeToConnectionEvents();
                
                _logger.Log(LogLevel.Info, "Network", "Connected to {0}:{1} (waiting for ID assignment)", ip, port);
            }
            catch (Exception ex)
            {
                _logger.LogException(ex, "ConnectToServer");
                OnConnectionFailed?.Invoke($"Connection failed: {ex.Message}");
            }
        }

        public void SendMessage(INetworkMessage message)
        {
            if (!IsConnected)
            {
                _logger.Log(LogLevel.Warning, "Network", "Cannot send message: not connected");
                return;
            }

            try
            {
                _profiler?.RecordMessage(message.Type);
                
                var packets = _packetProcessor.CreatePackets(message);
                
                foreach (var packet in packets)
                {
                    packet.Header.ClientId = LocalClientId;
                }
                
                _connectionManager.SendPackets(packets);
            }
            catch (Exception ex)
            {
                _logger.LogException(ex, "SendMessage");
            }
        }

        public void Disconnect()
        {
            if (!IsConnected)
                return;

            _logger.Log(LogLevel.Info, "Network", "Disconnecting...");
            
            _cts?.Cancel();
            _connectionManager?.Shutdown();
            
            IsConnected = false;
            IsServer = false;
            LocalClientId = -1;
            
            _logger.Log(LogLevel.Info, "Network", "Disconnected");
        }

        public NetworkStatistics GetStatistics()
        {
            return _connectionManager?.GetStatistics() ?? new NetworkStatistics();
        }

        private void SubscribeToConnectionEvents()
        {
            _connectionManager.OnPacketReceived += HandlePacketReceived;
            _connectionManager.OnClientConnected += (id) => OnClientConnected?.Invoke(id);
            _connectionManager.OnClientDisconnected += (id) => OnClientDisconnected?.Invoke(id);
        }

        private void HandlePacketReceived(NetworkPacket packet, int actualSenderId)
        {
            try
            {
                var message = _packetProcessor.ProcessPacket(packet);
                if (message != null)
                {
                    _profiler?.RecordMessage(message.Type);
            
                    int senderId = actualSenderId;
            
                    if (!IsServer && message.Type == MessageType.Handshake && LocalClientId < 0)
                    {
                        var handshake = message as Arena.Network.Messages.HandshakeMessage;
                        if (handshake != null && senderId == 0 && handshake.TargetId > 0)
                        {
                            LocalClientId = handshake.TargetId;
                            _logger.Log(LogLevel.Info, "Network", 
                                "✅ Assigned LocalClientId: {0}", LocalClientId);
                        }
                    }
            
                    _incomingMessages.Enqueue(new ReceivedMessage 
                    { 
                        Message = message, 
                        SenderId = senderId 
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogException(ex, "HandlePacket");
            }
        }

        private void ProcessIncomingMessages()
        {
            int processed = 0;
            int maxPerFrame = _config != null ? _config.MaxMessagesPerFrame : 10;
            
            while (_incomingMessages.TryDequeue(out var received) && processed < maxPerFrame)
            {
                _messageRouter?.RouteMessage(received.Message, received.SenderId);
                processed++;
            }
            
            if (_incomingMessages.Count > 100)
            {
                _logger.Log(LogLevel.Warning, "Network", "Message queue backlog: {0} messages", _incomingMessages.Count);
            }
        }

        private void OnDestroy()
        {
            Disconnect();
        }

        private struct ReceivedMessage
        {
            public INetworkMessage Message;
            public int SenderId;
        }
    }
}