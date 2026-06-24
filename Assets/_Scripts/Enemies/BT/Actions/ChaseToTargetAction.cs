using System;
using _Scripts.Enemies;
using Unity.Behavior;
using Unity.Properties;
using UnityEngine;
using Action = Unity.Behavior.Action;

namespace Enemies.BT.Actions
{
    [Serializable, GeneratePropertyBag]
    [NodeDescription(name: "ChaseToTarget", story: "[Enemy] chase to [TargetGameObject]", category: "Action/Navigation", id: "7a06d0f6d4f262ab0e73901909c8b93c")]
    public partial class ChaseToTargetAction : Action
    {
        [SerializeReference] public BlackboardVariable<AbstractEnemy> Enemy;
        [SerializeReference] public BlackboardVariable<GameObject> TargetGameObject;
        
        private Vector3 _destination;
        private INavMovement _navMovement;

        protected override Status OnStart()
        {
            if(Enemy.Value == null || TargetGameObject.Value == null || Enemy.Value.NavMovement == null)
                return Status.Failure;
            
            _destination = TargetGameObject.Value.transform.position;
            _navMovement = Enemy.Value.NavMovement;
            _navMovement.SetDestination(_destination);
            return Status.Running;
        }

        protected override Status OnUpdate()
        {
            if (TargetGameObject.Value == null)
                return Status.Failure;
            
            Vector3 newDestination = TargetGameObject.Value.transform.position;
            float deltaDistance = Vector3.Distance(_destination, newDestination);
            if (deltaDistance > 1f)
            {
                _destination = newDestination;
                _navMovement.SetDestination(_destination);
            }
            
            if(_navMovement.IsArrived)
                return Status.Success;
            
            return Status.Running;
        }
    }
}

