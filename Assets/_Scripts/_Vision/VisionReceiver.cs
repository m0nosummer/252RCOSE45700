using System.Collections.Generic;
using UnityEngine;
using Arena.Core;
using Arena.Core.DependencyInjection;
using Arena.Network;
using Arena.Logging;
using FischlWorks_FogWar;

namespace Arena.Vision
{
    public class VisionReceiver : MonoBehaviour
    {
        [Header("Debug")]
        [SerializeField] private bool showDebugInfo = false;
        
        private INetworkService _networkService;
        private IGameLogger _logger;
        
        private readonly Dictionary<int, GameObject> _trackedPlayers = new();
        private readonly Dictionary<int, Renderer[]> _playerRenderers = new();
        private readonly Dictionary<int, bool> _lastVisibility = new();
        
        private int _localPlayerId = -1;
        private bool _isInitialized = false;
        
        private csFogWar _fogWar;
        
        
        private void Start()
        {
            InjectDependencies();
            _fogWar = FindAnyObjectByType<csFogWar>();
            
            if (_fogWar == null)
            {
                _logger?.Log(LogLevel.Warning, "Vision", "csFogWar not found!");
            }
        }
        
        private void Update()
        {
            if (!_isInitialized && _networkService != null)
            {
                if (_networkService.IsServer)
                {
                    enabled = false;
                    return;
                }
                
                if (_networkService.IsConnected && _networkService.LocalClientId > 0)
                {
                    Initialize();
                }
            }
            
            if (_isInitialized && _fogWar != null)
            {
                UpdatePlayerVisibility();
            }
        }
        
        private void InjectDependencies()
        {
            var container = GameInstaller.Container;
            if (container == null) return;
            
            _networkService = container.Resolve<INetworkService>();
            _logger = container.Resolve<IGameLogger>();
        }
        
        private void Initialize()
        {
            _localPlayerId = _networkService.LocalClientId;
            _isInitialized = true;
            
            _logger?.Log(LogLevel.Info, "Vision", 
                "VisionReceiver initialized for Player {0}", _localPlayerId);
        }
        
        private void UpdatePlayerVisibility()
        {
            foreach (var kvp in _trackedPlayers)
            {
                int playerId = kvp.Key;
                GameObject playerObj = kvp.Value;
                
                if (playerId == _localPlayerId)
                    continue;
                
                if (playerObj == null)
                    continue;
                
                bool isVisible = _fogWar.CheckVisibility(playerObj.transform.position, 0);
                
                if (!_lastVisibility.TryGetValue(playerId, out bool wasVisible) || wasVisible != isVisible)
                {
                    _lastVisibility[playerId] = isVisible;
                    SetPlayerVisible(playerId, isVisible);
                    
                    if (showDebugInfo)
                    {
                        _logger?.Log(LogLevel.Debug, "Vision", 
                            "Player {0} visibility: {1}", playerId, isVisible);
                    }
                }
            }
        }
        
        private void SetPlayerVisible(int playerId, bool visible)
        {
            if (!_playerRenderers.TryGetValue(playerId, out var renderers))
            {
                if (_trackedPlayers.TryGetValue(playerId, out var playerObj) && playerObj != null)
                {
                    renderers = playerObj.GetComponentsInChildren<Renderer>();
                    _playerRenderers[playerId] = renderers;
                }
            }
            
            if (renderers != null)
            {
                foreach (var renderer in renderers)
                {
                    if (renderer != null)
                    {
                        renderer.enabled = visible;
                    }
                }
            }
        }
        
        public void RegisterPlayer(int playerId, GameObject playerObject)
        {
            if (playerObject == null) return;
            
            _trackedPlayers[playerId] = playerObject;
            _playerRenderers[playerId] = playerObject.GetComponentsInChildren<Renderer>();
            
            bool isLocal = (playerId == _localPlayerId) || 
                           (_networkService != null && playerId == _networkService.LocalClientId);
            
            if (isLocal)
            {
                _localPlayerId = playerId;
            }
            else
            {
                SetPlayerVisible(playerId, false);
            }
            
            _logger?.Log(LogLevel.Debug, "Vision", "Registered Player {0}", playerId);
        }
        
        public void UnregisterPlayer(int playerId)
        {
            _trackedPlayers.Remove(playerId);
            _playerRenderers.Remove(playerId);
            _lastVisibility.Remove(playerId);
        }
        
        private void OnDestroy()
        {
            _trackedPlayers.Clear();
            _playerRenderers.Clear();
            _lastVisibility.Clear();
        }
    }
}