using System;
using _Scripts.CombatSystem;
using Agents;
using CombatSystem;
using Players;
using UnityEngine;

namespace _Scripts.Players.SkillSystem
{
    public abstract class AbstractPlayerSkill : MonoBehaviour,ISkill
    {
        public event Action OnSkillEnd;
        [field: SerializeField] public SkillDataSO SkillData { get; private set; }

        protected PlayerController _player;
        protected PlayerSkillModule _skillModule;
        protected IControlMovement _movement;
        protected IRenderer _renderer;
        protected float _lastUsingTime;
        
        public bool IsUsing { get; private set; }
        public float NormalizedCooldown 
            => Mathf.Approximately(SkillData.cooldown, 0f) ? 1f : 
                Mathf.Clamp01((Time.time - _lastUsingTime) / SkillData.cooldown);
        
        public virtual void InitializeSkill(ISkillModule skillModule)
        {
             _skillModule = skillModule as PlayerSkillModule;
             Debug.Assert(_skillModule != null, "플레이어 스킬은 반드시 플레이어 스킬 모듈의 자식이어야 합니다.");
             _player = _skillModule.Player;
             _renderer = _player.GetModule<IRenderer>();
             Debug.Assert(_renderer != null, "플레이어 렌더러 모듈이 없습니다.");
             _movement = _player.GetModule<IControlMovement>();
             Debug.Assert(_movement != null, "이동 모듈이 필요합니다.");
             IsUsing = false;
        }

        public abstract bool CanUseSkill(GameObject target = null);

        public virtual void UseSkill(GameObject target = null)
        {
            IsUsing = true;
        }

        public virtual void StopSkill()
        {
            IsUsing = false;
            _lastUsingTime = Time.time;
            OnSkillEnd?.Invoke();
        }
    }
}