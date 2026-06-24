using System;
using _Scripts.CombatSystem;
using _Scripts.Enemies;
using Agents;
using CombatSystem;
using UnityEngine;

namespace Enemies.EnemySkills
{
    public abstract class AbstractEnemySkill : MonoBehaviour, ISkill
    {
        public event Action OnSkillEnd;
        [field:SerializeField] public SkillDataSO SkillData { get; private set; }

        protected AbstractEnemy _ownerEnemy;
        protected float _lastUseTime;
        protected IRenderer _renderer;
        
        public bool IsUsing { get; private set; }
        public float NormalizedCooldown => Mathf.Approximately(SkillData.cooldown,0)
        ? 1f : Mathf.Clamp01((Time.time - _lastUseTime) / SkillData.cooldown);
        public virtual void InitializeSkill(ISkillModule skillModule)
        {
            _ownerEnemy = skillModule.Owner as AbstractEnemy;
            Debug.Assert(_ownerEnemy != null,$"적 공격 스킬은 반드시 적 공격 컴포넌트의 자식이어야 합니다. {gameObject}");
            _renderer = _ownerEnemy.GetModule<IRenderer>(); //렌더링 모듈 가져오고
        }

        public abstract bool CanUseSkill(GameObject target = null);

        public virtual void UseSkill(GameObject target = null)
        {
            IsUsing = true;
        }

        public virtual void StopSkill()
        {
            IsUsing = false;
            _lastUseTime = Time.time;
            OnSkillEnd?.Invoke();
        }
    }
}