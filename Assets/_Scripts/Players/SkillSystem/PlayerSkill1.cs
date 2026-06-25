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
    public class PlayerSkill1 : AbstractPlayerSkill
    {
        [SerializeField] private PoolItemSO slashVfxItem;
        [SerializeField] private PoolItemSO impactVfxItem;
        
        [SerializeField] private AnimParamSO animParam;
        
        private AbstractDamageCaster _damageCaster;
        private CharacterMovementManager _movementManager;
        private PlayerTrigger _agentTrigger;

        public override void InitializeSkill(ISkillModule skillModule)
        {
            base.InitializeSkill(skillModule);
            _movementManager = skillModule.Owner.GetModule<CharacterMovementManager>();
            _damageCaster = GetComponentInChildren<AbstractDamageCaster>();
            _damageCaster.InitCaster(skillModule.Owner);
            _agentTrigger = skillModule.Owner.GetModule<PlayerTrigger>();
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
            
            _movement.CanManualMove = false;//자동 조작 모드로 변경
            while (IsUsing)
            {
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