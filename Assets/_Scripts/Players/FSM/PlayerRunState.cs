using Agents;
using Players.FSM;
using UnityEngine;

namespace _Scripts.Players.FSM
{
    public class PlayerRunState : AbstractPlayerState
    {
        private CharacterMovementManager _movementManager;
        private bool _shouldReturnToIdle;

        public PlayerRunState(Agent agent, int stateClipHash) : base(agent, stateClipHash)
        {
            _movementManager = agent.GetModule<CharacterMovementManager>();
            Debug.Assert(_movementManager != null, "플레이어 RunState은 movementManager가 필요합니다.");
        }

        public override void Enter(float transitionDuration, int layerIndex = 0)
        {
            base.Enter(transitionDuration, layerIndex);
            _shouldReturnToIdle = false;
            _movementManager.SwitchMode(CharacterMovementManager.MoveMode.NavMesh);
            
            TrySetDestination(_player.PlayerInput.GetWorldMousePosition());
            
            _player.PlayerInput.OnMovementChange += HandleMovementChange;
        }

        public override void Update()
        {
            base.Update();
            if (_shouldReturnToIdle)
            {
                _player.ChangeState(PlayerState.IDLE, 0.1f);
                return;
            }

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

        private void HandleMovementChange()
        {
            TrySetDestination(_player.PlayerInput.GetWorldMousePosition());
        }

        private void TrySetDestination(Vector3 destination)
        {
            if (_movementManager.IsDestinationNearCurrentPosition(destination))
            {
                _navMovement.StopImmediately();
                _shouldReturnToIdle = true;
                return;
            }

            _navMovement.SetDestination(destination);
        }
    }
}
