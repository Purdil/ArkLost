using System;
using Unity.Behavior;
using Unity.Properties;
using UnityEngine;

namespace Enemies.BT.Events
{
#if UNITY_EDITOR
    [CreateAssetMenu(menuName = "Behavior/Event Channels/StateChannel")]
#endif
    [Serializable, GeneratePropertyBag]
    [EventChannelDescription(name: "StateChannel", message: "Change [State]", category: "Events", id: "3605666e5ddf08b123d7198498444419")]
    public sealed partial class StateChannel : EventChannel<EnemyState> { }
}