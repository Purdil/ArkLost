using System;
using _Scripts.CoreSystem.Effects;
using CoreSystem;
using CoreSystem.Events;

public interface IVFXAgentTrigger
{
    event Action<AssetNameSO> OnPlayVFXAction;
    event Action<ShowPoolingVfx> OnShowPoolingVfx;
    
    [Obsolete("Animation Event 전용 함수입니다. 직접 호출하지 마세요.")]
    void ExecutePoolableVFX(ShowVFXInAnimationSO  showVFXInAnimationSo  );
    [Obsolete("Animation Event 전용 함수입니다. 직접 호출하지 마세요.")]
    void ExecuteVFX(AssetNameSO vfxName);
    /*
     * [Obsolete("Animation Event 전용 함수입니다. 직접 호출하지 마세요.")]
       public void ExecutePoolableVFX(ShowVFXInAnimationSO  showVFXInAnimationSo  )
        => OnShowPoolingVfx?.Invoke(CreateEvents.ShowPoolingVfx.InitData(showVFXInAnimationSo.PoolItemSo,
            transform.position + showVFXInAnimationSo.positionOffset ,
            transform.rotation * showVFXInAnimationSo.rotationOffset));

        [Obsolete("Animation Event 전용 함수입니다. 직접 호출하지 마세요.")]
        public void ExecuteVFX(AssetNameSO vfxName) => OnPlayVFXAction?.Invoke(vfxName);
     */
}