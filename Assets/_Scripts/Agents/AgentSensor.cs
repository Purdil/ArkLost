using GGMLib.ModuleSystem;
using UnityEngine;

namespace Agents
{
    public class AgentSensor : MonoBehaviour, IModule
    {
        [SerializeField] private LayerMask whatIsTarget;
        [SerializeField] private LayerMask whatIsObstacle;
        [SerializeField] private int maxColliderCount = 5; //최대 감지 갯수

        private ModuleOwner _owner;
        private Collider[] _colliderResults;
        public Collider[] ColliderResults => _colliderResults;
        
        public void Initialize(ModuleOwner owner)
        {
            _owner = owner;
            Debug.Assert(maxColliderCount > 0, $"Max collider Count는 최소 0보다 커야합니다.");
            _colliderResults = new Collider[maxColliderCount];
        }

        public bool IsTargetInViewAngle(Transform targetTrm, float viewAngle)
        {
            Vector3 direction = targetTrm.position - transform.position;
            direction.y = 0;
            float angle = Vector3.Angle(transform.forward, direction);
            return angle <= viewAngle * 0.5f; //시야 각 안에 존재하는 지 체크하는 함수
        }

        //타겟과 나 사이에 장애물이 있는지 체크하는 함수
        public bool IsTargetIsInSight(Transform targetTrm)
        {
            Vector3 targetPosition = targetTrm.position;
            targetPosition.y = transform.position.y; //위치 동기화

            Vector3 direction = targetPosition - transform.position;
            float distance = direction.magnitude;
            //적 타겟까지 레이를 쏴서 장애물이 있는 지 검사.
            if (Physics.Raycast(transform.position, direction.normalized,
                    out RaycastHit hit, distance, whatIsObstacle))
            {
                return false;
            }

            return true;
        }
        
        public bool IsTargetInViewRadius(Transform targetTrm, float viewRadius)
            => (targetTrm.position - transform.position).sqrMagnitude <= viewRadius * viewRadius;
        
        public int FindTargetsInRadius(float viewRadius)
            => Physics.OverlapSphereNonAlloc(transform.position, viewRadius, _colliderResults, whatIsTarget);
    }
}
