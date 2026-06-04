using System.Collections.Generic;
using UnityEngine;

namespace GGMLib.ObjectPool.Runtime
{
    [CreateAssetMenu(fileName = "PoolManager", menuName = "Lib/Pool/PoolManager", order = 0)]
    public class PoolManagerSO : ScriptableObject
    {
        public List<PoolItemSO> itemList = new();

        private Dictionary<PoolItemSO, Pool> _pools = new();
        private Transform _rootTrm;

        public void InitializePool(Transform rootTrm)
        {
            _rootTrm = rootTrm;
            _pools = new Dictionary<PoolItemSO, Pool>();

            foreach (PoolItemSO item in itemList)
            {
                IPoolable poolable = item.prefab.GetComponent<IPoolable>();
                Debug.Assert(poolable != null, $"PoolItem은 반드시 IPoolable을 가져야합니다. {item.prefab.name}");

                Pool pool = new Pool(poolable, _rootTrm, item.initCount);
                _pools.Add(item, pool); //풀을 딕셔너리에 item을 기준으로 만들어 넣어준다.
            }
        }
        
        //w제네릭을 통해 원하는 아이템을 가져오게 한다.
        public T Pop<T>(PoolItemSO item) where T : IPoolable
        {
            Debug.Assert(_rootTrm != null,"풀 매니저는 초기화후 사용해야합니다.");

            if (_pools.TryGetValue(item, out Pool pool))
            {
                return (T)pool.Pop();
            }
            return default;
        }

        public void Push(IPoolable item)
        {
            Debug.Assert(_rootTrm != null, "풀 매니저는 초기화 후 사용해야 합니다.");
            if (_pools.TryGetValue(item.Item, out Pool pool))
            {
                pool.Push(item);
            }
        }
    }
}