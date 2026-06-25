using System;
using _Scripts.CombatSystem;
using UnityEngine;

namespace _Scripts.Boss.BossSkillSystem
{
    [Serializable]
    public class BossSkillWeightEntry
    {
        public SkillDataSO skillData;
        public float baseWeight = 1f;

        public Vector2 validDistanceRange = new(0f, 999f);
        public float repeatPenalty = 0.5f;
        public bool preventImmediateRepeat;
    }

    public enum SkillType
    {
        HARD,
        IMPACT,
        NORMAL
    }
}