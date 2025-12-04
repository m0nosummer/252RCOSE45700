using UnityEngine;

namespace Arena.Core
{
    public static class GameConstants
    {
        public static class Network
        {
            public const int DefaultServerPort = 7777;
            public const int MaxPacketSize = 1400;
            public const int MaxPayloadSize = MaxPacketSize - 20;
            public const int BufferSize = 4096;
            public const int MaxRetries = 5;
            public const float RetryTimeoutSeconds = 0.2f;
            public const float ConnectionTimeoutSeconds = 5f;
            public const int ServerTickRateHz = 20;
            public const int ServerTickRate = 1000 / ServerTickRateHz;
            public const int ClientTickRateHz = 20;
        }

        public static class Gameplay
        {
            public static readonly Vector3 Player1SpawnPosition = new(-5f, 0f, 5f);
            public static readonly Vector3 Player2SpawnPosition = new(5f, 0f, 5f);
            
            public const float DefaultMoveSpeed = 7f;
            public const float DefaultHealth = 100f;
            public const float DashDistance = 4f;
            public const float DashDuration = 0.2f;
            public const float DashCooldown = 8f;
        }

        public static class Vision
        {
            public const float DayVisionRange = 15f;
            public const float NightVisionRange = 8f;
            public const float DayVisionAngle = 75f;
            public const float NightVisionAngle = 45f;
        }

        public static class Compression
        {
            public const float MinPositionRange = -100f;
            public const float MaxPositionRange = 100f;
            public const float PositionThreshold = 0.01f;
            public const float RotationThresholdDegrees = 0.5f;
            public const float VelocityThreshold = 0.1f;
        }
    }
}