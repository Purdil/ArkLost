using System;
using _Scripts.Enemies;
using Unity.Behavior;
using UnityEngine;

namespace Enemies.BT.Condition
{
    [Serializable, Unity.Properties.GeneratePropertyBag]
    [Condition(name: "TargetInStopDistance", story: "[Enemy] check [TargetGameObject] in stopDistance", category: "Conditions", id: "dbe766fcaacc292f469ada25f48dcd66")]
    public partial class TargetInStopDistanceCondition : Unity.Behavior.Condition
    {
        [SerializeReference] public BlackboardVariable<AbstractEnemy> Enemy;
        [SerializeReference] public BlackboardVariable<GameObject> TargetGameObject;

        public override bool IsTrue()
        {
            if (Enemy.Value == null || TargetGameObject.Value == null)
            {
                Debug.LogError("condition에 Enemy 또는 TargetGameObject가 할당되지 않았습니다. 항상 false반환");
                return false;
            }

            float stopDistance = Enemy.Value.StopDistance;
            float targetDistance = Vector3.Distance(TargetGameObject.Value.transform.position,Enemy.Value.transform.position);
            
            return targetDistance <= stopDistance;
        }
    }
}
