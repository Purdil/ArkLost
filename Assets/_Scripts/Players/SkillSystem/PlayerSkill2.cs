using System.Collections;
using _Scripts.Agents;
using _Scripts.CombatSystem;
using CombatSystem;
using CoreSystem.Events;
using Enemies;
using GGMLib.AnimationSystem;
using GGMLib.ObjectPool.Runtime;
using UnityEngine;

namespace _Scripts.Players.SkillSystem
{
    //카운터 스킬 예정
    public class PlayerSkill2 : AbstractPlayerSkill
    {
        [SerializeField] private PoolItemSO slashVfxItem;
        [SerializeField] private PoolItemSO impactVfxItem;
        
        [SerializeField] private AnimParamSO animParam;
        
        [SerializeField] private float comboDuration;
        [SerializeField] private AnimationCurve comboCurve;
        
        private AbstractDamageCaster _damageCaster;
        private CharacterMovementManager _movementManager;
        private AgentTrigger _agentTrigger;
        public override void InitializeSkill(ISkillModule skillModule)
        {
            base.InitializeSkill(skillModule);
            _movementManager = skillModule.Owner.GetModule<CharacterMovementManager>();
            _damageCaster = GetComponentInChildren<AbstractDamageCaster>();
            _damageCaster.InitCaster(skillModule.Owner);
            _agentTrigger = skillModule.Owner.GetModule<AgentTrigger>();
            _lastUsingTime = 0;
        }

        public override bool CanUseSkill(GameObject target = null)
        {
            bool cooldownReady = NormalizedCooldown >= 1f;
            bool notUsing = !IsUsing;
            bool canUse = cooldownReady && notUsing;
            
            return canUse;
        }

        public override void UseSkill(GameObject target = null)
        {
            base.UseSkill(target);
            _movementManager.SwitchMode(CharacterMovementManager.MoveMode.CharacterController);
            _renderer.PlayClip(animParam.ParamHash, 0, 0);

            Vector3 mousePosition = _player.PlayerInput.GetWorldMousePosition();
            mousePosition.y = _player.transform.position.y;
            Vector3 direction = (mousePosition - _player.transform.position).normalized;
            
            _movement.RotateTo(direction);
            StartCoroutine(SkillCoroutine());
        }

        public IEnumerator SkillCoroutine()
        {
            _agentTrigger.OnAnimationEnd += HandleAnimationEnd;
            _agentTrigger.OnDamageCast += HandleDamageCast;
            float currentDuration = 0;
            Vector3 forward = _player.transform.forward;
            _movement.CanManualMove = false;//자동 조작 모드로 변경
            while (IsUsing)
            {
                float percent = currentDuration / comboDuration; //0~1 값으로 값을 정규화해준다.
                currentDuration += Time.deltaTime;
                float force = comboCurve.Evaluate(percent);
                _movement.SetMovementVelocity(forward * force);
                yield return null;
            }

            _movement.CanManualMove = true; //수동 조작모드로 변경.
            _movement.SetMovementVelocity(Vector3.zero);
            _agentTrigger.OnAnimationEnd -= HandleAnimationEnd;
            _agentTrigger.OnDamageCast -= HandleDamageCast;
        }

        private void HandleDamageCast()
        {
            Vector3 position = _damageCaster.transform.position;
            bool isHit = _damageCaster.CastDamage(position, transform.forward, SkillData);
            if (isHit)
            {
                var evt = CreateEvents.ShowPoolingVfx.InitData(
                    slashVfxItem, _damageCaster.LastHitPosition, Quaternion.identity);
                _skillModule.CreateChannel.RaiseEvent(evt);
                
                evt = CreateEvents.ShowPoolingVfx.InitData(
                    impactVfxItem, _damageCaster.LastHitPosition, Quaternion.identity);
                _skillModule.CreateChannel.RaiseEvent(evt);
            }
        }

        private void HandleAnimationEnd() => StopSkill();

        public override void StopSkill()
        {
            _movementManager.SwitchMode(CharacterMovementManager.MoveMode.NavMesh);
            base.StopSkill();
        }
    }
}