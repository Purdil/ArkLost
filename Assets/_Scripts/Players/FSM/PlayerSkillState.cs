using _Scripts.Agents;
using _Scripts.Agents.FSM.AgentTriggers;
using _Scripts.CombatSystem;
using Agents;
using Players;
using Players.FSM;
using UnityEngine;

namespace _Scripts.Players.FSM
{
    public class PlayerSkillState : AbstractPlayerState
    {
        private readonly ISkillModule _skillModule;
        private readonly AgentTrigger _trigger;
        private bool _isSkillEnd;
        
        public PlayerSkillState(Agent agent, int stateClipHash) : base(agent, stateClipHash)
        {
            _skillModule = agent.GetModule<ISkillModule>();
            Debug.Assert(_skillModule != null, "플레이어 스킬 모듈을 찾을 수 없습니다.");
            _trigger = agent.GetModule<AgentTrigger>();
            Debug.Assert(_trigger != null, "트리거를 찾을 수 없습니다.");
        }

        public override void Enter(float transitionDuration, int layerIndex = 0)
        {
            // base.Enter(transitionDuration, layerIndex); 부모껄 실행하면 안된다.
            _isSkillEnd = false;
            _player.PlayerInput.OnMovementChange += HandleManualMovement;
            _skillModule.OnCurrentSkillEnd += HandleSkillEnd;
        }


        public override void Update()
        {
            base.Update();
            if(_isSkillEnd)
                _player.ChangeState(PlayerState.IDLE, 0.1f);
        }

        public override void Exit()
        {
            _skillModule.OnCurrentSkillEnd -= HandleSkillEnd;
            _player.PlayerInput.OnMovementChange -= HandleManualMovement;
            base.Exit();
        }
        private void HandleManualMovement()
        {
            if (_trigger.CanManualMovement)
            {
                _skillModule.StopSkillIfNotFinished();
                _trigger.CanManualMovement = false;
                
                _player.ChangeState(PlayerState.RUN, 0.1f);
            }
            
        }

        private void HandleSkillEnd() => _isSkillEnd = true;
    }
}