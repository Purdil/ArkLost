using System;
using System.Collections.Generic;
using System.Linq;
using _Scripts.Agents;
using _Scripts.CombatSystem;
using _Scripts.Enemies;
using _Scripts.Players.FSM;
using CoreSystem.Events;
using Enemies;
using GGMLib.EventChannelSystem;
using GGMLib.ModuleSystem;
using Players.FSM;
using UnityEngine;

namespace _Scripts.Players
{
    public class PlayerSkillModule : MonoBehaviour, ISkillModule, IModule, IAfterInitModule
    {
        [field: SerializeField] public GameEventChannelSO CreateChannel {get; private set;}
        public ModuleOwner Owner { get; private set; }
        public PlayerController Player { get; private set; }
        private INavMovement _navMovement;
        private INavAgentRenderer _navAgentRenderer;
        private IVFXAgentTrigger _agentTrigger;
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
            _agentTrigger =  Owner.GetModule<IVFXAgentTrigger>();

            _skillDict = GetComponentsInChildren<ISkill>()
                .ToDictionary(skill => skill.SkillData.skillIndex);

            foreach (ISkill skill in _skillDict.Values)
            {
                skill.InitializeSkill(this); //스킬들을 초기화
            }

            Player.PlayerInput.OnAttackKeyPressed += HandleAttackKeyPress; //기본공격은 이걸로 바인딩
            Player.PlayerInput.OnDashKeyPressed += HandleDashKeyPress;
            Player.PlayerInput.OnSkill1Pressed += HandleSkill1KeyPress;
            Player.PlayerInput.OnSkill2Pressed += HandleSkill2KeyPress;
            Player.PlayerInput.OnSkill3Pressed += HandleSkill3KeyPress;
            Player.PlayerInput.OnSkill4Pressed += HandleSkill4KeyPress;
        }
        public void AfterInit()
        {
            _agentTrigger.OnShowPoolingVfx += HandlePoolingVfx;
        }

        private void HandlePoolingVfx(ShowPoolingVfx obj)
        {
            CreateChannel.RaiseEvent(obj);
        }

        private void OnDestroy()
        {
            if (Player != null && Player.PlayerInput != null)
            {
                Player.PlayerInput.OnAttackKeyPressed -= HandleAttackKeyPress; //기본공격은 이걸로 바인딩
                Player.PlayerInput.OnDashKeyPressed -= HandleDashKeyPress;
                Player.PlayerInput.OnSkill1Pressed -= HandleSkill1KeyPress;
                Player.PlayerInput.OnSkill2Pressed -= HandleSkill2KeyPress;
                Player.PlayerInput.OnSkill3Pressed -= HandleSkill3KeyPress;
                Player.PlayerInput.OnSkill4Pressed -= HandleSkill4KeyPress;
            }
        }
        private void HandleSkill1KeyPress()
        {
            if (CanUseSkill(2))
            {
                Player.ChangeState(PlayerState.SKILL, 0);
                UseSkill(2);
            }
        }
        private void HandleSkill2KeyPress()
        {
            if (CanUseSkill(3))
            {
                Player.ChangeState(PlayerState.SKILL, 0);
                UseSkill(3);
            }
        }
        private void HandleSkill3KeyPress()
        {
            if (CanUseSkill(4))
            {
                Player.ChangeState(PlayerState.SKILL, 0);
                UseSkill(4);
            }
        }
        private void HandleSkill4KeyPress()
        {
            if (CanUseSkill(5))
            {
                Player.ChangeState(PlayerState.SKILL, 0);
                UseSkill(5);
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
            if (_currentSkill is ILinkSkill { CanLink: true } && _currentSkill.SkillData.skillIndex == skillIndex)
            {
                return true;
            }
            
            if (_currentSkill is { IsUsing: true } )
            {
                return false;
            }
            
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
                if (_currentSkill != null)
                    _currentSkill.OnSkillEnd -= HandleCurrentSkillEnd;
                _currentSkill = skill;
                _currentSkill.OnSkillEnd += HandleCurrentSkillEnd;
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
