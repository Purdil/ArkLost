using System;
using _Scripts.CombatSystem;
using CombatSystem;
using GGMLib.ModuleSystem;
using UnityEngine;
using UnityEngine.Events;

namespace Agents
{
    public abstract class Agent : ModuleOwner, IDamageable
    {
        public bool IsDead { get; set; }

        public UnityEvent OnHit;
        public UnityEvent OnDeath;
        
        public HealthModule Health { get; private set; }
        public ActionDataModule ActionData { get; private set; }

        protected override void InitializeComponents()
        {
            base.InitializeComponents();
            Health = GetModule<HealthModule>();
            ActionData = GetModule<ActionDataModule>();
        }

        protected override void AfterInitComponents()
        {
            base.AfterInitComponents();
            Health.OnDeath += HandleDeath;
            OnHit.AddListener(HandleHitEvent);
        }


        protected virtual void OnDestroy()
        {
            Health.OnDeath -= HandleDeath;
        }
        protected virtual void HandleHitEvent(){}

        protected virtual void HandleDeath()
        {
            IsDead = true;
            OnDeath?.Invoke();
        }

        public void ApplyDamage(DamageData damageData)
        {
            if (IsDead) return;
            if (ActionData != null)
            {
                ActionData.HitPoint = damageData.HitPoint;
                ActionData.HitNormal = damageData.HitNormal;
                ActionData.Attacker = damageData.Attacker;
            }
          
            OnHit?.Invoke();
            
            if (Health != null)
            {
                Health.ApplyDamage(damageData.DamageAmount);
            }

        }
    }
}