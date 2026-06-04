using _Scripts.Players;
using _Scripts.Players.FSM;
using Agents;
using UnityEngine;

namespace Players.FSM
{
    public class PlayerSkillState : AbstractPlayerState
    {
        private readonly PlayerSkillModule _skillModule;
        private bool _isSkillEnd;
        
        public PlayerSkillState(Agent agent, int stateClipHash) : base(agent, stateClipHash)
        {
            _skillModule = agent.GetModule<PlayerSkillModule>();
            Debug.Assert(_skillModule != null, "플레이어 스킬 모듈을 찾을 수 없습니다.");
        }

        public override void Enter(float transitionDuration, int layerIndex = 0)
        {
            // base.Enter(transitionDuration, layerIndex); 부모껄 실행하면 안된다.
            _isSkillEnd = false;
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
            base.Exit();
        }

        private void HandleSkillEnd() => _isSkillEnd = true;
    }
}