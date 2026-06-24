using System;
using GGMLib.ModuleSystem;
using UnityEngine;
using UnityEngine.Events;

namespace _Scripts.CombatSystem
{
    public class HealthModule : MonoBehaviour, IModule
    {
        [Serializable]
        public sealed class HealthValueChangedEvent : UnityEvent<float, float>
        {
        }

        public event Action OnDeath;
        public event Action<float, float> OnHealthChanged;

        [SerializeField] private float maxHealth; // Later this should come from stats.
        [SerializeField] private float currentHealth; // Serialized for debugging.
        [SerializeField] private HealthValueChangedEvent onHealthChangedUnity = new HealthValueChangedEvent();

        private ModuleOwner _owner;

        public float MaxHealth => maxHealth;
        public float CurrentHealth => currentHealth;
        public float NormalizedHealth => maxHealth > 0f ? Mathf.Clamp01(currentHealth / maxHealth) : 0f;
        public HealthValueChangedEvent OnHealthChangedUnity => onHealthChangedUnity;

        public void Initialize(ModuleOwner owner)
        {
            _owner = owner;
            currentHealth = maxHealth;
            NotifyHealthChanged();
        }

        public void ApplyDamage(float damageAmount)
        {
            if (damageAmount <= 0f)
            {
                ApplyHeal(-damageAmount);
                return;
            }

            var wasAlive = currentHealth > 0f;
            SetCurrentHealth(currentHealth - damageAmount);

            if (wasAlive && currentHealth <= 0f)
                OnDeath?.Invoke();
        }

        public void ApplyHeal(float healAmount)
        {
            if (healAmount <= 0f)
                return;

            SetCurrentHealth(currentHealth + healAmount);
        }

        public void SetHealth(float value)
        {
            SetCurrentHealth(value);
        }

        private void SetCurrentHealth(float value)
        {
            currentHealth = Mathf.Clamp(value, 0f, Mathf.Max(0f, maxHealth));
            NotifyHealthChanged();
        }

        private void NotifyHealthChanged()
        {
            OnHealthChanged?.Invoke(currentHealth, maxHealth);
            onHealthChangedUnity?.Invoke(currentHealth, maxHealth);
        }
    }
}
