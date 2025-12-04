using System;
using System.Collections.Generic;
using UnityEngine;

namespace Arena.Core.DependencyInjection
{
    public class DIContainer
    {
        private readonly Dictionary<Type, object> _singletons = new();
        private readonly Dictionary<Type, Func<DIContainer, object>> _factories = new();
        private readonly HashSet<Type> _resolving = new();
        private readonly object _lock = new object();

        public void RegisterSingleton<TInterface, TImplementation>(TImplementation instance) 
            where TImplementation : class, TInterface
        {
            if (instance == null)
                throw new ArgumentNullException(nameof(instance));

            var interfaceType = typeof(TInterface);
            
            lock (_lock)
            {
                if (_singletons.ContainsKey(interfaceType))
                {
                    Debug.LogWarning($"[DIContainer] {interfaceType.Name} already registered.");
                }
                
                _singletons[interfaceType] = instance;
            }
            
            Debug.Log($"[DIContainer] Registered singleton: {interfaceType.Name}");
        }

        public void RegisterFactory<TInterface>(Func<DIContainer, TInterface> factory) where TInterface : class
        {
            if (factory == null)
                throw new ArgumentNullException(nameof(factory));

            var interfaceType = typeof(TInterface);
            
            lock (_lock)
            {
                _factories[interfaceType] = container => factory(container);
            }
            
            Debug.Log($"[DIContainer] Registered factory: {interfaceType.Name}");
        }

        public T Resolve<T>() where T : class
        {
            var type = typeof(T);
            
            if (_singletons.TryGetValue(type, out var singleton))
            {
                return singleton as T;
            }

            lock (_lock)
            {
                
                if (_singletons.TryGetValue(type, out singleton))
                {
                    return singleton as T;
                }
                
                if (_resolving.Contains(type))
                {
                    throw new InvalidOperationException($"Circular dependency detected: {type.Name}");
                }
                
                if (_factories.TryGetValue(type, out var factory))
                {
                    _resolving.Add(type);
                    try
                    {
                        var instance = factory(this) as T;
                        _singletons[type] = instance; 
                        Debug.Log($"[DIContainer] Resolved from factory: {type.Name}");
                        return instance;
                    }
                    finally
                    {
                        _resolving.Remove(type);
                    }
                }

                throw new InvalidOperationException($"Service not registered: {type.Name}");
            }
        }

        public bool IsRegistered<T>() where T : class
        {
            var type = typeof(T);
            
            return _singletons.ContainsKey(type) || _factories.ContainsKey(type);
        }
 
        public void Unregister<T>() where T : class
        {
            var type = typeof(T);
            
            lock (_lock)
            {
                _singletons.Remove(type);
                _factories.Remove(type);
            }
            
            Debug.Log($"[DIContainer] Unregistered: {type.Name}");
        }

        public void Clear()
        {
            lock (_lock)
            {
                _singletons.Clear();
                _factories.Clear();
                _resolving.Clear();
            }
            
            Debug.Log("[DIContainer] All services cleared");
        }
    }
}