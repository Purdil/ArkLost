using System;
using UnityEngine;

namespace GGMLib.ObjectPool.Runtime
{
    public class PoolInitializer : MonoBehaviour
    {
        [field: SerializeField] public PoolManagerSO PoolManagerAsser { get; private set; }

        private void Awake()
        {
            PoolInitializer[] initializers = FindObjectsByType<PoolInitializer>(FindObjectsSortMode.None);

            if (initializers.Length > 1)
                return;
            
            PoolManagerAsser.InitializePool(transform);
            DontDestroyOnLoad(gameObject);
        }
    }
}