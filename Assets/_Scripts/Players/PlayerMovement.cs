using System;
using Agents;
using CoreSystem;
using GGMLib.ModuleSystem;
using UnityEngine;

namespace Players
{
    public class PlayerMovement : MonoBehaviour, IModule, IControlMovement, IAfterInitModule
    {
        [SerializeField] private float moveSpeed = 8f;
        [SerializeField] private float gravity = -9.8f;
        [SerializeField] private float rotationSpeed = 8f;
        [SerializeField] private CharacterController controller;
        [SerializeField] private AssetNameSO footStepVfxName;
        
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
        
        public void AfterInit()
        {
            if(_vfxModule != null && footStepVfxName != null)
                _vfxModule.StopVfx(footStepVfxName.AssetHash);
        }

        public void SetMovementDirection(Vector2 inputDirection)
        {
            Vector3 newDirection = new Vector3(inputDirection.x, 0f, inputDirection.y);

            if (_vfxModule != null && footStepVfxName != null)
            {
                if (newDirection.magnitude > 0.01f && _movementDirection.sqrMagnitude <= 0.01f)
                {
                    _vfxModule.PlayVfx(footStepVfxName.AssetHash);
                }else if (newDirection.magnitude <= 0.01f && _movementDirection.sqrMagnitude >= 0.01f)
                {
                    _vfxModule.StopVfx(footStepVfxName.AssetHash);
                }
            }
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