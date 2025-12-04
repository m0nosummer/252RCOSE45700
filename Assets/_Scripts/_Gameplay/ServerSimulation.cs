using UnityEngine;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Arena.Core;
using Arena.Core.DependencyInjection;
using Arena.Network;
using Arena.Network.Messages;
using Arena.Logging;

namespace Arena.Gameplay
{
    public class ServerSimulation : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private NetworkConfig networkConfig;

        private INetworkService _networkService;
        private IGameLogger _logger;
        
        private readonly ConcurrentDictionary<int, PlayerState> _playerStates = new();
        private readonly Dictionary<uint, ServerBullet> _activeBullets = new();
        private float _stateUpdateTimer;
        
        private uint _nextBulletId = 1;
        private readonly object _bulletIdLock = new();

        private float _fixedTimestep;
        private float _accumulatedTime;

        private void Start()
        {
            if (networkConfig == null)
            {
                Debug.LogError("[ServerSimulation] NetworkConfig is not assigned!");
                return;
            }
            
            _fixedTimestep = 1f / networkConfig.ServerTickRateHz;
            _accumulatedTime = 0f;
            
            Debug.Log($"[SERVER] Fixed Timestep: {_fixedTimestep}s ({networkConfig.ServerTickRateHz}Hz)");
            
            InjectDependencies();
            SubscribeToEvents();
        }

        private void Update()
        {
            _accumulatedTime += Time.deltaTime;
            
            while (_accumulatedTime >= _fixedTimestep)
            {
                SimulatePhysics(_fixedTimestep);
                _accumulatedTime -= _fixedTimestep;
            }
            
            UpdateBullets();
            BroadcastPlayerStates();
        }

        private void SimulatePhysics(float deltaTime)
        {
            foreach (var state in _playerStates.Values)
            {
                if (!state.IsAlive) continue;
                
                state.Position += state.Velocity * deltaTime;
            }
        }

        private void InjectDependencies()
        {
            var container = GameInstaller.Container;
            
            if (container == null)
            {
                Debug.LogError("[ServerSimulation] DIContainer not found!");
                return;
            }

            _networkService = container.Resolve<INetworkService>();
            _logger = container.Resolve<IGameLogger>();
        }

        private void SubscribeToEvents()
        {
            if (_networkService != null)
            {
                _networkService.MessageRouter.RegisterHandler(MessageType.PlayerInput, OnPlayerInputMessage);
                _networkService.MessageRouter.RegisterHandler(MessageType.Fire, OnFireMessage);
                _networkService.MessageRouter.RegisterHandler(MessageType.Handshake, OnHandshakeMessage);
        
                _networkService.OnClientConnected += OnClientConnected;
                _networkService.OnClientDisconnected += OnClientDisconnected;
            }
        }

        private void UnsubscribeFromEvents()
        {
            if (_networkService != null)
            {
                _networkService.MessageRouter.UnregisterHandler(MessageType.PlayerInput, OnPlayerInputMessage);
                _networkService.MessageRouter.UnregisterHandler(MessageType.Fire, OnFireMessage);
                _networkService.MessageRouter.UnregisterHandler(MessageType.Handshake, OnHandshakeMessage);
        
                _networkService.OnClientConnected -= OnClientConnected;
                _networkService.OnClientDisconnected -= OnClientDisconnected;
            }
        }

        private void OnPlayerInputMessage(INetworkMessage message, int senderId)
        {
            if (message is PlayerInputMessage inputMsg)
                ProcessPlayerInput(inputMsg, senderId);
        }

        private void OnFireMessage(INetworkMessage message, int senderId)
        {
            if (message is FireMessage fireMsg)
                ProcessFire(fireMsg, senderId);
        }

        private void OnHandshakeMessage(INetworkMessage message, int senderId)
        {
            if (message is HandshakeMessage handshake)
                HandleClientHandshake(handshake, senderId);
        }
        
        private void ProcessPlayerInput(PlayerInputMessage input, int senderId)
        {
            if (!_playerStates.TryGetValue(senderId, out var state))
                return;

            if (!state.IsAlive)
                return;
    
            Vector3 velocity = new Vector3(
                input.MoveInput.x * networkConfig.PlayerMoveSpeed,
                0,
                input.MoveInput.y * networkConfig.PlayerMoveSpeed
            );
    
            state.Velocity = new Vector3(
                velocity.x,
                state.Velocity.y,
                velocity.z
            );
    
            if (input.MouseWorldPosition != Vector3.zero)
            {
                Vector3 direction = input.MouseWorldPosition - state.Position;
                direction.y = 0;
        
                if (direction.sqrMagnitude > 0.01f)
                {
                    state.Rotation = Quaternion.LookRotation(direction);
                }
            }
    
            // Server Reconciliation
            state.LastProcessedInput = input.SequenceNumber;
        }
        
