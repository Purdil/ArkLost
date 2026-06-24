using System;
using _Scripts.CombatSystem;
using _Scripts.Enemies;
using CombatSystem;
using Unity.Behavior;
using Unity.Properties;
using UnityEngine;
using Action = Unity.Behavior.Action;

namespace Enemies.BT.Actions
{
    [Serializable, GeneratePropertyBag]
    [NodeDescription(name: "UseSkill", story: "[Enemy] use [SkillNumber] to [TargetGameObject]", category: "Action/Combat", id: "a69b392dd8b2b649f09cecc33f6a9ad5")]
    public partial class UseSkillAction : Action
    {
        [SerializeReference] public BlackboardVariable<AbstractEnemy> Enemy;
        [SerializeReference] public BlackboardVariable<int> SkillNumber;
        [SerializeReference] public BlackboardVariable<GameObject> TargetGameObject;

        private ISkillModule _skillModule;
        private bool _isSkillEnd;
        protected override Status OnStart()
        {
            if (Enemy.Value == null || SkillNumber.Value < 0 || Enemy.Value.SkillModule == null)
            {
                Debug.LogError("use skill의 기본 값이 설정되지 않았습니다.");
                return Status.Failure;
            }
            
            _skillModule = Enemy.Value.SkillModule;
            
            _isSkillEnd = false;
            _skillModule.OnCurrentSkillEnd += HandleSkillEnd;
            _skillModule.UseSkill(SkillNumber.Value, TargetGameObject.Value);
            return Status.Running;
        }

        private void HandleSkillEnd()
        {
            _skillModule.OnCurrentSkillEnd -= HandleSkillEnd;
            _isSkillEnd = true;
            
        }

        protected override Status OnUpdate()
        {
            return _isSkillEnd ? Status.Success :  Status.Running; 
        }

        protected override void OnEnd()
        {
            if (_skillModule != null)
            {
                _skillModule.OnCurrentSkillEnd -= HandleSkillEnd;
                _skillModule.StopSkillIfNotFinished(); // 공격으로 썼던것드를 모두 CleanUp해라.
            }
        }
    }
}

