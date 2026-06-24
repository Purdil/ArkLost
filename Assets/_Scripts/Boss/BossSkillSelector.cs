using System.Collections.Generic;
using System.Linq;
using _Scripts.CombatSystem;
using _Scripts.Enemies;
using GGMLib.ModuleSystem;
using UnityEngine;

namespace _Scripts.Boss
{
    public class BossSkillSelector : MonoBehaviour, IModule, IBossSkillSelector
    {
        [SerializeField] private BossSkillProfileSO profile;

        private AbstractBoss _boss;
        private ISkillModule _skillModule;
        private int _lastSelectedSkillIndex = -1;
        

        public bool TrySelectSkill(GameObject target,SkillType skillType, out int skillIndex)
        {
            skillIndex = -1;
            List<BossSkillWeightEntry> canUseSkills = new List<BossSkillWeightEntry>();

            canUseSkills = profile.entries.Where(obj => _skillModule.CanUseSkill(obj.skillData.skillIndex, target)
            && obj.skillData.skillType == skillType).ToList();
            BossSkillWeightEntry lastUseSkill = canUseSkills.Find(obj =>  obj.skillData.skillIndex == _lastSelectedSkillIndex);

            if (lastUseSkill != null)
            {
                canUseSkills.Remove(lastUseSkill);
            }
            
            float totalWeight = canUseSkills.Sum(x => x.baseWeight);
            float random = Random.Range(0, totalWeight);
            
            float current = 0;

            foreach(var skill in canUseSkills)
            {
                current += skill.baseWeight;

                if(random <= current)
                {
                    skillIndex = skill.skillData.skillIndex;
                    _lastSelectedSkillIndex = skillIndex;
                    return true;
                }
            }
            
            return false;
        }
        
        public void Initialize(ModuleOwner owner)
        {
            _boss = owner as AbstractBoss;
        }
    }
}