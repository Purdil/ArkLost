using System;
using _Scripts.Agents;
using _Scripts.Agents.AgentTriggers;
using _Scripts.Enemies;
using Agents;
using Unity.Behavior;
using Unity.Properties;
using UnityEngine;
using Action = Unity.Behavior.Action;

namespace Enemies.BT.Actions
{
    [Serializable, GeneratePropertyBag]
    [NodeDescription(name: "WaitForAnimation", story: "[Enemy] wait for animation", category: "Action/Animation", id: "b84fd59e9e29c3d9a9c959beb3d3a01c")]
    public partial class WaitForAnimationAction : Action
    {
        [SerializeReference] public BlackboardVariable<AbstractEnemy> Enemy;

        private AgentTrigger _agentTrigger;
        private bool _isAnimationEnd;
        
        protected override Status OnStart()
        {
            if (Enemy.Value == null || Enemy.Value.Trigger == null)
                return Status.Failure;

            _isAnimationEnd = false;
            _agentTrigger = Enemy.Value.Trigger;
            _agentTrigger.OnAnimationEnd += HandleAnimationEnd;
            return Status.Running;
        }

      

        protected override Status OnUpdate()
        {
            return _isAnimationEnd ? Status.Success : Status.Running;
        }

        protected override void OnEnd()
        {
            if(_agentTrigger != null)
                _agentTrigger.OnAnimationEnd -= HandleAnimationEnd;
        }
        private void HandleAnimationEnd() => _isAnimationEnd = true;
    }
}

