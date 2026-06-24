using _Scripts.CombatSystem;
using GGMLib.ModuleSystem;
using UnityEngine;

namespace CombatSystem
{
    public abstract class AbstractDamageCaster : MonoBehaviour
    {
        [SerializeField] protected LayerMask whatIsEnemy;
        
        public ModuleOwner CasterOwner { get; private set; }
        public Vector3 LastHitPosition { get; protected set; }
        public Vector3 LastHitNormal { get; protected set; }
        public bool LastHitIsCritical { get; protected set; }

        public virtual void InitCaster(ModuleOwner owner)
        {
            CasterOwner = owner;
        }
        
        public abstract bool CastDamage(Vector3 position, Vector3 direction, SkillDataSO skillData);
    }
}