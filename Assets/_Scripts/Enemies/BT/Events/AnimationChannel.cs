using System;
using GGMLib.AnimationSystem;
using Unity.Behavior;
using Unity.Properties;
using UnityEngine;

namespace Enemies.BT.Events
{
#if UNITY_EDITOR
    [CreateAssetMenu(menuName = "Behavior/Event Channels/AnimationChannel")]
#endif
    [Serializable, GeneratePropertyBag]
    [EventChannelDescription(name: "AnimationChannel", message: "Play [Clip]", category: "Events", id: "e6c5d3936a5371403ee37f31e838b973")]
    public sealed partial class AnimationChannel : EventChannel<AnimParamSO> { }
}

