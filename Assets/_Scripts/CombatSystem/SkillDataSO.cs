using UnityEngine;

namespace _Scripts.CombatSystem
{
    [CreateAssetMenu(fileName = "Skill data", menuName = "Agent/Skill data", order = 25)]
    public class SkillDataSO : ScriptableObject
    {
        public int skillIndex;
        public string skillName;
        public float cooldown;
        //나중에 데미지 계수 등 여러가지 요소가 들어간다.
        public float skillRange = 1f;
        public float damageMultiplier = 1f;
        public float damage = 5;
        
    }
}