using System;
using _Scripts.CoreSystem.Effects;
using CoreSystem;
using CoreSystem.Events;
using GGMLib.ModuleSystem;
using GGMLib.ObjectPool.Runtime;
using UnityEngine;

namespace _Scripts.Agents
{
    public class AgentTrigger : MonoBehaviour, IModule
    {
        public event Action OnAnimationEnd;
        public event Action OnLinkTimeEnd;
        public bool CanManualMovement { get; set; }
        public event Action OnDamageCast;
        public event Action<AssetNameSO> OnPlayVFXAction;
        public event Action<ShowPoolingVfx> OnShowPoolingVfx;
        public void Initialize(ModuleOwner owner)
        {
          
        }

        private void AnimationEndTrigger()
        {
            CanManualMovement = false;
            OnAnimationEnd?.Invoke();
        }

        private void ExecutePoolableVFX(ShowVFXInAnimationSO  showVFXInAnimationSo  )
        => OnShowPoolingVfx?.Invoke(CreateEvents.ShowPoolingVfx.InitData(showVFXInAnimationSo.PoolItemSo,
            transform.position + showVFXInAnimationSo.positionOffset ,
            transform.rotation * showVFXInAnimationSo.rotationOffset));
            
        
        private void ExecuteVFX(AssetNameSO vfxName) => OnPlayVFXAction?.Invoke(vfxName);
        private void DamageCastTrigger() => OnDamageCast?.Invoke();
        private void LinkTimeTrigger() => OnLinkTimeEnd?.Invoke();
        private void CanManualMovementTrigger() => CanManualMovement = true;
    }
}