using UnityEngine;

namespace CoreSystem.Effects
{
    public interface IPlayableVFX
    {
        AssetNameSO VfxName { get; }
        float VfxDuration { get; }
        void PlayVFX(Vector3 position, Quaternion rotation);
        void PlayVFX();
        void StopVFX();
    }
}