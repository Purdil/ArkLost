using System;
using _Scripts.Enemies;
using Enemies.Nav;
using Unity.Behavior;
using Unity.Properties;
using UnityEngine;
using Action = Unity.Behavior.Action;

namespace Enemies.BT.Actions
{
    [Serializable, GeneratePropertyBag]
    [NodeDescription(name: "MoveToNextPoint", story: "[Enemy] move to Point", category: "Action/Navigation", id: "07f8666076a548ce2feb1a65e4843ab5")]
    public partial class MoveToNextPointAction : Action
    {
        [SerializeReference] public BlackboardVariable<AbstractEnemy> Enemy;

        private INavMovement _navMovement;
        
        protected override Status OnStart()
        {
            if (Enemy.Value == null || Enemy.Value.NavMovement == null || Enemy.Value.WayPoints == null)
                return Status.Failure;
            
            _navMovement = Enemy.Value.NavMovement;
            WayPointManager wayPointManager = Enemy.Value.WayPoints;
            
            //현 위치에서 가장 가까운 웨이포인트를 찾아서.
            int index = Enemy.Value.CurrentWayPointIndex;
            
            index = index < 0 ?
                wayPointManager.GetClosestPointIndexFromPosition(Enemy.Value.transform.position)
                    : wayPointManager.GetNextWayPointIndex(index);
            
            if (index < 0)
                return Status.Failure;
            
            Enemy.Value.CurrentWayPointIndex = index; //갱신
            
            WayPoint targetPoint = wayPointManager[index]; //인덱서로 접근 가능
            _navMovement.SetDestination(targetPoint.Position); //이동명령 
            return Status.Running;
        }

        protected override Status OnUpdate()
        {
            return _navMovement.IsArrived ? Status.Success : Status.Running;
        }
    }
}

