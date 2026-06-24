using UnityEngine;

namespace _Scripts.CombatSystem
{
    [CreateAssetMenu(fileName = "DamageCastRangeData", menuName = "DamageCastRangeData", order = 0)]
    public class DamageCastRangeDataSO : ScriptableObject
    {
        [field: SerializeField] public CastType CastType { get; private set; } = CastType.Sphere;
        [field: SerializeField] public Vector3 BoxSize { get; private set; } = Vector3.one;
        [field: SerializeField, Range(0.5f,3f)] public float CasterRadius { get; private set; } = 1;
        [field: SerializeField, Range(0f, 1f)] public float CasterInterpolation { get; private set; } = 0.5f; //보간, 뒤로 빼는 정도
        [field: SerializeField, Range(0f, 3f)] public float CastingRange { get; private set; } = 1f;
        [field: SerializeField] public bool IsDebugMode { get; private set; } = false;
        
        #if UNITY_EDITOR
        [SerializeField] private GameObject user;
        #endif
        
        private void OnDrawGizmos()
        {
            if (!IsDebugMode) return;
            Vector3 startPosition = user.transform.position - user.transform.forward * (CasterInterpolation * 2f);
            switch (CastType)
            {
                case CastType.Ray:
                    Gizmos.color = Color.green;
                    Gizmos.DrawRay(startPosition,user.transform.forward * CastingRange);
                    break;
                case CastType.Sphere:
                    Gizmos.color = Color.green;
                    Gizmos.DrawWireSphere(startPosition, CasterRadius);
                    Gizmos.color = Color.red;
                    Gizmos.DrawWireSphere(startPosition + user.transform.forward * CastingRange, CasterRadius);
                    break;
                case CastType.Box:
                    Gizmos.color = Color.green;
                    Gizmos.DrawWireCube(startPosition, BoxSize);
                    Gizmos.color = Color.red;
                    Gizmos.DrawWireCube(startPosition + user.transform.forward * CasterRadius, BoxSize);
                    break;
            }
        }
    }
}