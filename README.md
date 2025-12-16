### Multiplayer Network Project
```
_Scripts
├── Editor
│   ├── AssetTreeGenerator.cs
│   └── SceneAnalyzerSafe.cs
├── _Core
│   ├── _Constants
│   │   └── GameConstants.cs
│   ├── _DependencyInjection
│   │   ├── DIContainer.cs
│   │   └── GameInstaller.cs
│   ├── _Logging
│   │   ├── IGameLogger.cs
│   │   └── UnityLogger.cs
│   └── _Utilities
│       └── UnityMainThreadDispatcher.cs
├── _Gameplay
│   ├── CameraFollow.cs
│   ├── ClientBulletManager.cs
│   └── ServerSimulation.cs
├── _Input
│   ├── PlayerInputActions.cs
│   ├── PlayerInputActions.inputactions
│   └── PlayerInputHandler.cs
├── _Network
│   ├── _Core
│   │   ├── ByteArrayPool.cs
│   │   ├── ClientSession.cs
│   │   ├── ConnectionManager.cs
│   │   ├── MessageFactory.cs
│   │   ├── MessageRouter.cs
│   │   ├── MessageType.cs
│   │   ├── NetworkService.cs
│   │   ├── NetworkTestManager.cs
│   │   └── ReliableUdpManager.cs
│   ├── _Exceptions
│   │   └── NetworkException.cs
│   ├── _Interfaces
│   │   ├── IConnectionManager.cs
│   │   ├── IMessageRouter.cs
│   │   ├── INetworkMessage.cs
│   │   ├── INetworkService.cs
│   │   └── IPacketProcessor.cs
│   ├── _Messages
│   │   ├── BulletDestroyMessage.cs
│   │   ├── BulletSpawnMessage.cs
│   │   ├── FireMessage.cs
│   │   ├── GameTimeMessage.cs
│   │   ├── HandshakeMessage.cs
│   │   ├── NetworkMessage.cs
│   │   ├── PlayerInputMessage.cs
│   │   ├── PlayerJoinedMessage.cs
│   │   ├── PlayerLeftMessage.cs
│   │   └── PlayerStateMessage.cs
│   └── _Packets
│       ├── NetworkPacket.cs
│       ├── PacketHeader.cs
│       ├── PacketManager.cs
│       └── PacketProcessor.cs
├── _Player
│   ├── _States
│   │   ├── DeadPlayerState.cs
│   │   ├── IPlayerState.cs
│   │   ├── LocalPlayerState.cs
│   │   └── RemotePlayerState.cs
│   ├── Bullet.cs
│   └── Player.cs
├── _Settings
│   └── NetworkConfig.cs
└── _Vision
    ├── GameTimeManager.cs
    ├── GameTimerUI.cs
    └── VisionReceiver.cs
```
