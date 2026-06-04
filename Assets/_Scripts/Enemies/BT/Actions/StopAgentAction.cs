using System;
using _Scripts.Enemies;
using Unity.Behavior;
using Unity.Properties;
using UnityEngine;
using Action = Unity.Behavior.Action;

namespace Enemies.BT.Actions
{
    [Serializable, GeneratePropertyBag]
    [NodeDescription(name: "StopAgent", story: "Stop [Enemy]", category: "Action/Navigation", id: "03ebf02a3d03e440a55e7bb861d1cd3f")]
    public partial class StopAgentAction : Action
    {
        [SerializeReference] public BlackboardVariable<AbstractEnemy> Enemy;

        protected override Status OnStart()
        {
            if (Enemy.Value == null || Enemy.Value.NavMovement == null)
                return Status.Failure;
            
            Enemy.Value.NavMovement.StopImmediately();
            return Status.Success;
        }
    }
}

