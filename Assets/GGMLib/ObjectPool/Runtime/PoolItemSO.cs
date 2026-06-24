using System;
using UnityEngine;

namespace GGMLib.ObjectPool.Runtime
{
    [CreateAssetMenu(fileName = "PoolItem", menuName = "Lib/Pool/Item", order = 0)]
    public class PoolItemSO : ScriptableObject
    {
        [HideInInspector] public string itemName;
        public GameObject prefab;
        public int initCount;

        private void OnValidate()
        {
            if (prefab != null && !prefab.TryGetComponent(out IPoolable _))
            {
                Debug.LogError($"Poolable component not found on {prefab.name}");
                prefab = null;
            }
        }
    }
}