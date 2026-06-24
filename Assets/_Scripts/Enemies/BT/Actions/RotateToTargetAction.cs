using System;
using _Scripts.Enemies;
using Unity.Behavior;
using Unity.Properties;
using UnityEngine;
using Action = Unity.Behavior.Action;

namespace Enemies.BT.Actions
{
    [Serializable, GeneratePropertyBag]
    [NodeDescription(name: "RotateToTarget", story: "[Enemy] rotate to [TargetGameObject]", category: "Action/Animation", id: "a772fe5e14e20083b9494ef5c39bfac4")]
    public partial class RotateToTargetAction : Action
    {
        [SerializeReference] public BlackboardVariable<AbstractEnemy> Enemy;
        [SerializeReference] public BlackboardVariable<GameObject> TargetGameObject;

        [SerializeReference] public BlackboardVariable<float> RotateSpeed = new(10f);
        [SerializeReference] public BlackboardVariable<float> RotateDuration = new(0.4f);

        private float _startTime;
        
        protected override Status OnStart()
        {
            if (Enemy.Value == null || TargetGameObject.Value == null)
                return Status.Failure;
            
            _startTime = Time.time;
            return Status.Running;
        }

        protected override Status OnUpdate()
        {
            if (_startTime + RotateDuration.Value < Time.time)
                return Status.Success;

            Vector3 direction = (TargetGameObject.Value.transform.position - Enemy.Value.transform.position);
            direction.y = 0;
            Quaternion targetRotation = Quaternion.LookRotation(direction.normalized);
            Enemy.Value.transform.rotation
                = Quaternion.Lerp(Enemy.Value.transform.rotation,
                    targetRotation, RotateSpeed.Value * Time.deltaTime);
            
            return Status.Running;
        }
    }
}

