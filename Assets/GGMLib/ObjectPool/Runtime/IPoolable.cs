using UnityEngine;

namespace GGMLib.ObjectPool.Runtime
{
    public interface IPoolable
    {
        PoolItemSO Item { get; set; }
        GameObject GameObject { get; }
        void ResetItem();
    }
}