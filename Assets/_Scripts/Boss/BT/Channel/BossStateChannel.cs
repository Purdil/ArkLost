using _Scripts.Boss;
using System;
using Unity.Behavior;
using UnityEngine;
using Unity.Properties;

#if UNITY_EDITOR
[CreateAssetMenu(menuName = "Behavior/Event Channels/BossStateChannel")]
#endif
[Serializable, GeneratePropertyBag]
[EventChannelDescription(name: "BossStateChannel", message: "Change [State]", category: "Events", id: "ef578f30d0472db453a75952dcab6409")]
public sealed partial class BossStateChannel : EventChannel<BossState> { }

