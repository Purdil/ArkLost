using GGMLib.ModuleSystem;
using UnityEngine;

namespace CombatSystem
{
    public struct DamageData
    {
        public float DamageAmount;
        public Vector3 HitPoint;
        public Vector3 HitNormal;
        public ModuleOwner Attacker;
        public bool IsCritical;

        public DamageData(float damageAmount, Vector3 hitPoint, Vector3 hitNormal, ModuleOwner attacker,
            bool isCritical)
        {
            DamageAmount = damageAmount;
            HitPoint = hitPoint;
            HitNormal =  hitNormal;
            Attacker = attacker;
            IsCritical = isCritical;
        }
    }
}