using System;
using GGMLib.ModuleSystem;
using UnityEngine;

namespace _Scripts.Agents.FSM.AgentTriggers
{
    public class AgentTrigger : MonoBehaviour, IModule
    {
        public event Action OnAnimationEnd;
        public bool CanManualMovement { get; set; }
        public void Initialize(ModuleOwner owner)
        {
          
        }

        private void AnimationEndTrigger()
        {
            CanManualMovement = false;
            OnAnimationEnd?.Invoke();
        }
        private void CanManualMovementTrigger() => CanManualMovement = true;
    }
}