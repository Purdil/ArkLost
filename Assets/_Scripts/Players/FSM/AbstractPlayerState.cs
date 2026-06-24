using _Scripts.Enemies;
using Agents;
using Agents.FSM;
using Players;

namespace _Scripts.Players.FSM
{
    public abstract class AbstractPlayerState : AgentState
    {
        protected PlayerController _player;
        protected IControlMovement _controlMovement;
        protected INavMovement  _navMovement;
        protected const float INPUT_DEADZONE = 0.1f; //입력을 안받는 임계값
        
        protected AbstractPlayerState(Agent agent, int stateClipHash) : base(agent, stateClipHash)
        {
            _player = agent as PlayerController;
            _navMovement = agent.GetModule<INavMovement>();
            _controlMovement = agent.GetModule<IControlMovement>();
        }
    }
}