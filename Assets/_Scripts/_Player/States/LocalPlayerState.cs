using UnityEngine;
using System.Collections.Generic;
using Arena.Core;
using Arena.Network.Messages;
using Arena.Logging;
using Arena.Input;

namespace Arena.Player.States
{
    public class LocalPlayerState : IPlayerState
    {
        private readonly Player _player;
        private readonly INetworkService _networkService;
        private readonly IGameLogger _logger;
        
        private PlayerInputHandler _inputHandler;
        private Vector2 _moveInput;
        private Vector3 _mouseWorldPosition;
        private uint _inputSequence;

        private readonly Queue<PendingInput> _pendingInputs = new Queue<PendingInput>(128);
        private const int MAX_PENDING_INPUTS = 60;

        private float _lastReconciliationTime;
        private const float MIN_RECONCILIATION_INTERVAL = 0.1f;
        
        private float _lastInputSendTime;
        private const float INPUT_SEND_INTERVAL = 0.05f; // 20Hz

        public LocalPlayerState(Player player, INetworkService networkService, IGameLogger logger)
        {
            _player = player;
            _networkService = networkService;
            _logger = logger;
        }

        public void OnEnter()
        {
            _logger.Log(LogLevel.Debug, "Player", 
                "Local player state activated for {0}", _player.PlayerId);
    
            if (_player.Rigidbody != null)
            {
                _player.Rigidbody.isKinematic = true;
            }
    
            _lastReconciliationTime = Time.time;
            _lastInputSendTime = Time.time;
    
            SetupInputHandler();
            SetupCamera();
        }

        public void Update()
        {
            HandleInput();
            RotateTowardsMouse();
        }

        public void FixedUpdate()
        {
            ApplyMovement();
        }

        public void OnServerStateReceived(PlayerStateMessage serverState)
        {
            while (_pendingInputs.Count > 0)
            {
                var oldest = _pendingInputs.Peek();
                if (oldest.SequenceNumber <= serverState.LastProcessedInput)
                {
                    _pendingInputs.Dequeue();
                }
                else
                {
                    break;
                }
            }

            float positionError = Vector3.Distance(_player.transform.position, serverState.Position);
            
            if (positionError < 0.3f)
            {
                return;
            }
            
            float timeSinceLastReconciliation = Time.time - _lastReconciliationTime;
            if (timeSinceLastReconciliation < MIN_RECONCILIATION_INTERVAL && positionError < 1.0f)
            {
                return;
            }
            
            _lastReconciliationTime = Time.time;
            
            if (positionError > 1.0f)
            {
                _logger.Log(LogLevel.Debug, "Player",
                    "Desync {0:F2}m - reconciling", positionError);
                
                _player.transform.position = serverState.Position;
                _player.transform.rotation = serverState.Rotation;
                
                Vector3 replayPosition = serverState.Position;
                foreach (var pending in _pendingInputs)
                {
                    Vector3 movement = new Vector3(
                        pending.MoveInput.x * _player.MoveSpeed,
                        0,
                        pending.MoveInput.y * _player.MoveSpeed
                    ) * Time.fixedDeltaTime;
                    
                    replayPosition += movement;
                }
                
                _player.transform.position = replayPosition;
            }
            else if (positionError > 0.3f)
            {
                _player.transform.position = Vector3.Lerp(
                    _player.transform.position,
                    serverState.Position,
                    0.15f
                );
            }
            
            while (_pendingInputs.Count > MAX_PENDING_INPUTS)
            {
                _pendingInputs.Dequeue();
            }
        }

        private void HandleInput()
        {
            if (_inputHandler == null) return;
            
            _moveInput = _inputHandler.MoveInput;
            _mouseWorldPosition = _inputHandler.MouseWorldPosition;
            
            if (Time.time - _lastInputSendTime >= INPUT_SEND_INTERVAL)
            {
                SendInputToServer();
                _lastInputSendTime = Time.time;
            }
        }

        private void RotateTowardsMouse()
        {
            if (_mouseWorldPosition == Vector3.zero) return;
            
            Vector3 direction = _mouseWorldPosition - _player.transform.position;
            direction.y = 0;
            
            if (direction.sqrMagnitude > 0.01f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(direction);
                _player.transform.rotation = Quaternion.RotateTowards(
                    _player.transform.rotation,
                    targetRotation,
                    _player.RotationSpeed * Time.deltaTime
                );
            }
        }

        private void ApplyMovement()
        {
            Vector3 velocity = new Vector3(_moveInput.x, 0, _moveInput.y) * _player.MoveSpeed;
            Vector3 movement = velocity * Time.fixedDeltaTime;
            _player.transform.position += movement;
            
            if (Physics.Raycast(_player.transform.position, Vector3.down, out RaycastHit hit, 2f))
            {
                _player.transform.position = new Vector3(
                    _player.transform.position.x,
                    hit.point.y,
                    _player.transform.position.z
                );
            }
        }

        private void SendInputToServer()
        {
            if (_networkService == null || !_networkService.IsConnected)
                return;

            uint currentSequence = _inputSequence++;
    
            var inputMsg = new PlayerInputMessage
            {
                MoveInput = _moveInput,
                MouseWorldPosition = _mouseWorldPosition,
                AttackPressed = _inputHandler.FireHeld,
                SequenceNumber = currentSequence
            };
            
            _networkService.SendMessage(inputMsg);
            
            _pendingInputs.Enqueue(new PendingInput
            {
                SequenceNumber = currentSequence,
                MoveInput = _moveInput,
                Position = _player.transform.position,
                Timestamp = Time.time
            });
        }

        private void SetupInputHandler()
        {
            _inputHandler = _player.GetComponent<PlayerInputHandler>();
            if (_inputHandler == null)
            {
                _inputHandler = _player.gameObject.AddComponent<PlayerInputHandler>();
            }
            
            int groundLayer = LayerMask.GetMask("Default", "Ground");
            if (groundLayer == 0) groundLayer = ~0;
            _inputHandler.SetGroundLayer(groundLayer);
            
            _inputHandler.EnableInput();
            _inputHandler.OnFirePerformed += OnFireInput;
        }

        private void OnFireInput()
        {
            
        }
        
        public void OnExit()
        {
            if (_inputHandler != null)
            {
                _inputHandler.DisableInput();
                _inputHandler.OnFirePerformed -= OnFireInput;
            }
            
            _pendingInputs.Clear();
        }
        
        private void SetupCamera()
        {
            if (Camera.main != null)
            {
                var cameraFollow = Camera.main.GetComponent<Arena.Gameplay.CameraFollow>();
                if (cameraFollow != null)
                {
                    cameraFollow.SetTarget(_player.transform);
                }
            }
        }

        private struct PendingInput
        {
            public uint SequenceNumber;
            public Vector2 MoveInput;
            public Vector3 Position;
            public float Timestamp;
        }
    }
}