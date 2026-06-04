using System.Collections.Generic;
using UnityEngine;

namespace GGMLib.ObjectPool.Runtime
{
    public class Pool
    {
        private readonly Stack<IPoolable> _pool;
        private readonly Transform _parentTrm;
        private readonly GameObject _prefab;

        public Pool(IPoolable poolable, Transform parent, int initCount)
        {
            _pool = new Stack<IPoolable>(initCount);
            _parentTrm = parent;
            _prefab = poolable.GameObject;

            for (int i = 0; i < initCount; i++)
            {
                GameObject go = Object.Instantiate(_prefab, _parentTrm);
                go.SetActive(false);
                IPoolable item = go.GetComponent<IPoolable>();
                Debug.Assert(item != null, $"Poolable item 은 반드시 IPoolable을 구현해야합니다: {_prefab}");
                _pool.Push(item);
            }
        }

        public IPoolable Pop()
        {
            IPoolable item;
            if (_pool.Count > 0)
            {
                item = _pool.Pop();
                item.GameObject.SetActive(true);
            }
            else
            {
                GameObject go = Object.Instantiate(_prefab, _parentTrm);
                item = go.GetComponent<IPoolable>();
            }
            item.ResetItem();
            return item;
        }

        public void Push(IPoolable item)
        {
            item.GameObject.SetActive(false);
            _pool.Push(item);
        }
    }
}