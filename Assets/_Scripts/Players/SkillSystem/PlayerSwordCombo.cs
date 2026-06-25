using System.Collections;
using _Scripts.Agents;
using _Scripts.CombatSystem;
using Agents;
using CombatSystem;
using CoreSystem;
using CoreSystem.Events;
using Enemies;
using GGMLib.AnimationSystem;
using GGMLib.ObjectPool.Runtime;
using UnityEngine;

namespace _Scripts.Players.SkillSystem
{
    public class PlayerSwordCombo : AbstractPlayerSkill , ILinkSkill
    {
        [SerializeField] private PoolItemSO slashVfxItem;
        [SerializeField] private PoolItemSO impactVfxItem;

        [SerializeField] private AnimParamSO[] comboClips;
        [SerializeField] private AnimationCurve[] comboCurves; //움직이는 양을 조절
        [SerializeField] private float[] comboDurations; //콤보 지속시간
        [SerializeField] private AssetNameSO[] comboEffects;

        [SerializeField] private float comboWindow = 0.4f; //콤보가 이어지는 시간

        private PlayerTrigger _agentTrigger;
        private AbstractDamageCaster _damageCaster;
        private INavAgentRenderer _navAgentRenderer;
        private CharacterMovementManager _movementManager;
        private IEnumerator _curCoroutine;
        
        public bool CanLink { get; private set; }
        public float AttackSpeed { get; private set; }
        public int ComboCounter { get; private set; } = 0;

        public override void InitializeSkill(ISkillModule skillModule)
        {
            base.InitializeSkill(skillModule);
            _agentTrigger = _player.GetModule<PlayerTrigger>();
            Debug.Assert(_agentTrigger != null, "Sword combo 공격은 애니메이션 트리거가 필요합니다.");
            _player.GetModule<VfxModule>();
            _navAgentRenderer = _player.GetModule<INavAgentRenderer>();
            _damageCaster = GetComponentInChildren<AbstractDamageCaster>();
            Debug.Assert(_damageCaster != null, $"데미지 캐스터가 있어야 정상적으로 데미지를 줄 수 있습니다. : {gameObject}");
            _movementManager = _player.GetModule<CharacterMovementManager>();
            Debug.Assert(_movementManager != null, "플레이어 소드 콤보는 무브먼트 매니저를 필요로 합니다.");
            _damageCaster.InitCaster(skillModule.Owner); //해당 오너로 캐스터를 초기화(오너를 넣어줘야 차후에 딜러가 누군지 알 수 있다.)
        }

        public override bool CanUseSkill(GameObject target = null)
        {
            bool cooldownReady = NormalizedCooldown >= 1f;
            bool notUsing = !IsUsing;
            bool canUse = cooldownReady && notUsing;
            
            return canUse;
        }

        public override void UseSkill(GameObject target = null)
        {
            if (CanLink)
            {
                _lastUsingTime = Time.time;
                ComboCounter++;
                CanLink = false;
                if (_curCoroutine != null)
                {
                    StopCoroutine(_curCoroutine);
                    _curCoroutine = null;
                }
                UnsubscribeTriggerEvents();
            }
            _movementManager.SwitchMode(CharacterMovementManager.MoveMode.CharacterController);
            base.UseSkill(target);
            bool comboCounterOver = ComboCounter >= comboClips.Length;
            bool comboWindowExhaust = Time.time >= _lastUsingTime + comboWindow;
            if (comboCounterOver || comboWindowExhaust)
            {
                ComboCounter = 0;
            }
            _renderer.PlayClip(comboClips[ComboCounter].ParamHash, 0f, 0.05f);
            Vector3 mousePosition = _player.PlayerInput.GetWorldMousePosition();
            mousePosition.y = _player.transform.position.y;
            Vector3 direction = (mousePosition - _player.transform.position).normalized;
            
            _movement.RotateTo(direction);
            _curCoroutine = SwordComboCoroutine();
            StartCoroutine(_curCoroutine);
        }

        private IEnumerator SwordComboCoroutine()
        {
            _agentTrigger.OnAnimationEnd += HandleAnimationEnd;
            _agentTrigger.OnDamageCast += HandleDamageCast;
            _agentTrigger.OnLinkTimeEnd += HandleLinkCombo;
            AnimationCurve comboCurve = comboCurves[ComboCounter];
            float comboDuration = comboDurations[ComboCounter];
            float currentDuration = 0;
            Vector3 forward = _player.transform.forward;
            _movement.CanManualMove = false;//자동 조작 모드로 변경
            while (IsUsing)
            {
                float percent = currentDuration / comboDuration; //0~1 값으로 값을 정규화해준다.
                currentDuration += Time.deltaTime;
                float force = comboCurve.Evaluate(percent);
                _movement.SetMovementVelocity(forward * force);
                yield return null;
            }

            _movement.CanManualMove = true; //수동 조작모드로 변경.
            _movement.SetMovementVelocity(Vector3.zero);
            _navAgentRenderer.EndManualControl();
            UnsubscribeTriggerEvents();
            _curCoroutine = null;
        }

        private void HandleLinkCombo()
        {
            CanLink = true;
        }

        private void HandleDamageCast()
        {
            Vector3 position = _damageCaster.transform.position;
            bool isHit = _damageCaster.CastDamage(position, transform.forward, SkillData);
            if (isHit)
            {
                /*var evt = CreateEvents.ShowPoolingVfx.InitData(
                    slashVfxItem, _damageCaster.LastHitPosition, Quaternion.identity);
                _skillModule.CreateChannel.RaiseEvent(evt);
                
               evt = CreateEvents.ShowPoolingVfx.InitData(
                    impactVfxItem, _damageCaster.LastHitPosition, Quaternion.identity);
                _skillModule.CreateChannel.RaiseEvent(evt);*/
            }
        }

       

        private void HandleAnimationEnd() => StopSkill();

        public override void StopSkill()
        {
            if (!IsUsing && _curCoroutine == null)
                return;

            ComboCounter++;
            UnsubscribeTriggerEvents();

            if (_curCoroutine != null)
            {
                StopCoroutine(_curCoroutine);
                _curCoroutine = null;
            }

            CanLink = false;
            _navAgentRenderer.EndManualControl();
            _movementManager.SwitchMode(CharacterMovementManager.MoveMode.NavMesh);
            base.StopSkill();
        }

        private void UnsubscribeTriggerEvents()
        {
            _agentTrigger.OnDamageCast -= HandleDamageCast;
            _agentTrigger.OnLinkTimeEnd -= HandleLinkCombo;
            _agentTrigger.OnAnimationEnd -= HandleAnimationEnd;
        }

    }
}
