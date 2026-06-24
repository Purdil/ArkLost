using System;
using _Scripts.CombatSystem;
using CombatSystem;
using UnityEngine;

namespace _Scripts.Players.SkillSystem
{
    public class Skill3Effect : MonoBehaviour
    {
        [SerializeField] private SkillDataSO skillData;
        [SerializeField] private AbstractDamageCaster damageCaster;

        private void OnParticleCollision(GameObject other)
        {
            damageCaster.CastDamage(transform.position, transform.forward, skillData);
        }

        private void Awake()
        {
            damageCaster.InitCaster(null);
        }
    }
}