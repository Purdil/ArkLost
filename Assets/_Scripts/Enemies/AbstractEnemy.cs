using _Scripts.Agents;
using _Scripts.Agents.AgentTriggers;
using _Scripts.CombatSystem;
using _Scripts.Enemies.BT;
using _Scripts.Enemies.BT.Events;
using Agents;
using CombatSystem;
using Enemies.BT;
using Enemies.BT.Events;
using Enemies.Nav;
using Reflex.Attributes;
using Unity.Behavior;
using UnityEngine;

namespace _Scripts.Enemies
{
    [RequireComponent(typeof(BehaviorGraphAgent))]
    public abstract class AbstractEnemy : Agent
    {
        [field: SerializeField] public float DetectRadius { get; set; } = 5f; //감지 거리
        [field: SerializeField] public float ViewAngle { get; set; } = 160f; //시야각
        [field: SerializeField] public float StopDistance { get; set; } = 1f; //정지 최소 거리
        public INavMovement NavMovement { get; private set; }
        public BehaviorGraphAgent BtAgent { get; private set; }
        public IRenderer AgentRenderer { get; private set; }
        public AgentSensor Sensor { get; private set; }
        public ISkillModule SkillModule { get; private set; }
        public AgentTrigger Trigger { get; private set; }
        
        public StateChannel StateChannel { get; private set; }
        
        [Inject] [field: SerializeField] public WayPointManager WayPoints { get; private set; }
        public int CurrentWayPointIndex { get; set; } = -1; //임시코드
        
        [SerializeField] private bool canDrawDebug = false;
        
        protected override void InitializeComponents()
        {
            base.InitializeComponents();
            NavMovement = GetModule<INavMovement>();
            AgentRenderer = GetModule<IRenderer>();
            BtAgent = GetComponent<BehaviorGraphAgent>();
            Sensor = GetModule<AgentSensor>();
            SkillModule = GetModule<ISkillModule>();
            Trigger = GetModule<AgentTrigger>();
        }

        protected virtual void Start()
        {
            if (!GetVariable<StateChannel>(BtVars.StateChannel, out var channelVariable))
            {
                Debug.LogError($"BTAgent에 상태채널이 없습니다. : {gameObject}");
                return;
            }

            StateChannel = channelVariable.Value;
            SetVariableValue(BtVars.Boss, this);
        }

        protected override void HandleHitEvent()
        {
            base.HandleHitEvent();
            StateChannel.SendEventMessage(EnemyState.HIT);
        }

        #region BT Helpers

        public void SetVariableValue<T>(string variableName, T value)
        {
            Debug.Assert(!string.IsNullOrEmpty(variableName), "변수 이름은 null일 수 없습니다.");

            if (BtAgent.GetVariable<T>(variableName, out BlackboardVariable<T> variable))
            {
                variable.Value = value;
            }
            else
            {
                Debug.LogError($"Var : {variableName}을 찾을 수 없습니다.");
            }
        }

        public bool GetVariable<T>(string variableName, out BlackboardVariable<T> variable)
        {
            Debug.Assert(!string.IsNullOrEmpty(variableName), "변수 이름은 null일 수 없습니다.");
            return BtAgent.GetVariable<T>(variableName, out variable);
        }

        #endregion

        private void OnDrawGizmos()
        {
            if (!canDrawDebug) return;
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, DetectRadius);
        }
    }
}