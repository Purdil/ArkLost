using System;
using _Scripts.Agents;
using _Scripts.Enemies;
using Agents;
using GGMLib.AnimationSystem;
using GGMLib.ModuleSystem;
using UnityEngine;
using UnityEngine.AI;

namespace Enemies
{
    public class NavAgentRenderer : AgentRenderer, IAfterInitModule, INavAgentRenderer
    {
        [SerializeField] private AnimParamSO speedParam;
        
        [Header("Navigation Agent control")]
        [SerializeField] private bool updateRotation;
        [SerializeField] private bool updatePosition;

        [Header("Force rotation settings")]
        [SerializeField] private float rootMotionSpeedMultiplier = 1f;
        [SerializeField] private bool forceRotation;
        [SerializeField] private float forceRotationSpeed;
        
        private INavMovement _navMovement;
        private NavMeshAgent _navAgent;
        private Vector2 _velocity;
        private Vector2 _smoothDeltaPosition;
        private bool _isSyncNavPos = true;

        public bool IsUpdateRotationByAnimator
        {
            get => !updateRotation; //이게 켜져있으면 NavAgent가 처리한다.
            set
            {
                updateRotation = !value;
                if (_navAgent != null)
                {
                    _navAgent.updateRotation = updateRotation; //갱신한다.
                }
            }
        }

        public override void Initialize(ModuleOwner owner)
        {
            base.Initialize(owner);
            _navMovement = owner.GetModule<INavMovement>();
            Debug.Assert(_navMovement != null, "NavAgentRenderer는 INavMovement가 필요합니다.");
        }

        public void AfterInit()
        {
            _navAgent = _navMovement.NavMeshAgent; //이건 AfterInit에서 해야해.
            Debug.Assert(_navAgent != null, "NavAgent가 null입니다.");
            Animator.applyRootMotion = true;
            _navAgent.updatePosition = updatePosition;
            _navAgent.updateRotation = updateRotation;
        }


        private void OnAnimatorMove()
        {
            if (_navAgent == null || !_isSyncNavPos) return;

            Vector3 delta = Animator.deltaPosition * rootMotionSpeedMultiplier;
            delta.y = 0f;

            Vector3 rootPosition = _owner.transform.position + delta;
            rootPosition.y = _navAgent.nextPosition.y;

            if (NavMesh.SamplePosition(rootPosition, out NavMeshHit hit, 0.4f, NavMesh.AllAreas))
            {
                _owner.transform.position = rootPosition;
                _navAgent.nextPosition = hit.position;
            }

            if (IsUpdateRotationByAnimator)
                _owner.transform.rotation *= Animator.deltaRotation;
        }

        private void Update()
        {
            if (_navAgent.enabled)
            {
                SynchronizeAnimatorAndNavAgent();
                ForceRotationControl();
            }
                
        }

        public void SetSyncNavPos(bool isSync)
        {
            _isSyncNavPos = isSync;
        }

        public void BeginManualControl()
        {
            SetSyncNavPos(false);
            UpdateNavAgentPosition();
            Animator.SetFloat(speedParam.ParamHash, 0f);
        }

        public void EndManualControl()
        {
            Vector3 ownerPosition = _owner.transform.position;

            if (NavMesh.SamplePosition(ownerPosition, out NavMeshHit hit, 0.4f, NavMesh.AllAreas))
            {
                ownerPosition = hit.position;
            }

            _navAgent.Warp(ownerPosition);
            _navAgent.nextPosition = ownerPosition;
            _navAgent.velocity = Vector3.zero;

            Animator.rootPosition = ownerPosition;

            _smoothDeltaPosition = Vector2.zero;
            _velocity = Vector2.zero;
            Animator.SetFloat(speedParam.ParamHash, 0f);

            SetSyncNavPos(true);
        }

        public void UpdateNavAgentPosition()
        {
            if (_navAgent == null || !_navAgent.enabled) return;

            Vector3 ownerPosition = _owner.transform.position;
            if (!NavMesh.SamplePosition(ownerPosition, out NavMeshHit hit, 0.4f, NavMesh.AllAreas)) return;
            
            _navAgent.Warp(hit.position);
            _navAgent.nextPosition = hit.position;
            _navAgent.velocity = Vector3.zero;
            _smoothDeltaPosition = Vector2.zero;
            _velocity = Vector2.zero;
            
            SynchronizeAnimatorAndNavAgent();
        }

        private void SynchronizeAnimatorAndNavAgent()
        {
            if(_navAgent == null || !_isSyncNavPos) return;

            //월드 좌표의 델타좌표.
            Vector3 worldDeltaPosition = _navAgent.nextPosition - _owner.transform.position;
            worldDeltaPosition.y = 0; //이건 계산하지 않을거다.
            
            float dx = Vector3.Dot(_owner.transform.right, worldDeltaPosition);
            float dy = Vector3.Dot(_owner.transform.forward, worldDeltaPosition);
            
            Vector2 localDelta = new Vector2(dx, dy);
            float smooth = Mathf.Min(1, Time.deltaTime / 0.1f);
            
            _smoothDeltaPosition = Vector2.Lerp(_smoothDeltaPosition, localDelta, smooth);
            _velocity = _smoothDeltaPosition / Time.deltaTime;

            if (_navAgent.remainingDistance <= _navAgent.stoppingDistance)
            {
                _velocity = Vector2.Lerp(Vector2.zero, _velocity, _navAgent.remainingDistance / _navAgent.stoppingDistance);
            }
            
            Animator.SetFloat(speedParam.ParamHash, _velocity.magnitude);
            //모델의 위치가 너무 벗어난경우 처리
            float deltaMagnitude = worldDeltaPosition.magnitude;
            if (deltaMagnitude > _navAgent.radius * 0.5f)
            {
                _owner.transform.position = Vector3.Lerp(Animator.rootPosition, _navAgent.nextPosition, smooth);
            }
        }

        private void ForceRotationControl()
        {
            if (!_isSyncNavPos || !forceRotation || _navAgent == null || _navMovement.IsArrived) return;

            Vector3 desiredDirection = _navAgent.steeringTarget - _owner.transform.position; 
            if(desiredDirection.sqrMagnitude < 0.01f) return; //거의 정지면 회전 안함.

            desiredDirection.y = 0;
            Quaternion desiredRotation = Quaternion.LookRotation(desiredDirection);
            _owner.transform.rotation = Quaternion.RotateTowards(
                _owner.transform.rotation, desiredRotation, forceRotationSpeed * Time.deltaTime);
        }
    }
}
