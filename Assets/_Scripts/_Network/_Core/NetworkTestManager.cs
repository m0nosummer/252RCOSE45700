using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Arena.Core;
using Arena.Core.DependencyInjection;
using Arena.Core.Utilities;
using Arena.Logging;
using Arena.Network.Messages;
using Arena.Player;

namespace Arena.Network
{
    public class NetworkTestManager : MonoBehaviour
    {
        [Header("Network Settings")]
        [SerializeField] private string serverIP = "127.0.0.1";
        [SerializeField] private int serverPort = 7777;
        
        [Header("UI References")]
        [SerializeField] private Button serverButton;
        [SerializeField] private Button clientButton;
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private GameObject networkTestUI;
        
        [Header("Player Prefab")]
        [SerializeField] private GameObject playerPrefab;

        private INetworkService _networkService;
        private IGameLogger _logger;
        private int _localPlayerId = -1;
        private System.Text.StringBuilder _logBuilder = new System.Text.StringBuilder();
        
        private Dictionary<int, GameObject> _spawnedPlayers = new Dictionary<int, GameObject>();

        private void Start()
        {
            InitializeDependencies();
            SetupUI();
            SubscribeToNetworkEvents();
            
            UpdateStatus("Ready - Choose Server or Client");
        }

        private void InitializeDependencies()
        {
            var container = GameInstaller.Container;
            
            if (container == null)
            {
                Debug.LogError("[NetworkTestManager] DIContainer not found!");
                return;
            }

            _networkService = container.Resolve<INetworkService>();
            _logger = container.Resolve<IGameLogger>();
        }

        private void SetupUI()
        {
            if (serverButton != null)
                serverButton.onClick.AddListener(StartServer);
            
            if (clientButton != null)
                clientButton.onClick.AddListener(StartClient);
        }

        private void SubscribeToNetworkEvents()
        {
            if (_networkService != null)
            {
                _networkService.MessageRouter.RegisterHandler(MessageType.Handshake, OnHandshakeMessage);
                _networkService.MessageRouter.RegisterHandler(MessageType.PlayerJoined, OnPlayerJoinedMessage);
                _networkService.MessageRouter.RegisterHandler(MessageType.PlayerLeft, OnPlayerLeftMessage);
        
                _networkService.OnClientConnected += HandleClientConnected;
                _networkService.OnClientDisconnected += HandleClientDisconnected;
                _networkService.OnConnectionFailed += HandleConnectionFailed;
            }
        }

        private void UnsubscribeFromNetworkEvents()
        {
            if (_networkService != null)
            {
                _networkService.MessageRouter.UnregisterHandler(MessageType.Handshake, OnHandshakeMessage);
                _networkService.MessageRouter.UnregisterHandler(MessageType.PlayerJoined, OnPlayerJoinedMessage);
                _networkService.MessageRouter.UnregisterHandler(MessageType.PlayerLeft, OnPlayerLeftMessage);
        
                _networkService.OnClientConnected -= HandleClientConnected;
                _networkService.OnClientDisconnected -= HandleClientDisconnected;
                _networkService.OnConnectionFailed -= HandleConnectionFailed;
            }
        }

        private void OnHandshakeMessage(INetworkMessage message, int senderId)
        {
            if (message is HandshakeMessage handshake)
                HandleHandshake(handshake, senderId);
        }

        private async void StartServer()
        {
            if (_networkService == null)
            {
                AddLog("[ERROR] NetworkService not available!");
                return;
            }
            try
            {
                DisableButtons();
                UpdateStatus("Starting server...");
                AddLog("[SERVER] Starting...");

                await _networkService.StartServerAsync(serverPort);

                UpdateStatus($"Server running on port {serverPort}");
                AddLog($"[SERVER] Started on port {serverPort}");
            }
            catch (Exception ex)
            {
                UpdateStatus("Failed to start server");
                AddLog($"[ERROR] Server start failed: {ex.Message}");
                EnableButtons();
            }
        }

        private async void StartClient()
        {
            if (_networkService == null)
            {
                AddLog("[ERROR] NetworkService not available!");
                return;
            }

            try
            {
                DisableButtons();
                UpdateStatus("Connecting to server...");
                AddLog($"[CLIENT] Connecting to {serverIP}:{serverPort}...");

                await _networkService.ConnectToServerAsync(serverIP, serverPort);

                UpdateStatus($"Connected to {serverIP}:{serverPort}");
                AddLog($"[CLIENT] Connected!");

                SendHandshake();
                if (networkTestUI != null)
                {
                    networkTestUI.SetActive(false);
                }
            }
            catch (Exception ex)
            {
                UpdateStatus("Failed to connect");
                AddLog($"[ERROR] Connection failed: {ex.Message}");
                EnableButtons();
            }
        }

        private void SendHandshake()
        {
            var handshake = new HandshakeMessage
            {
                ClientVersion = "1.0.0",
                PlayerName = $"Player_{UnityEngine.Random.Range(100, 999)}"
            };

            _networkService.SendMessage(handshake);
            AddLog($"[CLIENT] Sent handshake as {handshake.PlayerName}");
        }

        // Unity API -> use dispatcher
        private void HandleClientConnected(int clientId)
        {
            UnityMainThreadDispatcher.Enqueue(() =>
            {
                UpdateStatus($"Client {clientId} connected");
                AddLog($"[EVENT] Client {clientId} connected - waiting for PlayerJoined...");
            });
        }

        private void HandleClientDisconnected(int clientId)
        {
            UnityMainThreadDispatcher.Enqueue(() =>
            {
                UpdateStatus($"Client {clientId} disconnected");
                AddLog($"[EVENT] Client {clientId} disconnected!");
                RemovePlayer(clientId);
            });
        }

