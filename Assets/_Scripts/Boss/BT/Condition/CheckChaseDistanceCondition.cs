using System;
using Unity.Behavior;
using UnityEngine;

namespace _Scripts.Boss.BT.Condition
{
    [Serializable, Unity.Properties.GeneratePropertyBag]
    [Condition(name: "CheckChaseDistance", story: "[Boss] to [TargetObject] out [ChaseDistance]", category: "Conditions", id: "80c24cf15db07ec5b9a45e0e15f4c38a")]
    public partial class CheckChaseDistanceCondition : Unity.Behavior.Condition
    {
        [SerializeReference] public BlackboardVariable<GameObject> Boss;
        [SerializeReference] public BlackboardVariable<GameObject> TargetObject;
        [SerializeReference] public BlackboardVariable<float> ChaseDistance;

        public override bool IsTrue()
        {
            if (Boss.Value == null || TargetObject.Value == null || ChaseDistance.Value <= 0)
            {
                return false;
            }
            
            float distance = Vector3.Distance(Boss.Value.transform.position, TargetObject.Value.transform.position);
            Debug.Log($"ChaseDistance: {ChaseDistance.Value} meters: {distance} meters");
            return distance >= ChaseDistance.Value;
        }

     
    }
}
