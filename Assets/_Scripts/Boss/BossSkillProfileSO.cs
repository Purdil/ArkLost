using System.Collections.Generic;
using UnityEngine;

namespace _Scripts.Boss
{
    [CreateAssetMenu(fileName = "BossSkillProfileSO", menuName = "Boss/SkillProfile", order = 0)]
    public class BossSkillProfileSO : ScriptableObject
    {
        public List<BossSkillWeightEntry> entries;
    }
}