using UnityEngine;

namespace GGMLib.AnimationSystem
{
    [CreateAssetMenu(fileName = "Animator param", menuName = "Lib/Animator param", order = 10)]
    public class AnimParamSO : ScriptableObject
    {
        [field: SerializeField] public string ParamName { get; private set; }
        [field: SerializeField] public int ParamHash { get; private set; }

        private void OnValidate()
        {
            ParamHash = Animator.StringToHash(ParamName);
        }
    }
}