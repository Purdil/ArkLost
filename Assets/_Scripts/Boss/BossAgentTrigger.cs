using System;
using _Scripts.Agents.FSM.AgentTriggers;
using _Scripts.CoreSystem.Effects;
using CoreSystem;
using CoreSystem.Events;

namespace _Scripts.Boss
{
    public class BossAgentTrigger : AgentTrigger, IVFXAgentTrigger, IDamageCastAgentTrigger
    {
        public event Action<AssetNameSO> OnPlayVFXAction;
        public event Action<ShowPoolingVfx> OnShowPoolingVfx;
        public event Action OnDamageCast;
        
        [Obsolete("Animation Event 전용 함수입니다. 직접 호출하지 마세요.")]
        public void ExecutePoolableVFX(ShowVFXInAnimationSO  showVFXInAnimationSo  )
            => OnShowPoolingVfx?.Invoke(CreateEvents.ShowPoolingVfx.InitData(showVFXInAnimationSo.PoolItemSo,
                transform.position + showVFXInAnimationSo.positionOffset ,
                transform.rotation * showVFXInAnimationSo.rotationOffset));

        [Obsolete("Animation Event 전용 함수입니다. 직접 호출하지 마세요.")]
        public void ExecuteVFX(AssetNameSO vfxName) => OnPlayVFXAction?.Invoke(vfxName);
        
        [Obsolete("Animation Event 전용 함수입니다. 직접 호출하지 마세요.")]
        public void DamageCastTrigger() => OnDamageCast?.Invoke();
    }
}