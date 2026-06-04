using System;
using _Scripts.Enemies;
using Unity.Behavior;
using UnityEngine;

namespace Enemies.BT.Condition
{
    [Serializable, Unity.Properties.GeneratePropertyBag]
    [Condition(name: "CanUseSkill", story: "[Enemy] can use [SkillNumber] To [TargetGameObject]", category: "Conditions", id: "db5c6202e7220d30703b1c9121d23688")]
    public partial class CanUseSkillCondition : Unity.Behavior.Condition
    {
        [SerializeReference] public BlackboardVariable<AbstractEnemy> Enemy;
        [SerializeReference] public BlackboardVariable<int> SkillNumber;
        [SerializeReference] public BlackboardVariable<GameObject> TargetGameObject;

        public override bool IsTrue()
        {
            if (Enemy.Value == null || SkillNumber.Value < 0 || TargetGameObject.Value == null)
            {
                Debug.LogError("Can use skill 의 컨디션 조건이 잘못되었습니다.");
                return false;
            }
            return Enemy.Value.SkillModule.CanUseSkill(SkillNumber.Value, TargetGameObject.Value);
        }

        public override void OnStart()
        {
        }

        public override void OnEnd()
        {
        }
    }
}
