using System;
using GGMLib.ModuleSystem;
using UnityEngine;

namespace _Scripts.CombatSystem
{
    public interface ISkillModule
    {
        ModuleOwner Owner { get; }

        event Action OnCurrentSkillEnd;
        bool CanUseSkill(int skillIndex, GameObject target = null);
        void UseSkill(int skillIndex, GameObject target = null);
        void InvokeSkillEnd();
        void StopSkillIfNotFinished();
    }
}