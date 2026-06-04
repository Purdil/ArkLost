using Agents;
using Players.FSM;
using UnityEngine;

namespace _Scripts.Players.FSM
{
    public class PlayerRunState : AbstractPlayerState
    {
        public PlayerRunState(Agent agent, int stateClipHash) : base(agent, stateClipHash)
        {
        }

        public override void Enter(float transitionDuration, int layerIndex = 0)
        {
            base.Enter(transitionDuration, layerIndex);
            _navMovement.SetDestination(_player.PlayerInput.GetWorldMousePosition());
            _player.PlayerInput.OnMovementChange += HandleMovementChange;
        }

        public override void Update()
        {
            base.Update();
            Physics.SyncTransforms();
            if (_navMovement.IsArrived)
            {
                _player.ChangeState(PlayerState.IDLE, 0.1f); 
            }
        }

        public override void Exit()
        {
            _player.PlayerInput.OnMovementChange -= HandleMovementChange;
            base.Exit();
        }

        private void HandleMovementChange(Vector2 movementPosition)
        {
            _navMovement.SetDestination(_player.PlayerInput.GetWorldMousePosition());
        }
    }
}