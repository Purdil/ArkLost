using System.Collections;
using _Scripts.Players.SkillSystem;
using Agents;
using CombatSystem;
using GGMLib.AnimationSystem;
using UnityEngine;

namespace Players.SkillSystem
{
    public class PlayerRolling : AbstractPlayerSkill
    {
        [SerializeField] private AnimParamSO rollingParam;
        [SerializeField] private AnimationCurve rollingCurve;
        [SerializeField] private float speed = 5f;
        [SerializeField] private float duration = 0.5f;

        private AgentTrigger _trigger;

        public override void InitializeSkill(ISkillModule skillModule)
        {
            base.InitializeSkill(skillModule);
            _trigger = _player.GetModule<AgentTrigger>();
            Debug.Assert(_trigger != null, "롤링 스킬은 애니메이션 트리거가 필요합니다.");
        }

        public override bool CanUseSkill(GameObject target = null)
        {
            return NormalizedCooldown >= 1f && !IsUsing;
        }

        public override void UseSkill(GameObject target = null)
        {
            base.UseSkill(target);
            Vector3 worldPosition = _player.PlayerInput.GetWorldMousePosition();
            worldPosition.y = _player.transform.position.y;
            Vector3 direction = (worldPosition - _player.transform.position).normalized;
            _movement.RotateTo(direction);
            StartCoroutine(RollingCoroutine());
        }

        private IEnumerator RollingCoroutine()
        {
            _renderer.PlayClip(rollingParam.ParamHash, 0, 0.01f);
            _trigger.OnAnimationEnd += HandleAnimationEnd;

            float currentDuration = 0;
            Vector3 forward = _player.transform.forward;
            _movement.CanManualMove = false;

            while (IsUsing)
            {
                float percent = currentDuration / duration;
                currentDuration += Time.deltaTime; 
                float force = rollingCurve.Evaluate(percent) * speed;
                _movement.SetMovementVelocity(forward * force);
                yield return null;
            }
            
            _movement.CanManualMove = true;
            _trigger.OnAnimationEnd -= HandleAnimationEnd;
        }

        private void HandleAnimationEnd()
        {
            StopSkill();
        }
    }
}