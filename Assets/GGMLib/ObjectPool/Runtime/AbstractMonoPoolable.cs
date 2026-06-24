using UnityEngine;

namespace GGMLib.ObjectPool.Runtime
{
    public abstract class AbstractMonoPoolable : MonoBehaviour, IPoolable
    {
        [field: SerializeField] public PoolItemSO Item { get; set; }
        public GameObject GameObject => this != null ? gameObject : null;
        
        public virtual void ResetItem() { }
    }
}