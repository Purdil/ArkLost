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

        protected AbstractPlayerState(Agent agent, int stateClipHash) : base(agent, stateClipHash)
        {
            _player = agent as PlayerController;
            _navMovement = agent.GetModule<INavMovement>();
            _controlMovement = agent.GetModule<IControlMovement>();
        }
    }
}