        private void ProcessFire(FireMessage fire, int senderId)
        {
            var shooter = _playerStates.GetValueOrDefault(senderId);
            
            if (shooter == null || !shooter.IsAlive)
            {
                _logger.Log(LogLevel.Warning, "Server", 
                    "Fire rejected: Player {0} invalid state", senderId);
                return;
            }
            
            uint bulletId = GetNextBulletId();
            
            var bullet = new ServerBullet
            {
                BulletId = bulletId,
                OwnerId = senderId,
                Position = fire.FirePosition,
                Direction = fire.FireDirection.normalized,
                Speed = fire.BulletSpeed,
                Damage = fire.Damage,
                SpawnTime = Time.time,
                Lifetime = 3f
            };
            
            _activeBullets.Add(bulletId, bullet);
            
            var spawnMsg = new BulletSpawnMessage
            {
                TargetId = -1,  // broadcast
                BulletId = bulletId,
                OwnerId = senderId,
                SpawnPosition = fire.FirePosition,
                Direction = fire.FireDirection.normalized,
                Speed = fire.BulletSpeed,
                Damage = fire.Damage
            };
            
            _networkService?.SendMessage(spawnMsg);
            
            _logger.Log(LogLevel.Debug, "Server", 
                "Bullet {0} spawned by Player {1} - broadcasted to all clients", bulletId, senderId);
        }
        
        private void HandleClientHandshake(HandshakeMessage handshake, int senderId)
        {
            _logger.Log(LogLevel.Info, "Server", 
                "Handshake from Client {0}: {1} - sending existing players", 
                senderId, handshake.PlayerName);
            
            int sentCount = 0;
            foreach (var existingState in _playerStates.Values)
            {
                if (existingState.PlayerId == senderId)
                    continue;
                
                var existingPlayerMsg = new PlayerJoinedMessage
                {
                    TargetId = senderId,
                    PlayerId = existingState.PlayerId,
                    PlayerName = $"Player_{existingState.PlayerId}",
                    SpawnPosition = existingState.Position,
                    Health = existingState.Health
                };
                
                _networkService?.SendMessage(existingPlayerMsg);
                sentCount++;
                
                _logger.Log(LogLevel.Debug, "Server", 
                    "Sent existing Player {0} info to Client {1}", 
                    existingState.PlayerId, senderId);
            }
            
            if (sentCount > 0)
            {
                _logger.Log(LogLevel.Info, "Server", 
                    "Sent {0} existing player(s) to Client {1}", sentCount, senderId);
            }
        }

        private void OnClientConnected(int clientId)
        {
            int playerId = clientId;
            
            Vector3[] spawnPositions = {
                new Vector3(-5f, 0f, 0f),
                new Vector3(5f, 0f, 0f),
                new Vector3(-5f, 0f, 5f),
                new Vector3(5f, 0f, 5f)
            };
            
            Vector3 spawnPos = spawnPositions[playerId % spawnPositions.Length];
            
            var newState = new PlayerState
            {
                PlayerId = playerId,
                Position = spawnPos,
                Rotation = Quaternion.identity,
                Velocity = Vector3.zero,
                Health = networkConfig.PlayerMaxHealth,
                IsAlive = true,
                LastProcessedInput = 0
            };
            
            _playerStates.TryAdd(playerId, newState);
            
            var joinMsg = new PlayerJoinedMessage
            {
                TargetId = -1,
                PlayerId = playerId,
                PlayerName = $"Player_{playerId}",
                SpawnPosition = spawnPos,
                Health = networkConfig.PlayerMaxHealth
            };
            
            _networkService?.SendMessage(joinMsg);
            
            _logger.Log(LogLevel.Info, "Server", 
                "Player {0} joined at {1} - broadcasted to all clients", playerId, spawnPos);
        }

        private void OnClientDisconnected(int clientId)
        {
            int playerId = clientId;

            if (_playerStates.TryRemove(playerId, out _))
            {
                var leftMsg = new PlayerLeftMessage
                {
                    TargetId = -1,
                    PlayerId = playerId
                };
                
                _networkService?.SendMessage(leftMsg);
                
                _logger.Log(LogLevel.Info, "Server", 
                    "Player {0} left - broadcasted to all clients", playerId);
            }
        }

