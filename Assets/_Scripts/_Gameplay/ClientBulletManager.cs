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
                    _logger?.Log(LogLevel.Debug, "Bullet", "Server detected - destroying ClientBulletManager");
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
            _logger?.Log(LogLevel.Debug, "Bullet", "Received bullet {0} from Player {1}", 
                spawnMsg.BulletId, spawnMsg.OwnerId);
            
            if (bulletPrefab == null)
            {
                _logger?.Log(LogLevel.Error, "Bullet", "Bullet prefab not assigned!");
                return;
            }

            if (_networkBullets.ContainsKey(spawnMsg.BulletId))
            {
                _logger?.Log(LogLevel.Warning, "Bullet", "Bullet {0} already exists", spawnMsg.BulletId);
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

                _logger?.Log(LogLevel.Debug, "Bullet", "Spawned bullet {0}, total: {1}", 
                    spawnMsg.BulletId, _networkBullets.Count);
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
                _logger?.Log(LogLevel.Debug, "Bullet", "Bullet {0} already destroyed", bulletId);
                return;
            }
    
            if (!_networkBullets.TryGetValue(bulletId, out var bulletObj))
            {
                _logger?.Log(LogLevel.Warning, "Bullet",
                    "Cannot destroy bullet {0} - not found", bulletId);
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

            _logger?.Log(LogLevel.Debug, "Bullet", "Destroyed bullet {0}, remaining: {1}", 
                bulletId, _networkBullets.Count);
    
            if (_processedDestroys.Count > 1000)
            {
                _processedDestroys.Clear();
                _logger?.Log(LogLevel.Debug, "Bullet", "Cleared processed destroys cache");
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