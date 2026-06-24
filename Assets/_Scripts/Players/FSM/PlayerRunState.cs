using Agents;
using Players.FSM;
using UnityEngine;

namespace _Scripts.Players.FSM
{
    public class PlayerRunState : AbstractPlayerState
    {
        private CharacterMovementManager _movementManager;
        public PlayerRunState(Agent agent, int stateClipHash) : base(agent, stateClipHash)
        {
            _movementManager = agent.GetModule<CharacterMovementManager>();
            Debug.Assert(_movementManager != null, "플레이어 RunState은 movementManager가 필요합니다.");
        }

        public override void Enter(float transitionDuration, int layerIndex = 0)
        {
            base.Enter(transitionDuration, layerIndex);
            Debug.Log("RunState Enter");
            _movementManager.SwitchMode(CharacterMovementManager.MoveMode.NavMesh);
            _navMovement.SetDestination(_player.PlayerInput.GetWorldMousePosition());
            Physics.SyncTransforms();
            _player.PlayerInput.OnMovementChange += HandleMovementChange;
        }

        public override void Update()
        {
            base.Update();
            if (_navMovement.IsArrived)
            {
                _movementManager.SwitchMode(CharacterMovementManager.MoveMode.CharacterController);
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