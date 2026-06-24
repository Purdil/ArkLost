using UnityEngine;
using UnityEngine.VFX;

namespace CoreSystem.Effects
{
    public class PlayGraphVFX : MonoBehaviour, IPlayableVFX
    {
        [field: SerializeField] public AssetNameSO VfxName { get; private set; }
        [field: SerializeField] public float VfxDuration { get; private set; }
        [SerializeField] private VisualEffect[] effects;
        
        public void PlayVFX(Vector3 position, Quaternion rotation)
        {
            transform.SetPositionAndRotation(position, rotation);
            PlayVFX();
        }

        public void PlayVFX()
        {
            foreach (VisualEffect effect in effects)
                effect.Play();
        }

        public void StopVFX()
        {
            foreach (VisualEffect effect in effects)
                effect.Stop();
        }
    }
}