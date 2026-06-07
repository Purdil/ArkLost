using System;
using System.Collections.Generic;
using System.Linq;
using _Scripts.Enemies;
using CombatSystem;
using Enemies;
using GGMLib.EventChannelSystem;
using GGMLib.ModuleSystem;
using Players.FSM;
using UnityEngine;

namespace _Scripts.Players
{
    public class PlayerSkillModule : MonoBehaviour, ISkillModule, IModule
    {
        [field: SerializeField] public GameEventChannelSO CreateChannel {get; private set;}
        public ModuleOwner Owner { get; private set; }
        public PlayerController Player { get; private set; }
        private INavMovement _navMovement;
        private INavAgentRenderer _navAgentRenderer;
        public event Action OnCurrentSkillEnd;

        private Dictionary<int, ISkill> _skillDict;
        private ISkill _currentSkill;
        
        public void Initialize(ModuleOwner owner)
        {
            Owner = owner;
            Player = Owner as PlayerController;
            Debug.Assert(Player != null, "플레이어 스킬 모듈은 플레이어의 자식으로 있어야 합니다");
            _navMovement = owner.GetModule<INavMovement>();
            _navAgentRenderer = owner.GetModule<INavAgentRenderer>();
            Debug.Assert(_navAgentRenderer != null, "PlayerSkillModule requires INavAgentRenderer.");

            _skillDict = GetComponentsInChildren<ISkill>()
                .ToDictionary(skill => skill.SkillData.skillIndex);

            foreach (ISkill skill in _skillDict.Values)
            {
                skill.InitializeSkill(this); //스킬들을 초기화
            }

            Player.PlayerInput.OnAttackKeyPressed += HandleAttackKeyPress; //기본공격은 이걸로 바인딩
            Player.PlayerInput.OnDashKeyPressed += HandleDashKeyPress;
        }

        private void OnDestroy()
        {
            if (Player != null && Player.PlayerInput != null)
            {
                Player.PlayerInput.OnAttackKeyPressed -= HandleAttackKeyPress; //기본공격은 이걸로 바인딩
                Player.PlayerInput.OnDashKeyPressed -= HandleDashKeyPress;                
            }
        }

        private void HandleDashKeyPress()
        {
            if (CanUseSkill(1))
            {
                Player.ChangeState(PlayerState.SKILL, 0);
                UseSkill(1);
            }
        }

        private void HandleAttackKeyPress()
        {
            if (CanUseSkill(0))
            {
                _navAgentRenderer.BeginManualControl();
                //스킬상태 애니메이션 제어는 각 스킬 콤포넌트가 할거라서 duration 0으로 보냈다.
                _navMovement.StopImmediately();
                Player.ChangeState(PlayerState.SKILL, 0); 
                
                UseSkill(0);
            }
        }

        public bool CanUseSkill(int skillIndex, GameObject target = null)
        {
            Debug.Log($"InputTime : {Time.time}");
            if (_currentSkill is { IsUsing: true })
            {
                Debug.Log($"Is Using : {Time.time}");
                return false;
            }
            
            Debug.Log($"Reach skill.CanUseSkill : {Time.time}");

            if (_skillDict.TryGetValue(skillIndex, out ISkill skill))
            {
                return skill.CanUseSkill(target);
            }

            return false;
        }

        public void UseSkill(int skillIndex, GameObject target = null)
        {
            if (_skillDict.TryGetValue(skillIndex, out ISkill skill))
            {
                //아직은 기존스킬 캔슬하고 쏘는건 안만든다.
                if (_currentSkill != null)
                    _currentSkill.OnSkillEnd -= HandleCurrentSkillEnd;
                _currentSkill = skill;
                _currentSkill.OnSkillEnd += HandleCurrentSkillEnd;
                //이 시점에서도 위치가 튐.
                skill.UseSkill(target);
            }
        }

        private void HandleCurrentSkillEnd()
        {
            _currentSkill.OnSkillEnd -= HandleCurrentSkillEnd;
            InvokeSkillEnd();
            _currentSkill = null;
        }

        public void InvokeSkillEnd() => OnCurrentSkillEnd?.Invoke();
        public void StopSkillIfNotFinished()
        {
            if (_currentSkill != null)
            {
                _currentSkill.StopSkill();
                _currentSkill = null;
            }
        }
    }
}
