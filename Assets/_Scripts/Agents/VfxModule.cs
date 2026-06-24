using System.Collections.Generic;
using System.Linq;
using _Scripts.CoreSystem.Effects;
using CoreSystem;
using GGMLib.ModuleSystem;
using UnityEngine;

namespace _Scripts.Agents
{
    public class VfxModule : MonoBehaviour, IModule, IAfterInitModule
    {
        private ModuleOwner _owner;
        private Dictionary<int, IPlayableVFX> _playableDict;
        private AgentTrigger _trigger;
        public void Initialize(ModuleOwner owner)
        {
            _owner = owner;
            _playableDict = GetComponentsInChildren<IPlayableVFX>()
                .ToDictionary(vfx => vfx.VfxName.AssetHash);
            _trigger = owner.GetModule<AgentTrigger>();
        }
        
        public void AfterInit()
        {
            _trigger.OnPlayVFXAction += HandlePlayVFX;
        }

        private void OnDestroy()
        {
            _trigger.OnPlayVFXAction -= HandlePlayVFX;
        }

        private void HandlePlayVFX(AssetNameSO so)
        {
            PlayVfx(Animator.StringToHash(so.AssetName));
        }

        public void PlayVfx(int hash, Vector3 position, Quaternion rotation)
        {
            if (_playableDict.TryGetValue(hash, out var vfx))
            {
                vfx.PlayVFX(position, rotation);
            }
            else
            {
                Debug.LogWarning($"VFX with hash : {hash} not found");
            }
        }

        public void PlayVfx(int hash)
        {
            if (_playableDict.TryGetValue(hash, out var vfx))
            {
                vfx.PlayVFX();
            }
            else
            {
                Debug.LogWarning($"VFX with hash : {hash} not found");
            }
        }

        public void StopVfx(int hash)
        {
            if (_playableDict.TryGetValue(hash, out var vfx))
            {
                vfx.StopVFX();
            }
        }
        
    }
}