using UnityEngine;

namespace Arena.Network
{
    [CreateAssetMenu(fileName = "NetworkConfig", menuName = "Arena/Config/Network Config")]
    public class NetworkConfig : ScriptableObject
    {
        [Header("Connection Settings")]
        public string DefaultServerIP = "127.0.0.1";
        public int DefaultPort = 7777;
        public float ConnectionTimeoutSeconds = 5f;
        
        [Header("Performance Settings")]
        public int BufferSize = 4096;
        public int MaxMessagesPerFrame = 10;
        public int ServerTickRateHz = 20;
        public int ClientTickRateHz = 20;
        
        [Header("Reliability Settings")]
        public int MaxRetries = 5;
        public float RetryTimeoutSeconds = 0.2f;
        public bool EnableReliableUdp = true;
        
        [Header("Compression Settings")]
        public bool EnableCompression = true;
        public int CompressionThresholdBytes = 100;
        
        [Header("Validation Settings")]
        public bool EnableMessageValidation = true;
        public float MaxTimestampDriftSeconds = 1f;
        public float MaxVelocityMultiplier = 2f;
        
        [Header("Gameplay Settings")]
        public float PlayerMoveSpeed = 7f;
        public float PlayerMaxHealth = 100f;
        
        [Header("Vision System")]
        public float DayVisionRange = 15f;
        public float NightVisionRange = 8f;
        public float DayVisionAngle = 75f;
        public float NightVisionAngle = 45f;
        
        [Header("Debug & Profiling")]
        public bool EnableNetworkLogging = true;
        public bool EnablePerformanceMonitoring = true;
        public bool EnableNetworkProfiler = true;
        public float ProfilerUpdateInterval = 1f;

        private void OnValidate()
        {
            BufferSize = Mathf.Clamp(BufferSize, 1024, 65536);
            MaxMessagesPerFrame = Mathf.Clamp(MaxMessagesPerFrame, 1, 100);
            ServerTickRateHz = Mathf.Clamp(ServerTickRateHz, 10, 120);
            ClientTickRateHz = Mathf.Clamp(ClientTickRateHz, 10, 120);
            MaxRetries = Mathf.Clamp(MaxRetries, 1, 10);
            
            if (ConnectionTimeoutSeconds <= 0) ConnectionTimeoutSeconds = 5f;
            if (RetryTimeoutSeconds <= 0) RetryTimeoutSeconds = 0.2f;
        }
    }
}