        private void UpdateBullets()
        {
            var bulletsToRemove = new List<uint>();
            
            foreach (var kvp in _activeBullets)
            {
                var bullet = kvp.Value;
                
                if (Time.time - bullet.SpawnTime > bullet.Lifetime)
                {
                    bulletsToRemove.Add(kvp.Key);
                    BroadcastBulletDestroy(kvp.Key, DestroyReason.Timeout);
                    continue;
                }
                
                Vector3 prevPosition = bullet.Position;
                bullet.Position += bullet.Direction * bullet.Speed * Time.deltaTime;
                
                float moveDistance = Vector3.Distance(prevPosition, bullet.Position);
                RaycastHit hit;
                
                bool hitSomething = Physics.Raycast(
                    prevPosition,
                    bullet.Direction,
                    out hit,
                    moveDistance,
                    LayerMask.GetMask("Player", "Default")
                );
                
                if (hitSomething)
                {
                    var hitPlayerComponent = hit.collider.GetComponent<Arena.Player.Player>();
                    if (hitPlayerComponent != null && hitPlayerComponent.PlayerId != bullet.OwnerId)
                    {
                        var targetState = _playerStates.GetValueOrDefault(hitPlayerComponent.PlayerId);
                        if (targetState != null && targetState.IsAlive)
                        {
                            targetState.Health -= bullet.Damage;
                            
                            _logger.Log(LogLevel.Info, "Server", 
                                "Bullet {0} hit Player {1}. Damage: {2}, Remaining HP: {3}",
                                bullet.BulletId, hitPlayerComponent.PlayerId, bullet.Damage, targetState.Health);
                            
                            if (targetState.Health <= 0)
                            {
                                targetState.IsAlive = false;
                                targetState.Health = 0;
                                
                                _logger.Log(LogLevel.Info, "Server", 
                                    "Player {0} killed Player {1}", bullet.OwnerId, hitPlayerComponent.PlayerId);
                            }
                            
                            bulletsToRemove.Add(kvp.Key);
                            BroadcastBulletDestroy(kvp.Key, DestroyReason.HitPlayer);
                            continue;
                        }
                    }
                    
                    if (hit.collider.CompareTag("Default") || hit.collider.gameObject.layer == LayerMask.NameToLayer("Default"))
                    {
                        bulletsToRemove.Add(kvp.Key);
                        BroadcastBulletDestroy(kvp.Key, DestroyReason.HitWall);
                        continue;
                    }
                }
                
                if (Mathf.Abs(bullet.Position.x) > 50f || Mathf.Abs(bullet.Position.z) > 50f)
                {
                    bulletsToRemove.Add(kvp.Key);
                    BroadcastBulletDestroy(kvp.Key, DestroyReason.Timeout);
                }
            }
            
            foreach (var bulletId in bulletsToRemove)
            {
                _activeBullets.Remove(bulletId);
            }
        }

        private void BroadcastBulletDestroy(uint bulletId, DestroyReason reason)
        {
            var destroyMsg = new BulletDestroyMessage
            {
                TargetId = -1,
                BulletId = bulletId,
                Reason = reason
            };
            
            _networkService?.SendMessage(destroyMsg);
            
            _logger.Log(LogLevel.Debug, "Server", 
                "Bullet {0} destroyed ({1})", bulletId, reason);
        }

        // Synchronization
        private void BroadcastPlayerStates()
        {
            _stateUpdateTimer += Time.deltaTime;
            
            float updateInterval = 1f / networkConfig.ServerTickRateHz;
            
            if (_stateUpdateTimer >= updateInterval)
            {
                foreach (var state in _playerStates.Values)
                {
                    var stateMsg = new PlayerStateMessage
                    {
                        PlayerId = state.PlayerId,
                        Position = state.Position,
                        Rotation = state.Rotation,
                        Velocity = state.Velocity,
                        Health = state.Health,
                        IsAlive = state.IsAlive,
                        LastProcessedInput = state.LastProcessedInput
                    };
                    
                    _networkService?.SendMessage(stateMsg);
                }
                
                _stateUpdateTimer -= updateInterval;

                if (_stateUpdateTimer > updateInterval)
                {
                    _stateUpdateTimer = 0f;
                }
            }
        }
        private uint GetNextBulletId()
        {
            lock (_bulletIdLock)
            {
                return _nextBulletId++;
            }
        }
        private void OnDestroy()
        {
            UnsubscribeFromEvents();
        }
        private class PlayerState
        {
            public int PlayerId;
            public Vector3 Position;
            public Quaternion Rotation;
            public Vector3 Velocity;
            public float Health;
            public bool IsAlive;
            public uint LastProcessedInput;
        }

        private class ServerBullet
        {
            public uint BulletId;
            public int OwnerId;
            public Vector3 Position;
            public Vector3 Direction;
            public float Speed;
            public float Damage;
            public float SpawnTime;
            public float Lifetime;
        }
    }
}