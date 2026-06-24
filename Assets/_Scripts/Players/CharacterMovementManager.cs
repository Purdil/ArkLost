using GGMLib.ModuleSystem;
using UnityEngine;
using UnityEngine.AI;

namespace _Scripts.Players
{
    public class CharacterMovementManager : MonoBehaviour, IModule
    {
        public enum MoveMode { NavMesh, CharacterController }

        private NavMeshAgent _navAgent;
        private CharacterController _character;

        public void Initialize(ModuleOwner owner)
        {
            _navAgent = owner.GetComponent<NavMeshAgent>();
            _character = owner.GetComponent<CharacterController>();
            _character.enabled = false;
        }
        private MoveMode _currentMode;

        public void SwitchMode(MoveMode newMode)
        {
            if (_currentMode == newMode) return;

            switch (newMode)
            {
                case MoveMode.NavMesh:
                    _character.enabled = false;
                    _navAgent.enabled = true;
                    _navAgent.Warp(transform.position); 
                    Physics.SyncTransforms();
                    break;

                case MoveMode.CharacterController:
                    _navAgent.enabled = false;
                    Physics.SyncTransforms();       
                    _character.enabled = true;
                    break;
            }

            _currentMode = newMode;
        }

    }
}