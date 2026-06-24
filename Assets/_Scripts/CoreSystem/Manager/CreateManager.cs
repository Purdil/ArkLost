using System;
using _Scripts.CoreSystem.Effects;
using CoreSystem.Effects;
using CoreSystem.Events;
using GGMLib.EventChannelSystem;
using GGMLib.ObjectPool.Runtime;
using UnityEngine;

namespace CoreSystem.Manager
{
    public class CreateManager : MonoBehaviour
    {
        [SerializeField] private GameEventChannelSO createChannel;
        [SerializeField] private PoolManagerSO poolManagerAsset;

        private void Awake()
        {
            createChannel.AddListener<ShowPoolingVfx>(HandleShowPollingVfx);
        }


        private void OnDestroy()
        {
            createChannel.RemoveListener<ShowPoolingVfx>(HandleShowPollingVfx);
        }
        private void HandleShowPollingVfx(ShowPoolingVfx evt)
        {
            PoolableVfx vfx = poolManagerAsset.Pop<PoolableVfx>(evt.ItemData);
            vfx.OnVfxEnd += HandleVfxEnd;
            vfx.PlayVfx(evt.Position,evt.Rotation);
        }

        private void HandleVfxEnd(PoolableVfx targetVfx)
        {
            targetVfx.OnVfxEnd -= HandleVfxEnd;
            poolManagerAsset.Push(targetVfx);
        }
    }
}