using GGMLib.AnimationSystem;
using UnityEngine;

namespace Agents.FSM
{
    [CreateAssetMenu(fileName = "State data", menuName = "Agent/State data", order = 0)]
    public class StateSO : ScriptableObject
    {
        public string stateName;
        public string className;
        public int assetIndex;
        public AnimParamSO stateParam;
    }
}