using GGMLib.ModuleSystem;
using UnityEngine;
using UnityEngine.AI;

namespace _Scripts.Players
{
    public class CharacterMovementManager : MonoBehaviour, IModule
    {
        [SerializeField] private float ignoreMoveDistance = 0.1f;
        public enum MoveMode { NavMesh, CharacterController }

        private const float MinMoveDistance = 0.1f;

        private NavMeshAgent _navAgent;
        private CharacterController _character;
        private Transform _ownerTransform;
        private Vector2 _lastRequestPos;

        public void Initialize(ModuleOwner owner)
        {
            _ownerTransform = owner.transform;
            _navAgent = owner.GetComponent<NavMeshAgent>();
            _character = owner.GetComponent<CharacterController>();
            _character.enabled = false;
        }
        private MoveMode _currentMode;

        public void SwitchMode(MoveMode newMode)
        {
            if (_currentMode == newMode) return;

            bool syncTransforms = false;

            switch (newMode)
            {
                case MoveMode.NavMesh:
                    _character.enabled = false;
                    _navAgent.enabled = true;
                    _navAgent.Warp(GetCurrentPosition());
                    syncTransforms = true;
                    break;

                case MoveMode.CharacterController:
                    _navAgent.enabled = false;
                    _character.enabled = true;
                    syncTransforms = true;
                    break;
            }

            _currentMode = newMode;
            if (syncTransforms)
                Physics.SyncTransforms();
        }

        public bool IsDestinationNearCurrentPosition(Vector2 destination)
        {
            Vector3 currentPosition = GetCurrentPosition();
            Vector2 currentPositionXZ = new Vector2(currentPosition.x, currentPosition.z);
            float moveDistance = GetMoveDistance();
            float requestDistance = Vector2.Distance(_lastRequestPos, destination);
            _lastRequestPos = destination;
            return (destination - currentPositionXZ).sqrMagnitude <= moveDistance * moveDistance
                && requestDistance < ignoreMoveDistance; // 작으면 무시
        }

        public bool IsDestinationNearCurrentPosition(Vector3 destination)
        {
            return IsDestinationNearCurrentPosition(new Vector2(destination.x, destination.z));
        }

        private float GetMoveDistance()
        {
            float stoppingDistance = _navAgent != null ? _navAgent.stoppingDistance : 0f;
            return Mathf.Max(MinMoveDistance, stoppingDistance);
        }

        private Vector3 GetCurrentPosition()
        {
            return _ownerTransform != null ? _ownerTransform.position : transform.position;
        }
    }
}
