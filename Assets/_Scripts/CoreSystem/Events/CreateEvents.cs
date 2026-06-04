using GGMLib.EventChannelSystem;
using GGMLib.ObjectPool.Runtime;
using UnityEngine;

namespace CoreSystem.Events
{
    public static class CreateEvents
    {
        public static readonly ShowPoolingVfx ShowPoolingVfx = new ShowPoolingVfx();
    }

    public class ShowPoolingVfx : GameEvent
    {
        public PoolItemSO ItemData { get; private set; }
        public Vector3 Position { get; private set; }
        public Quaternion Rotation { get; private set; }

        public ShowPoolingVfx InitData(PoolItemSO itemData, Vector3 position, Quaternion rotation)
        {
            ItemData = itemData;
            Position = position;
            Rotation = rotation;
            return this;
        }
    }
}