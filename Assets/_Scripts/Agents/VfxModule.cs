using System.Collections.Generic;
using System.Linq;
using CoreSystem.Effects;
using GGMLib.ModuleSystem;
using UnityEngine;

namespace Agents
{
    public class VfxModule : MonoBehaviour, IModule
    {
        private ModuleOwner _owner;
        private Dictionary<int, IPlayableVFX> _playableDict;
        public void Initialize(ModuleOwner owner)
        {
            _owner = owner;
            _playableDict = GetComponentsInChildren<IPlayableVFX>()
                .ToDictionary(vfx => vfx.VfxName.AssetHash);
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