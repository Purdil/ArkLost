using UnityEngine;

namespace _Scripts.Boss
{
    public interface IBossSkillSelector
    {
        bool TrySelectSkill(GameObject target,SkillType type, out int skillIndex);
    }
}