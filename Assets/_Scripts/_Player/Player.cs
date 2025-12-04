using UnityEngine;
using Arena.Core;
using Arena.Core.DependencyInjection;
using Arena.Network;
using Arena.Network.Messages;
using Arena.Logging;
using Arena.Player.States;
using Arena.Input;

namespace Arena.Player
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(CapsuleCollider))]
    public class Player : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private NetworkConfig networkConfig;
        
        [Header("Identity")]
        [SerializeField] private int playerId = -1;
        [SerializeField] private bool isLocalPlayer = false;
        
        [Header("Movement")]
        [SerializeField] private float rotationSpeed = 720f;
        
        [Header("Combat")]
        [SerializeField] private float attackDamage = 25f;
        [SerializeField] private float bulletSpeed = 20f;
        [SerializeField] private float fireRate = 0.5f;
        [SerializeField] private GameObject bulletPrefab;
        [SerializeField] private Transform firePoint;
        
        [Header("Components")]
        [SerializeField] private Rigidbody rb;
        [SerializeField] private MeshRenderer meshRenderer;

        public int PlayerId => playerId;
        public bool IsLocalPlayer => isLocalPlayer;
        
        public float MoveSpeed => networkConfig != null ? networkConfig.PlayerMoveSpeed : 7f;
        public float RotationSpeed => rotationSpeed;
        public Rigidbody Rigidbody => rb;
        public float Health { get; private set; }
        public bool IsAlive => Health > 0;

        private INetworkService _networkService;
        private IGameLogger _logger;
        
        private IPlayerState _currentState;
        private LocalPlayerState _localState;
        private RemotePlayerState _remoteState;
        
        private PlayerInputHandler _inputHandler;
        
        private float _lastFireTime = float.NegativeInfinity;
        private Material _playerMaterial;
        private Color _originalColor;

        private void Awake()
        {
            if (networkConfig == null)
            {
                Debug.LogError("[Player] NetworkConfig is not assigned!");
            }
            
            InitializeComponents();
            InjectDependencies();
        }
        
        private void Start()
        {
            InitializeStates();
            SubscribeToNetworkEvents();
            SetupVisuals();
            
            Health = networkConfig != null ? networkConfig.PlayerMaxHealth : 100f;
            Debug.Log($"[PLAYER {playerId}] Using MoveSpeed: {MoveSpeed}");
            _logger.Log(LogLevel.Info, "Player", "Player {0} initialized - IsLocal: {1}", playerId, isLocalPlayer);
        }
        
        private void Update()
        {
            _currentState?.Update();
            
            if (isLocalPlayer && _inputHandler != null && _inputHandler.FirePressed && CanFire())
            {
                Fire();
            }
        }
        
        private void FixedUpdate()
        {
            _currentState?.FixedUpdate();
        }
        
        private void OnDestroy()
        {
            UnsubscribeFromNetworkEvents();
        }

        private void InitializeComponents()
        {
            if (rb == null)
                rb = GetComponent<Rigidbody>();
            
            if (meshRenderer == null)
                meshRenderer = GetComponentInChildren<MeshRenderer>();
            
            ConfigureRigidbody();
        }

        private void InjectDependencies()
        {
            var container = GameInstaller.Container;
            
            if (container == null)
            {
                Debug.LogError("[Player] DIContainer not found! Make sure GameInstaller exists.");
                return;
            }

            _networkService = container.Resolve<INetworkService>();
            _logger = container.Resolve<IGameLogger>();
        }

        private void InitializeStates()
        {
            _localState = new LocalPlayerState(this, _networkService, _logger);
            _remoteState = new RemotePlayerState(this, _logger);
            
            _currentState = isLocalPlayer ? (IPlayerState)_localState : _remoteState;
            _currentState.OnEnter();
            
            if (isLocalPlayer)
            {
                _inputHandler = GetComponent<PlayerInputHandler>();
            }
        }

        private void ConfigureRigidbody()
        {
            rb.mass = 1f;
            rb.linearDamping = 5f;
            rb.angularDamping = 5f;
            rb.useGravity = true;
            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
        }

        private void SetupVisuals()
        {
            if (meshRenderer != null)
            {
                _playerMaterial = new Material(meshRenderer.material);
                _originalColor = isLocalPlayer 
                    ? new Color(0.2f, 0.5f, 1f)
                    : new Color(1f, 0.3f, 0.3f);
                _playerMaterial.color = _originalColor;
                meshRenderer.material = _playerMaterial;
            }
        }

        public void SetPlayerId(int id)
        {
            playerId = id;
            UpdateObjectName();
        }
        
        public void SetLocalPlayer(bool isLocal)
        {
            isLocalPlayer = isLocal;
            
            _currentState?.OnExit();
            _currentState = isLocal ? (IPlayerState)_localState : _remoteState;
            _currentState?.OnEnter();
            
            if (isLocal)
            {
                _inputHandler = GetComponent<PlayerInputHandler>();
            }
            
            if (_playerMaterial != null)
            {
                _originalColor = isLocal ? new Color(0.2f, 0.5f, 1f) : new Color(1f, 0.3f, 0.3f);
                _playerMaterial.color = _originalColor;
            }
            
            UpdateObjectName();
        }
        
        private void UpdateObjectName()
        {
            gameObject.name = isLocalPlayer ? $"Player_{playerId}_Local" : $"Player_{playerId}_Remote";
        }
        
        private bool CanFire()
        {
            return IsAlive && Time.time - _lastFireTime >= fireRate;
        }

        private void Fire()
        {
            _lastFireTime = Time.time;
    
            Debug.Log($"[LOCAL] Player {playerId} calling Fire()");

            if (bulletPrefab == null)
            {
                _logger.Log(LogLevel.Error, "Player", "Bullet prefab not assigned!");
                return;
            }

            Vector3 spawnPos = firePoint != null 
                ? firePoint.position 
                : transform.position + transform.forward * 0.5f + Vector3.up * 0.5f;

            SendFireMessage(spawnPos, transform.forward);
    
            _logger.Log(LogLevel.Debug, "Player", 
                "Player {0} sent fire request to server", playerId);
        }

        private void SendFireMessage(Vector3 position, Vector3 direction)
        {
            if (_networkService == null || !_networkService.IsConnected)
                return;
            
            var fireMsg = new FireMessage
            {
                FirePosition = position,
                FireDirection = direction,
                Damage = attackDamage,
                BulletSpeed = bulletSpeed
            };
            
            _networkService.SendMessage(fireMsg);
        }

        public void TakeDamage(float damage, int attackerId)
        {
            if (!IsAlive) return;
            
            Health -= damage;
            
            _logger.Log(LogLevel.Info, "Player", "Player {0} took {1} damage from Player {2}. Health: {3}", 
                playerId, damage, attackerId, Health);
            
            if (_playerMaterial != null)
            {
                _playerMaterial.color = Color.red;
                Invoke(nameof(ResetColor), 0.1f);
            }
            
            if (Health <= 0)
            {
                Die();
            }
        }

        private void ResetColor()
        {
            if (_playerMaterial != null)
            {
                _playerMaterial.color = _originalColor;
            }
        }

        private void Die()
        {
            _logger.Log(LogLevel.Info, "Player", "Player {0} died", playerId);
            gameObject.SetActive(false);
        }

        private void SubscribeToNetworkEvents()
        {
            if (_networkService != null)
            {
                _networkService.MessageRouter.RegisterHandler(MessageType.PlayerState, OnPlayerStateMessage);
            }
        }

        private void UnsubscribeFromNetworkEvents()
        {
            if (_networkService != null)
            {
                _networkService.MessageRouter.UnregisterHandler(MessageType.PlayerState, OnPlayerStateMessage);
            }
        }

        private void OnPlayerStateMessage(INetworkMessage message, int senderId)
        {
            if (senderId != 0)
            {
                _logger.Log(LogLevel.Warning, "Player", 
                    "Rejected PlayerState from non-server source: {0}", senderId);
                return;
            }
    
            if (message is PlayerStateMessage stateMsg && stateMsg.PlayerId == playerId)
            {
                if (isLocalPlayer)
                {
                    _localState?.OnServerStateReceived(stateMsg);
                }
                else
                {
                    Health = stateMsg.Health;
                    _remoteState?.SetTargetTransform(stateMsg.Position, stateMsg.Rotation, stateMsg.Velocity);
                }
            }
        }
    }
}