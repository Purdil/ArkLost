using Agents;
using GGMLib.ModuleSystem;
using UnityEngine;

namespace _Scripts.Agents
{
    [RequireComponent(typeof(Animator))]
    public class AgentRenderer : MonoBehaviour, IModule, IRenderer
    {
        public Animator Animator { get; private set; }
        protected ModuleOwner _owner;
        
        public virtual void Initialize(ModuleOwner owner)
        {
            _owner = owner;
            Animator = GetComponent<Animator>();
        }

        public void PlayClip(int clipHash, float normalizedTime, float crossFadeDuration, int layerIndex = 0)
        {
            //Play, CrossFade, CrossFadeFixedTime
            Animator.CrossFadeInFixedTime(clipHash, crossFadeDuration, layerIndex, normalizedTime);
        }
    }
}