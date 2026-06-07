using Agents;
using CoreSystem;
using GGMLib.ModuleSystem;
using Players;
using UnityEngine;

namespace _Scripts.Boss
{
    public class BossMovement : MonoBehaviour, IModule, IControlMovement
    {
        [SerializeField] private float moveSpeed = 8f;
        [SerializeField] private float gravity = -9.8f;
        [SerializeField] private float rotationSpeed = 8f;
        [SerializeField] private CharacterController controller;
        
        private Vector3 _velocity;
        private float _verticalVelocity;
        private Vector3 _movementDirection;
        private ModuleOwner _owner;
        private Vector3 _manualVelocity; //수동 조작 속도.
        private VfxModule _vfxModule;
        
        public bool IsGround => controller.isGrounded;
        public Vector3 Velocity => _velocity;

        public bool CanManualMove { get; set; } = true;
        
        public void Initialize(ModuleOwner owner)
        {
            _owner = owner;
            _vfxModule = _owner.GetModule<VfxModule>();
        }
        

        public void SetMovementDirection(Vector2 inputDirection)
        {
            Vector3 newDirection = new Vector3(inputDirection.x, 0f, inputDirection.y);
            
            _movementDirection = newDirection;
        }

        public void SetMovementVelocity(Vector3 velocity)
        {
            _manualVelocity = velocity;
        }

        public void RotateTo(Vector3 direction)
        {
            if (direction.sqrMagnitude < Mathf.Epsilon) return;
            direction.y = 0;
            _owner.transform.forward = direction.normalized;
        }

        private void FixedUpdate()
        {
            CalculateMovement();
            ApplyGravity();
            MoveCharacter();
        }

        private void CalculateMovement()
        {
            if(CanManualMove)
                _velocity = Quaternion.Euler(0, -45f, 0) * _movementDirection;
            else 
                _velocity = _manualVelocity;
            
            _velocity *= moveSpeed * Time.fixedDeltaTime;

            if (_velocity.sqrMagnitude > Mathf.Epsilon)
            {
                Quaternion targetRotation = Quaternion.LookRotation(_velocity);
                _owner.transform.rotation = Quaternion.Lerp(_owner.transform.rotation, targetRotation, 
                    Time.fixedDeltaTime * rotationSpeed);
            }
        }

        private void ApplyGravity()
        {
            if (IsGround && _verticalVelocity <= 0)
            {
                _verticalVelocity = -0.3f; //아래로 당기는 힘을 준다.
            }
            else
            {
                _verticalVelocity += gravity * Time.fixedDeltaTime;
            }
            _velocity.y = _verticalVelocity; //중력 적용한 힘을 가한다.
        }

        private void MoveCharacter()
        {
            if(controller.enabled)
                controller.Move(_velocity);   
        }

        
    }
}