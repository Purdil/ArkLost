using _Scripts.CombatSystem;
using UnityEngine;

namespace _Scripts.Boss.BossSkillSystem.BossSkills
{
    [CreateAssetMenu(fileName = "BossSkill", menuName = "BossSkillData", order = 0)]
    public class BossSkillData : SkillDataSO
    {
        public RangeRenderData[] rangeRenderData;
    }
}