        private void HandleConnectionFailed(string reason)
        {
            UnityMainThreadDispatcher.Enqueue(() =>
            {
                UpdateStatus($"Connection failed: {reason}");
                AddLog($"[ERROR] {reason}");
                EnableButtons();
            });
        }

        private void HandleHandshake(HandshakeMessage handshake, int senderId)
        {
            AddLog($"[RECEIVED] Handshake from Player {senderId}: {handshake.PlayerName}");

            if (_networkService.IsServer)
            {
                var response = new HandshakeMessage
                {
                    TargetId = senderId,
                    ClientVersion = "1.0.0",
                    PlayerName = "Server"
                };
                _networkService.SendMessage(response);
        
                AddLog($"[SERVER] Sent ID assignment to Client {senderId}");
            }
            else
            {
                if (senderId == 0 && handshake.TargetId != -1)
                {
                    if (_localPlayerId == -1)
                    {
                        _localPlayerId = handshake.TargetId;
                        AddLog($"[CLIENT] Assigned Player ID: {_localPlayerId}");
            
                        if (_spawnedPlayers.ContainsKey(_localPlayerId))
                        {
                            AddLog($"[FIX] Removing incorrectly spawned remote");
                            RemovePlayer(_localPlayerId);
                        }
            
                        AddLog($"[CLIENT] Spawning local player {_localPlayerId}");
                        SpawnPlayer(_localPlayerId, true);
                    }
                }
            }
        }

        private void SpawnPlayer(int playerId, bool isLocal)
        {
            Debug.Log($"[SpawnPlayer] Called: playerId={playerId}, isLocal={isLocal}");
            
            if (_spawnedPlayers.ContainsKey(playerId))
            {
                AddLog($"[WARNING] Player {playerId} already spawned!");
                return;
            }

            if (playerPrefab == null)
            {
                Debug.LogError("[SpawnPlayer] Player prefab is NULL!");
                AddLog("[ERROR] Player prefab not assigned!");
                return;
            }

            // TODO : GameConstant 쓰기
            Vector3[] spawnPositions = {
                new Vector3(-5f, 1f, 0f),
                new Vector3(5f, 1f, 0f),
                new Vector3(-5f, 1f, 5f),
                new Vector3(5f, 1f, 5f)
            };
            
            Vector3 spawnPos = spawnPositions[playerId % spawnPositions.Length];

            GameObject playerObj = Instantiate(playerPrefab, spawnPos, Quaternion.identity);
            
            var player = playerObj.GetComponent<Player.Player>();

            if (player != null)
            {
                player.SetPlayerId(playerId);
                player.SetLocalPlayer(isLocal);
                
                _spawnedPlayers[playerId] = playerObj;
                
                AddLog($"[{(isLocal ? "LOCAL" : "REMOTE")}] Player {playerId} spawned at {spawnPos}");
            }
            else
            {
                Debug.LogError("[SpawnPlayer] Player component NOT found on prefab!");
                AddLog("[ERROR] Player component not found on prefab!");
                Destroy(playerObj);
            }
        }

        private void RemovePlayer(int playerId)
        {
            if (_spawnedPlayers.TryGetValue(playerId, out var playerObj))
            {
                if (playerObj != null)
                {
                    Destroy(playerObj);
                }
                _spawnedPlayers.Remove(playerId);
                
                AddLog($"[REMOVED] Player {playerId} removed");
            }
        }
        private void OnPlayerJoinedMessage(INetworkMessage message, int senderId)
        {
            if (message is PlayerJoinedMessage joinMsg)
                HandlePlayerJoined(joinMsg);
        }

        private void OnPlayerLeftMessage(INetworkMessage message, int senderId)
        {
            if (message is PlayerLeftMessage leftMsg)
                HandlePlayerLeft(leftMsg);
        }

       
        private void HandlePlayerJoined(PlayerJoinedMessage joinMsg)
        {
            AddLog($"[PLAYER JOIN] Player {joinMsg.PlayerId} joined at {joinMsg.SpawnPosition}");
    
            if (_networkService.LocalClientId == -1)
            {
                AddLog($"[SKIP] LocalClientId not assigned yet");
                return;
            }
    
            if (joinMsg.PlayerId == _networkService.LocalClientId)
            {
                AddLog($"[SKIP] Self (handled in Handshake)");
                return;
            }
    
            if (_spawnedPlayers.ContainsKey(joinMsg.PlayerId))
            {
                AddLog($"[WARNING] Player {joinMsg.PlayerId} already exists!");
                return;
            }
    
            SpawnPlayer(joinMsg.PlayerId, false);
        }

        private void HandlePlayerLeft(PlayerLeftMessage leftMsg)
        {
            AddLog($"[PLAYER LEFT] Player {leftMsg.PlayerId} left");
            RemovePlayer(leftMsg.PlayerId);
        }

        private void UpdateStatus(string status)
        {
            if (statusText != null)
            {
                statusText.text = $"Status: {status}";
            }
            
            _logger?.Log(LogLevel.Info, "Test", status);
        }

        private void AddLog(string message)
        {
            _logger?.Log(LogLevel.Info, "Test", message);
        }
        
        private void OnDestroy()
        {
            UnsubscribeFromNetworkEvents();
            
            foreach (var kvp in _spawnedPlayers)
            {
                if (kvp.Value != null)
                    Destroy(kvp.Value);
            }
            _spawnedPlayers.Clear();
        }
        
        private void DisableButtons()
        {
            if (serverButton != null) serverButton.interactable = false;
            if (clientButton != null) clientButton.interactable = false;
        }

        private void EnableButtons()
        {
            if (serverButton != null) serverButton.interactable = true;
            if (clientButton != null) clientButton.interactable = true;
        }
    }
}