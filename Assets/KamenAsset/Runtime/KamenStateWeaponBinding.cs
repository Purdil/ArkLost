using System;
using UnityEngine;

namespace KamenAsset.Runtime
{
    [Serializable]
    public sealed class KamenStateWeaponBinding
    {
        public string stateName;
        public KamenWeaponAttachmentMode attachmentMode = KamenWeaponAttachmentMode.Hand;
        public bool useAnimatedWeaponMarker;
        public AnimationClip weaponClip;
        public bool sampleWeaponClip;
        public bool applyRootTransformWhileSampling;
    }
}
