using System;
using System.Net;
using System.Net.Sockets;
using Arena.Logging;

namespace Arena.Network
{
    public class ClientSession : IDisposable
    {
        public int ClientId { get; }
        public TcpClient TcpClient { get; }
        public IPEndPoint UdpEndPoint { get; set; }
        public ReliableUdpManager ReliableManager { get; }
        
        private readonly IGameLogger _logger;
        
        public bool IsConnected => TcpClient?.Connected ?? false;
        public DateTime ConnectedAt { get; }
        public DateTime LastActivityAt { get; private set; }
        
        public ClientSession(int clientId, TcpClient tcpClient, IGameLogger logger)
        {
            ClientId = clientId;
            TcpClient = tcpClient;
            _logger = logger;
            ReliableManager = new ReliableUdpManager();
            ConnectedAt = DateTime.UtcNow;
            LastActivityAt = DateTime.UtcNow;
            
            _logger.Log(LogLevel.Debug, "Session", "Session {0} created", clientId);
        }
        
        public void UpdateActivity()
        {
            LastActivityAt = DateTime.UtcNow;
        }
        
        public void Dispose()
        {
            try
            {
                ReliableManager?.Clear();
                TcpClient?.Close();
                _logger?.Log(LogLevel.Debug, "Session", "Session {0} disposed", ClientId);
            }
            catch (Exception ex)
            {
                _logger?.LogException(ex, $"ClientSession-{ClientId}");
            }
        }
    }
}