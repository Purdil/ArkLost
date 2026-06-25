using UnityEngine;

namespace _Scripts.Boss.BossSkillSystem
{
    public interface IBossSkillSelector
    {
        bool TrySelectSkill(GameObject target,SkillType type, out int skillIndex);
    }
}