using _Scripts.Agents;
using _Scripts.CombatSystem;
using Agents;
using CombatSystem;
using GGMLib.AnimationSystem;
using UnityEngine;

namespace Enemies.EnemySkills
{
    public class CommonMeleeSkill : AbstractEnemySkill
    {
        [SerializeField] private AnimParamSO skillAnimParam;
        [SerializeField] private float crossFadeDuration = 0.15f;

        private AgentTrigger _trigger;

        public override void InitializeSkill(ISkillModule skillModule)
        {
            base.InitializeSkill(skillModule);
            _trigger = _ownerEnemy.GetComponentInChildren<AgentTrigger>();
            Debug.Assert(_trigger != null, $"애니메이션 트리거가 있어야 근접 공격 스킬이 정상적으로 작동합니다. : {gameObject}");
        }

        public override bool CanUseSkill(GameObject target = null)
        {
            if(target == null) return false;

            Vector3 distanceToTarget = target.transform.position - _ownerEnemy.transform.position;
            distanceToTarget.y = 0;
            
            //사거리내에 있고 쿨다운이 없다면 사용가능하다고 판단한다.
            return NormalizedCooldown >= 1f && distanceToTarget.magnitude <= SkillData.skillRange;
        }

        public override void UseSkill(GameObject target = null)
        {
            base.UseSkill(target);
            
            _renderer.PlayClip(skillAnimParam.ParamHash, 0, crossFadeDuration);
            _trigger.OnAnimationEnd += StopSkill;
        }

        public override void StopSkill()
        {
            base.StopSkill();
            _trigger.OnAnimationEnd -= StopSkill;
        }
    }
}