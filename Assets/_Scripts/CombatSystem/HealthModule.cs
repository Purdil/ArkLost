using System;
using GGMLib.ModuleSystem;
using UnityEngine;

namespace CombatSystem
{
    public class HealthModule : MonoBehaviour, IModule
    {
        public event Action OnDeath;
        [SerializeField] private float maxHealth; //나중에 스탯으로 변경된다.
        [SerializeField] private float currentHealth; // 디버그 용도로 직렬화 함
        
        private ModuleOwner _owner;
        public void Initialize(ModuleOwner owner)
        {
            _owner = owner;
            currentHealth = maxHealth;
        }

        public void ApplyDamage(float damageAmount)
        {
            currentHealth -= damageAmount;
            if (currentHealth <= 0)
            {
                currentHealth = 0;
                OnDeath?.Invoke();
            }
        }
        
    }
}