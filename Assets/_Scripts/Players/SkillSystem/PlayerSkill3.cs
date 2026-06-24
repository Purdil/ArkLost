using _Scripts.Agents;
using _Scripts.CombatSystem;
using CoreSystem.Events;
using GGMLib.AnimationSystem;
using GGMLib.ObjectPool.Runtime;
using UnityEngine;

namespace _Scripts.Players.SkillSystem
{
    public class PlayerSkill3 : AbstractPlayerSkill
    {
        [SerializeField] private AnimParamSO animParam;
        [SerializeField] private PoolItemSO slashEffect;

        private AgentTrigger _agentTrigger;
        private CharacterMovementManager _characterMovementManager;

        public override void InitializeSkill(ISkillModule skillModule)
        {
            base.InitializeSkill(skillModule);
            _characterMovementManager = skillModule.Owner.GetModule<CharacterMovementManager>();
            _agentTrigger = skillModule.Owner.GetModule<AgentTrigger>();
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
            _characterMovementManager.SwitchMode(CharacterMovementManager.MoveMode.CharacterController);
            _renderer.PlayClip(animParam.ParamHash,0,0);
            Vector3 mousePosition = _player.PlayerInput.GetWorldMousePosition();
            mousePosition.y = _player.transform.position.y;
            Vector3 direction = (mousePosition - _player.transform.position).normalized;
            
            _movement.RotateTo(direction);
            _agentTrigger.OnAnimationEnd += HandleAnimationEnd;
            _movement.CanManualMove = false;
        }

        private void HandleAnimationEnd() => StopSkill();

        public override void StopSkill()
        {
            base.StopSkill();
            _agentTrigger.OnAnimationEnd -= HandleAnimationEnd;
            _movement.CanManualMove = true;
            _characterMovementManager.SwitchMode(CharacterMovementManager.MoveMode.NavMesh);
        }
    }
}