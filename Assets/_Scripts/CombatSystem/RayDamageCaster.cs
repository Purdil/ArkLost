using CombatSystem;
using UnityEngine;

namespace _Scripts.CombatSystem
{
    public enum CastType {Ray, Sphere, Box}
    public class RayDamageCaster : AbstractDamageCaster
    {

        [SerializeField] private CastType castType = CastType.Sphere;
        [SerializeField] private Vector3 boxSize = Vector3.one;
        [SerializeField, Range(0.5f,20f)] private float casterRadius = 1;
        [SerializeField, Range(0f, 10f)] private float casterInterpolation = 0.5f; //보간, 뒤로 빼는 정도
        [SerializeField, Range(0f, 20f)] private float castingRange = 1f;
        [SerializeField] private bool isDebugMode = false;
        
        public override bool CastDamage(Vector3 position, Vector3 direction, SkillDataSO skillData)
        {
            //뒤로 보간만큼 빼준다.
            Vector3 startPosition = position - (direction * casterInterpolation * 2f);

            RaycastHit hit = default;
            bool isHit = castType switch
            {
                CastType.Ray => Physics.Raycast(startPosition, direction, out hit, castingRange, whatIsEnemy),
                CastType.Sphere => Physics.SphereCast(startPosition, casterRadius, direction,
                    out hit, castingRange, whatIsEnemy),
                CastType.Box => Physics.BoxCast(startPosition, boxSize * 0.5f, direction,
                    out hit,transform.rotation, castingRange, whatIsEnemy),
                _ => false
            };

            if (isHit && hit.collider != null && hit.collider.TryGetComponent(out IDamageable damageable))
            {
                float damageAmount = skillData.damage;
                bool isCritical = false;
                
                LastHitPosition = hit.point;
                LastHitNormal = hit.normal;
                LastHitIsCritical = isCritical;
                
                damageable.ApplyDamage(new DamageData
                {
                    DamageAmount = damageAmount * skillData.damageMultiplier,
                    Attacker = CasterOwner,
                    HitPoint = LastHitPosition,
                    HitNormal = LastHitNormal,
                    IsCritical = LastHitIsCritical,
                });
            }
            return isHit;
        }

        private void OnDrawGizmos()
        {
            if (!isDebugMode) return;
            Vector3 startPosition = transform.position - transform.forward * (casterInterpolation * 2f);
            switch (castType)
            {
                case CastType.Ray:
                    Gizmos.color = Color.green;
                    Gizmos.DrawRay(startPosition,transform.forward * castingRange);
                    break;
                case CastType.Sphere:
                    Gizmos.color = Color.green;
                    Gizmos.DrawWireSphere(startPosition, casterRadius);
                    Gizmos.color = Color.red;
                    Gizmos.DrawWireSphere(startPosition + transform.forward * castingRange, casterRadius);
                    break;
                case CastType.Box:
                {
                    Matrix4x4 oldMatrix = Gizmos.matrix;
                    
                    Gizmos.color = Color.green;
                    Gizmos.matrix = Matrix4x4.TRS(
                        startPosition,
                        transform.rotation,
                        Vector3.one
                    );
                    Gizmos.DrawWireCube(Vector3.zero, boxSize);
                    Gizmos.color = Color.red;
                    Gizmos.matrix = Matrix4x4.TRS(
                        startPosition + transform.forward * castingRange,
                        transform.rotation,
                        Vector3.one
                    );
                    Gizmos.DrawWireCube(Vector3.zero, boxSize);

                    Gizmos.matrix = oldMatrix;
                    break;
                }
            }
        }
    }
}
