using System;
using GGMLib.ModuleSystem;
using UnityEngine;

namespace _Scripts.Agents
{
    public class AgentTrigger : MonoBehaviour, IModule 
    {
        public event Action OnAnimationEnd;
        public event Action OnLinkTimeEnd;
        public bool CanManualMovement { get; set; }
        public event Action OnDamageCast;
        public void Initialize(ModuleOwner owner)
        {
          
        }

        private void AnimationEndTrigger()
        {
            CanManualMovement = false;
            OnAnimationEnd?.Invoke();
        }
        
        private void DamageCastTrigger() => OnDamageCast?.Invoke();
        private void LinkTimeTrigger() => OnLinkTimeEnd?.Invoke();
        private void CanManualMovementTrigger() => CanManualMovement = true;
    }
}