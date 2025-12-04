using UnityEngine;
using Arena.Network;
using Arena.Logging;
using Arena.Core.Utilities;
using Arena.Network.Profiling;

namespace Arena.Core.DependencyInjection
{
    public class GameInstaller : MonoBehaviour
    {
        [SerializeField] private NetworkConfig networkConfig;
        
        public static DIContainer Container { get; private set; }

        private void Awake()
        {
            if (Container != null)
            {
                Debug.LogWarning("[GameInstaller] Container already initialized!");
                Destroy(gameObject);
                return;
            }
            
            Container = new DIContainer();
            
            RegisterCoreServices();
            RegisterLoggingServices();
            RegisterNetworkServices();
            
            DontDestroyOnLoad(gameObject);
            
            Debug.Log("[GameInstaller] Initialization complete");
        }

        private void RegisterCoreServices()
        {
            var dispatcher = gameObject.AddComponent<UnityMainThreadDispatcher>();
            Container.RegisterSingleton<UnityMainThreadDispatcher, UnityMainThreadDispatcher>(dispatcher);
            
            Debug.Log("[GameInstaller] Core services registered");
        }

        private void RegisterLoggingServices()
        {
            Container.RegisterFactory<IGameLogger>(c => new UnityLogger());
            
            Debug.Log("[GameInstaller] Logging services registered");
        }

        private void RegisterNetworkServices()
        {
            if (networkConfig == null)
            {
                Debug.LogError("[GameInstaller] NetworkConfig not assigned!");
                networkConfig = ScriptableObject.CreateInstance<NetworkConfig>();
            }
            
            var logger = Container.Resolve<IGameLogger>();

            Container.RegisterFactory<NetworkProfiler>(c => new NetworkProfiler(logger));
            Container.RegisterFactory<IPacketProcessor>(c => new PacketProcessor(logger));
            Container.RegisterFactory<IConnectionManager>(c => new ConnectionManager(logger));
            Container.RegisterFactory<IMessageRouter>(c => new MessageRouter(logger));

            var networkService = gameObject.AddComponent<NetworkService>();
            networkService.Initialize(networkConfig, logger);
            Container.RegisterSingleton<INetworkService, NetworkService>(networkService);
            
            Debug.Log("[GameInstaller] Network services registered");
        }

        private void OnDestroy()
        {
            if (Container != null)
            {
                if (Container.IsRegistered<INetworkService>())
                {
                    var networkService = Container.Resolve<INetworkService>();
                    networkService?.Disconnect();
                }
                
                Container.Clear();
                Container = null;
            }
            
            Debug.Log("[GameInstaller] Shutdown complete");
        }
    }
}