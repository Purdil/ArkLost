using _Scripts.Players;
using _Scripts.Players.FSM;
using Agents;
using UnityEngine;

namespace Players.FSM
{
    public class PlayerIdleState : AbstractPlayerState
    {
        private CharacterMovementManager _movementManager;
        public PlayerIdleState(Agent agent, int stateClipHash) : base(agent, stateClipHash)
        {
            _movementManager = agent.GetModule<CharacterMovementManager>();
        }

        public override void Enter(float transitionDuration, int layerIndex = 0)
        {
            base.Enter(transitionDuration, layerIndex);
            _movementManager.SwitchMode(CharacterMovementManager.MoveMode.NavMesh);      
            _player.PlayerInput.OnMovementChange += HandleMovementChange;
            _navMovement.StopImmediately();
            _controlMovement.SetMovementDirection(Vector2.zero);
        }

        public override void Exit()
        {
            _player.PlayerInput.OnMovementChange -= HandleMovementChange;
            base.Exit();
        }

        private void HandleMovementChange(Vector2 movementPosition)
        {
            if (movementPosition.magnitude > INPUT_DEADZONE)
            {
                _player.ChangeState(PlayerState.RUN, 0.1f); //RUN상태로 전환
            }
        }
    }
}