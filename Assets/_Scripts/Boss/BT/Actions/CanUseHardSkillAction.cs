using System;
using _Scripts.Boss.BossSkillSystem;
using _Scripts.Enemies;
using Unity.Behavior;
using Unity.Properties;
using UnityEngine;
using Action = Unity.Behavior.Action;

namespace _Scripts.Boss.BT.Actions
{
    [Serializable, GeneratePropertyBag]
    [NodeDescription(name: "CanUseHardSkill", story: "[Phase] [Boss] can use to [TargetObject] select [Index]", category: "Action/Conditional", id: "15d176f1a87f54e986e7223cd4d4aefd")]
    public partial class CanUseHardSkillAction : Action
    {
        [SerializeReference] public BlackboardVariable<int> Phase;
        [SerializeReference] public BlackboardVariable<AbstractBoss> Boss;
        [SerializeReference] public BlackboardVariable<GameObject> TargetObject;
        [SerializeReference] public BlackboardVariable<int> Index;
       
        private BossSkillSelector _skillSelector;
        
        protected override Status OnStart()
        {
            if(Boss.Value == null || TargetObject.Value == null || Phase <= 0)
                return Status.Failure;
            
            _skillSelector ??= Boss.Value.GetModule<BossSkillSelector>();
           
            Debug.Assert(_skillSelector != null, "보스에게 스킬 선택 모듈이 없습니다.");
          

            _skillSelector.TrySelectSkill(TargetObject.Value,SkillType.HARD, out int value);
            
            Index.Value = value;
            if(value < 0)
                return Status.Failure;
            
            return Status.Success;
        }

        
    }
}

