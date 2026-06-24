using Enemies;
using System;
using _Scripts.Enemies;
using CombatSystem;
using Enemies.BT.Actions;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "ProcessHit", story: "[Enemy] process hit from [TargetGameObject]", category: "Action/Combat", id: "0537259329db217aafc596a3943f0bdb")]
public partial class ProcessHitAction : Action
{
    [SerializeReference] public BlackboardVariable<AbstractEnemy> Enemy;
    [SerializeReference] public BlackboardVariable<GameObject> TargetGameObject;

    protected override Status OnStart()
    {
        if (Enemy.Value == null || Enemy.Value.ActionData == null) 
            return Status.Failure;

        ActionDataModule actionData = Enemy.Value.ActionData;
        TargetGameObject.Value = actionData.Attacker.gameObject;

        RotateToTarget();
        return Status.Success;
    }

    private void RotateToTarget()
    {
        Vector3 direction = (TargetGameObject.Value.transform.position - Enemy.Value.transform.position);
        direction.y = 0;
        Enemy.Value.transform.rotation = Quaternion.LookRotation(direction.normalized);
    }

  
}

