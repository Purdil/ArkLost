using _Scripts.Agents.FSM;
using _Scripts.Players.FSM;
using Agents;
using Agents.FSM;
using Players;
using Players.FSM;
using UnityEngine;

namespace _Scripts.Players
{
    public class PlayerController : Agent
    {
        [field: SerializeField] public PlayerInputSO PlayerInput { get; private set; }
        [SerializeField] private StateListSO playerStates;
        
        private IControlMovement _movement;
        private StateMachine _stateMachine;

        protected override void InitializeComponents()
        {
            base.InitializeComponents();
            _stateMachine = new StateMachine(this, playerStates.states); //상태 머신을 생성한다.
            
            _movement = GetModule<IControlMovement>();
            Debug.Assert(_movement != null, "플레이어 이동 관련 모듈이 없습니다.");
        }

        private void Start()
        {
            _stateMachine.ChangeState(0, transitionDuration: 0); //IDLE상태로
        }

        private void Update()
        {
            _stateMachine.UpdateMachine();
        }

        public void ChangeState(PlayerState newStateIndex, float transitionDuration)
            => _stateMachine.ChangeState((int)newStateIndex, transitionDuration);
    }
}