using System.Collections.Generic;
using UnityEngine;
using Arena.Core;
using Arena.Core.DependencyInjection;
using Arena.Network;
using Arena.Network.Messages;
using Arena.Logging;
using Arena.Combat;

namespace Arena.Gameplay
{
    public class ClientBulletManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GameObject bulletPrefab;
        
        private INetworkService _networkService;
        private IGameLogger _logger;
        private readonly HashSet<uint> _processedDestroys = new HashSet<uint>();
        private readonly Dictionary<uint, GameObject> _networkBullets = new();
        private bool _isInitialized = false;

        private void Start()
        {
            InjectDependencies();
        }

        private void Update()
        {
            if (!_isInitialized && _networkService != null)
            {
                if (_networkService.IsServer)
                {
                    Debug.Log("[ClientBulletManager] Server detected - destroying");
                    Destroy(gameObject);
                    return;
                }
                
                if (_networkService.IsConnected)
                {
                    Initialize();
                }
            }
        }

        private void Initialize()
        {
            _isInitialized = true;
            
            SubscribeToNetworkEvents();
            
            Debug.Log("[ClientBulletManager] Initialized for CLIENT");
            _logger?.Log(LogLevel.Info, "Bullet", "ClientBulletManager initialized");
        }

        private void InjectDependencies()
        {
            var container = GameInstaller.Container;
            
            if (container == null)
            {
                Debug.LogError("[ClientBulletManager] DIContainer not found!");
                return;
            }

            _networkService = container.Resolve<INetworkService>();
            _logger = container.Resolve<IGameLogger>();
        }

        private void SubscribeToNetworkEvents()
        {
            if (_networkService != null)
            {
                _networkService.MessageRouter.RegisterHandler(MessageType.BulletSpawn, OnBulletSpawnMessage);
                _networkService.MessageRouter.RegisterHandler(MessageType.BulletDestroy, OnBulletDestroyMessage);
            }
        }

        private void UnsubscribeFromNetworkEvents()
        {
            if (_networkService != null)
            {
                _networkService.MessageRouter.UnregisterHandler(MessageType.BulletSpawn, OnBulletSpawnMessage);
                _networkService.MessageRouter.UnregisterHandler(MessageType.BulletDestroy, OnBulletDestroyMessage);
            }
        }

        private void OnBulletSpawnMessage(INetworkMessage message, int senderId)
        {
            if (!_isInitialized) return;
            
            if (message is BulletSpawnMessage spawnMsg)
                HandleBulletSpawn(spawnMsg);
        }

        private void OnBulletDestroyMessage(INetworkMessage message, int senderId)
        {
            if (!_isInitialized) return;
            
            if (message is BulletDestroyMessage destroyMsg)
                HandleBulletDestroy(destroyMsg);
        }

        private void HandleBulletSpawn(BulletSpawnMessage spawnMsg)
        {
            Debug.Log($"[CLIENT] Received bullet {spawnMsg.BulletId} from Player {spawnMsg.OwnerId}");
            
            if (bulletPrefab == null)
            {
                _logger?.Log(LogLevel.Error, "Bullet", "Bullet prefab not assigned!");
                return;
            }

            if (_networkBullets.ContainsKey(spawnMsg.BulletId))
            {
                Debug.LogWarning($"[CLIENT] Bullet {spawnMsg.BulletId} Already exists. Skipping.");
                return;
            }

            GameObject bulletObj = Instantiate(bulletPrefab, spawnMsg.SpawnPosition, Quaternion.identity);
            var bullet = bulletObj.GetComponent<Bullet>();

            if (bullet != null)
            {
                bullet.FireFromNetwork(
                    spawnMsg.BulletId,
                    spawnMsg.Direction,
                    spawnMsg.OwnerId,
                    spawnMsg.Speed,
                    spawnMsg.Damage
                );

                _networkBullets.Add(spawnMsg.BulletId, bulletObj);

                Debug.Log($"[CLIENT] Spawned bullet {spawnMsg.BulletId}, Dictionary size: {_networkBullets.Count}");
            }
            else
            {
                _logger?.Log(LogLevel.Error, "Bullet", "Bullet component not found!");
                Destroy(bulletObj);
            }
        }

        private void HandleBulletDestroy(BulletDestroyMessage destroyMsg)
        {
            uint bulletId = destroyMsg.BulletId;
    
            if (_processedDestroys.Contains(bulletId))
            {
                Debug.Log($"[CLIENT] Bullet {bulletId} already destroyed, ignoring duplicate");
                return;
            }
    
            Debug.Log($"[CLIENT] Destroy request for bullet {bulletId}, Dictionary has: {_networkBullets.Count} bullets");
    
            if (!_networkBullets.TryGetValue(bulletId, out var bulletObj))
            {
                Debug.LogWarning($"[CLIENT] Cannot destroy bullet {bulletId}! Dictionary keys: {string.Join(", ", _networkBullets.Keys)}");
                _processedDestroys.Add(bulletId); 
                return;
            }

            if (bulletObj != null)
            {
                var bullet = bulletObj.GetComponent<Bullet>();
                bullet?.DestroyFromNetwork();
            }

            _networkBullets.Remove(bulletId);
            _processedDestroys.Add(bulletId);

            Debug.Log($"[CLIENT] Destroyed bullet {bulletId}, Dictionary size: {_networkBullets.Count}");
    
            if (_processedDestroys.Count > 1000)
            {
                _processedDestroys.Clear();
                Debug.Log("[CLIENT] Cleared processed destroys cache");
            }
        }
        
        private void OnDestroy()
        {
            UnsubscribeFromNetworkEvents();
            
            foreach (var bulletObj in _networkBullets.Values)
            {
                if (bulletObj != null)
                    Destroy(bulletObj);
            }
            _networkBullets.Clear();
        }
    }
}