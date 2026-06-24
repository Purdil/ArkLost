using GGMLib.ObjectPool.Runtime;
using UnityEngine;

namespace _Scripts.CoreSystem.Effects
{
    [CreateAssetMenu(fileName = "ShowVFX", menuName = "ShowVFXInAnim", order = 0)]
    public class ShowVFXInAnimationSO : ScriptableObject
    {
        public PoolItemSO PoolItemSo;
        public Vector3 positionOffset;
        public Quaternion rotationOffset;
    }
}