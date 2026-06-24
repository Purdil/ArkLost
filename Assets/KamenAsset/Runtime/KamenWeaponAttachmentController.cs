using System;
using System.Collections.Generic;
using GGMLib.AnimationSystem;
using UnityEngine;

namespace KamenAsset.Runtime
{
    public sealed class KamenWeaponAttachmentController : MonoBehaviour
    {
        private static readonly string[] BuiltInHandStateNames =
        {
            "Idle",
            "Idle_S",
            "Idle_Battle",
            "Idle_Battle_S",
            "Run",
            "Run_S",
            "Alert_02",
            "Alert_02_S"
        };

        private static readonly string[] BuiltInBackStateNames =
        {
            "Idle_Normal",
            "Idle_Normal_M",
            "Walk",
            "Walk_M",
            "Hit",
            "Hit_M",
            "Repel",
            "Repel_M",
            "Alert_01",
            "Alert_01_M"
        };

        private static readonly int[] BuiltInHandStateHashes = BuildStateHashVariants(BuiltInHandStateNames);
        private static readonly int[] BuiltInBackStateHashes = BuildStateHashVariants(BuiltInBackStateNames);
        private static readonly string[] BuiltInSwordRootNames = { "Kamen_SwordSocket_Back", "weapon_l" };
        private static readonly string[] BuiltInAnimatedSwordRootNames = { "wp_mn_cdkcs_00_sk", "mn_cdkcs_00_sk" };
        private static readonly string[] BindingStatePrefixes =
        {
            "mn_cdkcn_00_ani__",
            "mn_cdkcn_00-2_ani__"
        };
        private static readonly int SwordTagHash = Animator.StringToHash("Sword");
        private static readonly int NoSwordTagHash = Animator.StringToHash("NoSword");

        [Header("References")]
        [SerializeField] private Animator animator;
        [SerializeField] private Transform swordRoot;
        [SerializeField] private Transform animatedSwordRoot;
        [SerializeField] private Transform handSocket;
        [SerializeField] private Transform backSocket;

        [Header("Animated Weapon Markers")]
        [SerializeField] private bool followAnimatedWeaponMarkers = true;
        [SerializeField] private bool followAnimatedHandWeaponMarker = true;
        [SerializeField] private bool followAnimatedBackWeaponMarker;
        [SerializeField] private bool parentAnimatedSwordRootToSwordRoot;
        [SerializeField] private bool syncAnimatedSwordRootPose;
        [SerializeField] private Transform handWeaponMarker;
        [SerializeField] private Transform backWeaponMarker;
        [SerializeField] private string swordRootName = "Kamen_SwordSocket_Back";
        [SerializeField] private string animatedSwordRootName = "wp_mn_cdkcs_00_sk";
        [SerializeField] private string handWeaponMarkerName = "b_wpn_02";
        [SerializeField] private string backWeaponMarkerName = "b_wpn_03";

        [Header("Stable Back Socket")]
        [SerializeField] private bool stabilizeBackSocket = true;
        [SerializeField] private string stableBackSocketParentName = "bip001-spine2";
        [SerializeField] private Vector3 stableBackSocketLocalPosition = new Vector3(-0.70248425f, 0.4331342f, 0.5018812f);
        [SerializeField] private Quaternion stableBackSocketLocalRotation = new Quaternion(0.30290583f, -0.6295372f, -0.49681073f, 0.51488835f);
        [SerializeField] private Vector3 stableBackLocalEulerAngles = Vector3.zero;

        [Header("Attachment Stabilization")]
        [SerializeField] private bool lockSocketPoseEveryFrame = true;
        [SerializeField] private bool captureInitialHandOffset = true;
        [SerializeField] private bool captureInitialHandRotation = true;
        [SerializeField] private bool captureInitialHandPosition = true;

        [Header("State Mapping")]
        [SerializeField] private bool forceAttachmentMode;
        [SerializeField] private KamenWeaponAttachmentMode forcedMode = KamenWeaponAttachmentMode.Hand;
        [SerializeField] private bool followAnimatorStateTags = true;
        [SerializeField] private int animatorLayerIndex = -1;
        [SerializeField] private float minimumLayerWeight = 0.01f;
        [SerializeField, Range(0f, 1f)] private float transitionSwitchNormalizedTime = 0.35f;
        [SerializeField] private bool classifyAnimatorClipNames;
        [SerializeField] private bool inferUnknownStateFromSwordPosition = true;
        [SerializeField] private float socketDistanceSwitchTolerance = 0.05f;
        [SerializeField] private KamenWeaponAttachmentMode defaultMode = KamenWeaponAttachmentMode.Back;
        [SerializeField] private KamenWeaponAttachmentMode noSwordStateMode = KamenWeaponAttachmentMode.Back;

        [Header("Anim Param Mapping")]
        [SerializeField] private AnimParamSO[] handAnimParams = Array.Empty<AnimParamSO>();
        [SerializeField] private AnimParamSO[] backAnimParams = Array.Empty<AnimParamSO>();
        [SerializeField] private AnimParamSO[] backClipAnimParams = Array.Empty<AnimParamSO>();
        [SerializeField]
        private string[] backClipNamePatterns = Array.Empty<string>();
        [SerializeField]
        private string[] backStateNames = Array.Empty<string>();
        [SerializeField]
        private string[] handStateNames = Array.Empty<string>();

        [Header("Per-State Weapon Animation")]
        [SerializeField] private bool sampleWeaponAnimationClips;
        [SerializeField] private bool preserveRootTransformWhileSampling = true;
        [SerializeField] private KamenStateWeaponBinding[] stateBindings = Array.Empty<KamenStateWeaponBinding>();

        [Header("Hand Socket Offset")]
        [SerializeField] private Vector3 handLocalPosition = Vector3.zero;
        [SerializeField] private Vector3 handLocalEulerAngles = Vector3.zero;
        [SerializeField] private Vector3 handLocalScale = Vector3.one;

        [Header("Back Socket Offset")]
        [SerializeField] private Vector3 backLocalPosition = Vector3.zero;
        [SerializeField] private Vector3 backLocalEulerAngles = Vector3.zero;
        [SerializeField] private Vector3 backLocalScale = Vector3.one;

        private KamenWeaponAttachmentMode currentMode = (KamenWeaponAttachmentMode)(-1);
        private KamenStateWeaponBinding currentBinding;
        private GameObject swordGameObject;
        private Quaternion cachedHandLocalRotation = Quaternion.identity;
        private Quaternion cachedBackLocalRotation = Quaternion.identity;
        private Quaternion cachedStableBackLocalRotation = Quaternion.identity;
        private int[] handAnimParamHashes = Array.Empty<int>();
        private int[] backAnimParamHashes = Array.Empty<int>();
        private int[] backClipAnimParamHashes = Array.Empty<int>();
        private int[] handStateHashes = Array.Empty<int>();
        private int[] backStateHashes = Array.Empty<int>();
        private int cachedEvaluationLayerIndex = -1;
        private bool runtimeCachesBuilt;
        private bool currentUsesAnimatedWeaponMarker;
        private bool initialHandOffsetCaptured;
        private bool animatedSwordRootDefaultPoseCaptured;
        private Vector3 runtimeHandLocalPosition;
        private Quaternion runtimeHandLocalRotation = Quaternion.identity;
        private Vector3 defaultAnimatedSwordRootLocalPosition;
        private Quaternion defaultAnimatedSwordRootLocalRotation = Quaternion.identity;
        private Vector3 defaultAnimatedSwordRootLocalScale = Vector3.one;
        private Transform stableBackSocket;
        private Transform stableBackSocketParent;

        private readonly Dictionary<int, KamenStateWeaponBinding> stateBindingByHash = new Dictionary<int, KamenStateWeaponBinding>();
        private readonly Dictionary<AnimationClip, KamenWeaponAttachmentMode> clipModeByClip = new Dictionary<AnimationClip, KamenWeaponAttachmentMode>();
        private readonly HashSet<AnimationClip> unresolvedClipModes = new HashSet<AnimationClip>();
        private readonly List<AnimatorClipInfo> clipInfoBuffer = new List<AnimatorClipInfo>(4);

        public Transform SwordRoot => swordRoot;
        public Transform AnimatedSwordRoot => animatedSwordRoot;
        public Transform HandSocket => handSocket;
        public Transform BackSocket => backSocket;
        public KamenWeaponAttachmentMode CurrentMode => currentMode;

        private void Awake()
        {
            ResolveMissingReferences();
            RebuildRuntimeCaches();
        }

        private void OnEnable()
        {
            ResolveMissingReferences();
            RebuildRuntimeCaches();
            currentBinding = null;
            ApplyRequestedAttachment(true);
        }

        private void Start()
        {
            ResolveMissingReferences();
            if (!runtimeCachesBuilt)
                RebuildRuntimeCaches();

            ApplyRequestedAttachment(true);
        }

        private void LateUpdate()
        {
            if (swordRoot == null)
                return;

            if (!forceAttachmentMode && !followAnimatorStateTags && !inferUnknownStateFromSwordPosition)
                return;

            ApplyRequestedAttachment(false);
        }

        private void OnValidate()
        {
            RebuildRuntimeCaches();
        }

        public void ForceSwordToHand()
        {
            forceAttachmentMode = true;
            forcedMode = KamenWeaponAttachmentMode.Hand;
            AttachSwordToHand();
        }

        public void ForceSwordToBack()
        {
            forceAttachmentMode = true;
            forcedMode = KamenWeaponAttachmentMode.Back;
            AttachSwordToBack();
        }

        public void ClearForcedAttachment()
        {
            forceAttachmentMode = false;
            ApplyRequestedAttachment(true);
        }

        public void AttachSwordToHand()
        {
            ApplyAttachment(KamenWeaponAttachmentMode.Hand, true);
        }

        public void AttachSwordToBack()
        {
            ApplyAttachment(KamenWeaponAttachmentMode.Back, true);
        }

        public void HideSword()
        {
            ApplyAttachment(KamenWeaponAttachmentMode.Hidden, true);
        }

        public void ShowSword()
        {
            var mode = IsValidMode(currentMode) && currentMode != KamenWeaponAttachmentMode.Hidden
                ? currentMode
                : GetFallbackAttachmentMode();

            if (mode == KamenWeaponAttachmentMode.Hidden)
                mode = defaultMode;

            ApplyAttachment(mode, true);
        }

        public void SetSwordAttachment(string attachmentName)
        {
            if (string.IsNullOrWhiteSpace(attachmentName))
                return;

            var key = attachmentName.Trim().ToLowerInvariant();
            if (key == "s" || key == "sword" || key == "hand" || key == "drawn" || key == "held")
            {
                AttachSwordToHand();
            }
            else if (key == "m" || key == "back" || key == "shoulder" || key == "sheathed")
            {
                AttachSwordToBack();
            }
            else if (key == "hidden" || key == "hide" || key == "none")
            {
                HideSword();
            }
        }

        private void ResolveMissingReferences()
        {
            if (animator == null)
                animator = GetComponentInChildren<Animator>(true);

            var searchRoot = animator != null ? animator.transform : transform;
            if (swordRoot == null)
                swordRoot = FindFirstChildByName(searchRoot, swordRootName, BuiltInSwordRootNames);

            if (animatedSwordRoot == null)
                animatedSwordRoot = FindFirstChildByName(searchRoot, animatedSwordRootName, BuiltInAnimatedSwordRootNames);

            CaptureAnimatedSwordRootDefaultPose();

            if (followAnimatedWeaponMarkers)
            {
                if (handWeaponMarker == null)
                    handWeaponMarker = FindChildByName(searchRoot, handWeaponMarkerName);

                if (backWeaponMarker == null)
                    backWeaponMarker = FindChildByName(searchRoot, backWeaponMarkerName);
            }

            if (stabilizeBackSocket)
                ResolveStableBackSocket(searchRoot);

            swordGameObject = null;
            CaptureInitialHandOffset();
        }

        private void RebuildRuntimeCaches()
        {
            cachedHandLocalRotation = Quaternion.Euler(handLocalEulerAngles);
            cachedBackLocalRotation = Quaternion.Euler(backLocalEulerAngles);
            cachedStableBackLocalRotation = Quaternion.Euler(stableBackLocalEulerAngles);
            if (!initialHandOffsetCaptured)
            {
                runtimeHandLocalPosition = handLocalPosition;
                runtimeHandLocalRotation = cachedHandLocalRotation;
            }

            handAnimParamHashes = BuildAnimParamHashVariants(handAnimParams);
            backAnimParamHashes = BuildAnimParamHashVariants(backAnimParams);
            backClipAnimParamHashes = BuildAnimParamHashVariants(backClipAnimParams);
            handStateHashes = BuildStateHashVariants(handStateNames);
            backStateHashes = BuildStateHashVariants(backStateNames);

            stateBindingByHash.Clear();
            if (stateBindings != null)
            {
                for (var i = 0; i < stateBindings.Length; i++)
                {
                    var binding = stateBindings[i];
                    if (binding == null || string.IsNullOrWhiteSpace(binding.stateName))
                        continue;

                    AddBindingHashVariants(binding.stateName, binding);
                }
            }

            clipModeByClip.Clear();
            unresolvedClipModes.Clear();
            cachedEvaluationLayerIndex = -1;
            runtimeCachesBuilt = true;
        }

        private void ApplyAttachment(KamenWeaponAttachmentMode mode, bool force)
        {
            ApplyAttachment(mode, force, ShouldUseAnimatedWeaponMarker(mode, null));
        }

        private void ApplyAttachment(KamenWeaponAttachmentMode mode, bool force, bool useAnimatedWeaponMarker)
        {
            var swordObject = GetSwordGameObject();
            if (swordRoot == null || swordObject == null || !IsValidMode(mode))
                return;

            if (mode == KamenWeaponAttachmentMode.Hidden)
            {
                if (force || currentMode != KamenWeaponAttachmentMode.Hidden || swordObject.activeSelf)
                    swordObject.SetActive(false);

                currentMode = KamenWeaponAttachmentMode.Hidden;
                currentUsesAnimatedWeaponMarker = false;
                return;
            }

            var socket = GetAttachmentSocket(mode, useAnimatedWeaponMarker);
            if (socket == null)
                return;

            var parentChanged = swordRoot.parent != socket;
            var activeChanged = !swordObject.activeSelf;
            var sourceChanged = currentUsesAnimatedWeaponMarker != useAnimatedWeaponMarker;
            if (!force && !lockSocketPoseEveryFrame && mode == currentMode && !parentChanged && !activeChanged && !sourceChanged)
                return;

            if (activeChanged)
                swordObject.SetActive(true);

            if (socket == swordRoot)
            {
                ApplyAnimatedSwordRootAttachment();
                currentMode = mode;
                currentUsesAnimatedWeaponMarker = useAnimatedWeaponMarker;
                return;
            }

            if (parentChanged)
                swordRoot.SetParent(socket, false);

            ApplySocketOffset(mode, useAnimatedWeaponMarker);
            ApplyAnimatedSwordRootAttachment();
            currentMode = mode;
            currentUsesAnimatedWeaponMarker = useAnimatedWeaponMarker;
        }

        private void ApplySocketOffset(KamenWeaponAttachmentMode mode, bool useAnimatedWeaponMarker)
        {
            if (mode == KamenWeaponAttachmentMode.Hand)
            {
                swordRoot.localPosition = runtimeHandLocalPosition;
                swordRoot.localRotation = runtimeHandLocalRotation;
                swordRoot.localScale = handLocalScale;
            }
            else
            {
                swordRoot.localPosition = backLocalPosition;
                swordRoot.localRotation = useAnimatedWeaponMarker
                    ? cachedBackLocalRotation
                    : cachedStableBackLocalRotation;
                swordRoot.localScale = backLocalScale;
            }
        }

        private void CaptureInitialHandOffset()
        {
            if (!Application.isPlaying || !captureInitialHandOffset || initialHandOffsetCaptured || swordRoot == null)
                return;

            var handTarget = GetAttachmentSocket(KamenWeaponAttachmentMode.Hand, followAnimatedHandWeaponMarker);
            if (handTarget == null)
                return;

            var backTarget = GetAttachmentSocket(KamenWeaponAttachmentMode.Back, false);
            if (backTarget != null)
            {
                var handDistance = (swordRoot.position - handTarget.position).sqrMagnitude;
                var backDistance = (swordRoot.position - backTarget.position).sqrMagnitude;
                if (backDistance < handDistance)
                    return;
            }

            runtimeHandLocalPosition = captureInitialHandPosition
                ? handTarget.InverseTransformPoint(swordRoot.position)
                : handLocalPosition;
            runtimeHandLocalRotation = captureInitialHandRotation
                ? Quaternion.Inverse(handTarget.rotation) * swordRoot.rotation
                : cachedHandLocalRotation;
            initialHandOffsetCaptured = true;
        }

        private KamenWeaponAttachmentMode GetRequestedMode()
        {
            return GetRequestedMode(out _, out _, out _, out _);
        }

        private KamenWeaponAttachmentMode GetRequestedMode(
            out AnimatorStateInfo stateInfo,
            out KamenStateWeaponBinding binding,
            out int layerIndex,
            out bool useNextState)
        {
            stateInfo = default;
            binding = null;
            layerIndex = 0;
            useNextState = false;

            if (forceAttachmentMode)
                return forcedMode;

            if (!followAnimatorStateTags || animator == null || animator.layerCount == 0)
                return GetFallbackAttachmentMode();

            layerIndex = GetEvaluationLayer();
            useNextState = ShouldUseNextState(layerIndex);
            stateInfo = useNextState
                ? animator.GetNextAnimatorStateInfo(layerIndex)
                : animator.GetCurrentAnimatorStateInfo(layerIndex);

            if (TryResolveBackAttachmentModeOverride(stateInfo, layerIndex, useNextState, out var overrideMode))
                return overrideMode;

            binding = FindStateBinding(stateInfo);

            if (TryResolveAttachmentModeFromAnimParams(stateInfo, out var animParamMode))
                return animParamMode;

            if (binding != null)
                return binding.attachmentMode;

            if (TryResolveAttachmentMode(stateInfo, layerIndex, useNextState, out var mode))
                return mode;

            return GetFallbackAttachmentMode();
        }

        private void ApplyRequestedAttachment(bool force)
        {
            var mode = GetRequestedMode(out var stateInfo, out var binding, out _, out _);
            var bindingChanged = currentBinding != binding;
            currentBinding = binding;
            var useAnimatedWeaponMarker = ShouldUseAnimatedWeaponMarker(mode, binding);

            ApplyAttachment(mode, force || bindingChanged, useAnimatedWeaponMarker);
            SampleWeaponAnimation(stateInfo, binding, mode, useAnimatedWeaponMarker);
        }

        private int GetEvaluationLayer()
        {
            if (animatorLayerIndex >= 0 && animatorLayerIndex < animator.layerCount)
                return animatorLayerIndex;

            if (animator.layerCount <= 1)
                return 0;

            if (cachedEvaluationLayerIndex >= 0 && cachedEvaluationLayerIndex < animator.layerCount)
            {
                for (var i = animator.layerCount - 1; i > cachedEvaluationLayerIndex; i--)
                {
                    if (IsEvaluatableLayer(i))
                    {
                        cachedEvaluationLayerIndex = i;
                        return i;
                    }
                }

                if (IsEvaluatableLayer(cachedEvaluationLayerIndex))
                    return cachedEvaluationLayerIndex;

                for (var i = cachedEvaluationLayerIndex - 1; i >= 0; i--)
                {
                    if (IsEvaluatableLayer(i))
                    {
                        cachedEvaluationLayerIndex = i;
                        return i;
                    }
                }
            }

            for (var i = animator.layerCount - 1; i >= 0; i--)
            {
                if (IsEvaluatableLayer(i))
                {
                    cachedEvaluationLayerIndex = i;
                    return i;
                }
            }

            cachedEvaluationLayerIndex = 0;
            return 0;
        }

        private bool IsEvaluatableLayer(int layerIndex)
        {
            var weight = layerIndex == 0 ? 1f : animator.GetLayerWeight(layerIndex);
            return weight >= minimumLayerWeight && HasAnimatorClipInfo(layerIndex);
        }

        private bool HasAnimatorClipInfo(int layerIndex)
        {
            if (animator.GetCurrentAnimatorClipInfoCount(layerIndex) > 0)
                return true;

            return animator.IsInTransition(layerIndex) && animator.GetNextAnimatorClipInfoCount(layerIndex) > 0;
        }

        private bool ShouldUseNextState(int layerIndex)
        {
            if (!animator.IsInTransition(layerIndex))
                return false;

            var transitionInfo = animator.GetAnimatorTransitionInfo(layerIndex);
            return transitionInfo.normalizedTime >= transitionSwitchNormalizedTime;
        }

        private KamenStateWeaponBinding FindStateBinding(AnimatorStateInfo stateInfo)
        {
            if (stateBindingByHash.Count == 0)
                return null;

            if (stateBindingByHash.TryGetValue(stateInfo.shortNameHash, out var binding))
                return binding;

            if (stateBindingByHash.TryGetValue(stateInfo.fullPathHash, out binding))
                return binding;

            return null;
        }

        private void SampleWeaponAnimation(
            AnimatorStateInfo stateInfo,
            KamenStateWeaponBinding binding,
            KamenWeaponAttachmentMode mode,
            bool useAnimatedWeaponMarker)
        {
            var swordObject = GetSwordGameObject();
            var shouldSample = binding != null
                && binding.weaponClip != null
                && (sampleWeaponAnimationClips || binding.sampleWeaponClip);

            if (!shouldSample || swordRoot == null || swordObject == null)
            {
                ResetAnimatedSwordRootDefaultPose();
                return;
            }

            if (currentMode == KamenWeaponAttachmentMode.Hidden || !swordObject.activeInHierarchy)
            {
                ResetAnimatedSwordRootDefaultPose();
                return;
            }

            var normalizedTime = stateInfo.loop
                ? Mathf.Repeat(stateInfo.normalizedTime, 1f)
                : Mathf.Clamp01(stateInfo.normalizedTime);

            var clipTime = binding.weaponClip.length * normalizedTime;
            ApplyAnimatedSwordRootAttachment();

            var sampleRoot = GetWeaponAnimationRoot();
            var localPosition = sampleRoot.localPosition;
            var localRotation = sampleRoot.localRotation;
            var localScale = sampleRoot.localScale;
            var preserveRootTransform = preserveRootTransformWhileSampling && !binding.applyRootTransformWhileSampling;

            binding.weaponClip.SampleAnimation(swordObject, clipTime);

            if (!preserveRootTransform)
                return;

            sampleRoot.localPosition = localPosition;
            sampleRoot.localRotation = localRotation;
            sampleRoot.localScale = localScale;
            ApplyAnimatedSwordRootAttachment();
        }

        private bool TryResolveAttachmentMode(
            AnimatorStateInfo stateInfo,
            int layerIndex,
            bool useNextState,
            out KamenWeaponAttachmentMode mode)
        {
            if (stateInfo.tagHash == SwordTagHash)
            {
                mode = KamenWeaponAttachmentMode.Hand;
                return true;
            }

            if (stateInfo.tagHash == NoSwordTagHash)
            {
                mode = noSwordStateMode;
                return true;
            }

            if (MatchesAnyStateHash(stateInfo, handStateHashes) || MatchesAnyStateHash(stateInfo, BuiltInHandStateHashes))
            {
                mode = KamenWeaponAttachmentMode.Hand;
                return true;
            }

            if (MatchesAnyStateHash(stateInfo, BuiltInBackStateHashes))
            {
                mode = noSwordStateMode;
                return true;
            }

            if (classifyAnimatorClipNames && TryResolveAttachmentModeFromAnimatorClips(layerIndex, useNextState, out mode))
                return true;

            mode = defaultMode;
            return false;
        }

        private bool TryResolveBackAttachmentModeOverride(
            AnimatorStateInfo stateInfo,
            int layerIndex,
            bool useNextState,
            out KamenWeaponAttachmentMode mode)
        {
            if (MatchesAnyStateHash(stateInfo, backAnimParamHashes) || MatchesAnyStateHash(stateInfo, backClipAnimParamHashes))
            {
                mode = KamenWeaponAttachmentMode.Back;
                return true;
            }

            if (MatchesAnyStateHash(stateInfo, backStateHashes))
            {
                mode = KamenWeaponAttachmentMode.Back;
                return true;
            }

            if (classifyAnimatorClipNames && TryResolveBackAttachmentModeFromAnimatorClips(layerIndex, useNextState))
            {
                mode = KamenWeaponAttachmentMode.Back;
                return true;
            }

            mode = defaultMode;
            return false;
        }

        private bool TryResolveAttachmentModeFromAnimParams(AnimatorStateInfo stateInfo, out KamenWeaponAttachmentMode mode)
        {
            if (MatchesAnyStateHash(stateInfo, handAnimParamHashes))
            {
                mode = KamenWeaponAttachmentMode.Hand;
                return true;
            }

            if (MatchesAnyStateHash(stateInfo, backAnimParamHashes) || MatchesAnyStateHash(stateInfo, backClipAnimParamHashes))
            {
                mode = KamenWeaponAttachmentMode.Back;
                return true;
            }

            mode = defaultMode;
            return false;
        }

        private bool TryResolveBackAttachmentModeFromAnimatorClips(int layerIndex, bool useNextState)
        {
            clipInfoBuffer.Clear();
            if (useNextState && animator.IsInTransition(layerIndex))
                animator.GetNextAnimatorClipInfo(layerIndex, clipInfoBuffer);
            else
                animator.GetCurrentAnimatorClipInfo(layerIndex, clipInfoBuffer);

            for (var i = 0; i < clipInfoBuffer.Count; i++)
            {
                var clip = clipInfoBuffer[i].clip;
                if (clip != null
                    && (MatchesAnyNamePattern(clip.name, backClipNamePatterns) || IsBackOnlyClipName(clip.name)))
                    return true;
            }

            return false;
        }

        private bool TryResolveAttachmentModeFromAnimatorClips(
            int layerIndex,
            bool useNextState,
            out KamenWeaponAttachmentMode mode)
        {
            clipInfoBuffer.Clear();
            if (useNextState && animator.IsInTransition(layerIndex))
                animator.GetNextAnimatorClipInfo(layerIndex, clipInfoBuffer);
            else
                animator.GetCurrentAnimatorClipInfo(layerIndex, clipInfoBuffer);

            var handWeight = 0f;
            var backWeight = 0f;

            for (var i = 0; i < clipInfoBuffer.Count; i++)
            {
                var clipInfo = clipInfoBuffer[i];
                if (!TryGetAttachmentModeFromClip(clipInfo.clip, out var clipMode))
                    continue;

                var weight = Mathf.Max(clipInfo.weight, 0.001f);
                if (clipMode == KamenWeaponAttachmentMode.Hand)
                    handWeight += weight;
                else if (clipMode == KamenWeaponAttachmentMode.Back)
                    backWeight += weight;
            }

            if (handWeight > backWeight)
            {
                mode = KamenWeaponAttachmentMode.Hand;
                return true;
            }

            if (backWeight > handWeight)
            {
                mode = noSwordStateMode;
                return true;
            }

            mode = defaultMode;
            return false;
        }

        private KamenWeaponAttachmentMode GetFallbackAttachmentMode()
        {
            if (inferUnknownStateFromSwordPosition && TryInferAttachmentModeFromSwordPosition(out var inferredMode))
                return inferredMode;

            if (IsValidMode(currentMode))
                return currentMode;

            return defaultMode;
        }

        private bool TryInferAttachmentModeFromSwordPosition(out KamenWeaponAttachmentMode mode)
        {
            mode = defaultMode;

            if (swordRoot == null)
                return false;

            var swordObject = GetSwordGameObject();
            if (swordObject == null)
                return false;

            if (!swordObject.activeSelf && currentMode == KamenWeaponAttachmentMode.Hidden)
            {
                mode = KamenWeaponAttachmentMode.Hidden;
                return true;
            }

            var handTarget = GetAttachmentSocket(
                KamenWeaponAttachmentMode.Hand,
                ShouldUseAnimatedWeaponMarker(KamenWeaponAttachmentMode.Hand, currentBinding));
            var backTarget = GetAttachmentSocket(
                KamenWeaponAttachmentMode.Back,
                ShouldUseAnimatedWeaponMarker(KamenWeaponAttachmentMode.Back, currentBinding));

            if (handTarget == null && backTarget == null)
                return false;

            if (handTarget == null)
            {
                mode = KamenWeaponAttachmentMode.Back;
                return true;
            }

            if (backTarget == null)
            {
                mode = KamenWeaponAttachmentMode.Hand;
                return true;
            }

            var handDelta = swordRoot.position - handTarget.position;
            var backDelta = swordRoot.position - backTarget.position;
            var handDistanceSqr = handDelta.sqrMagnitude;
            var backDistanceSqr = backDelta.sqrMagnitude;
            if (socketDistanceSwitchTolerance > 0f && IsValidMode(currentMode))
            {
                var handDistance = Mathf.Sqrt(handDistanceSqr);
                var backDistance = Mathf.Sqrt(backDistanceSqr);
                if (Mathf.Abs(handDistance - backDistance) <= socketDistanceSwitchTolerance)
                {
                    mode = currentMode;
                    return true;
                }
            }

            if (socketDistanceSwitchTolerance <= 0f && Mathf.Approximately(handDistanceSqr, backDistanceSqr) && IsValidMode(currentMode))
            {
                mode = currentMode;
                return true;
            }

            mode = handDistanceSqr <= backDistanceSqr ? KamenWeaponAttachmentMode.Hand : KamenWeaponAttachmentMode.Back;
            return true;
        }

        private bool TryGetAttachmentModeFromClip(AnimationClip clip, out KamenWeaponAttachmentMode mode)
        {
            mode = defaultMode;
            if (clip == null || string.IsNullOrWhiteSpace(clip.name))
                return false;

            if (clipModeByClip.TryGetValue(clip, out mode))
                return true;

            if (unresolvedClipModes.Contains(clip))
                return false;

            if (TryResolveAttachmentModeFromAnimParamName(clip.name, out mode))
            {
                clipModeByClip.Add(clip, mode);
                return true;
            }

            if (TryResolveAttachmentModeFromName(clip.name, out mode))
            {
                clipModeByClip.Add(clip, mode);
                return true;
            }

            unresolvedClipModes.Add(clip);
            return false;
        }

        private bool TryResolveAttachmentModeFromAnimParamName(string name, out KamenWeaponAttachmentMode mode)
        {
            var hashVariants = BuildNameHashVariants(name);
            if (MatchesAnyHash(hashVariants, handAnimParamHashes))
            {
                mode = KamenWeaponAttachmentMode.Hand;
                return true;
            }

            if (MatchesAnyHash(hashVariants, backAnimParamHashes) || MatchesAnyHash(hashVariants, backClipAnimParamHashes))
            {
                mode = KamenWeaponAttachmentMode.Back;
                return true;
            }

            mode = defaultMode;
            return false;
        }

        private bool TryResolveAttachmentModeFromName(string name, out KamenWeaponAttachmentMode mode)
        {
            if (MatchesAnyNamePattern(name, backClipNamePatterns))
            {
                mode = KamenWeaponAttachmentMode.Back;
                return true;
            }

            if (IsBackOnlyClipName(name))
            {
                mode = KamenWeaponAttachmentMode.Back;
                return true;
            }

            if (HasModeSuffix(name, "_S"))
            {
                mode = KamenWeaponAttachmentMode.Hand;
                return true;
            }

            if (HasModeSuffix(name, "_M"))
            {
                mode = KamenWeaponAttachmentMode.Back;
                return true;
            }

            if (ContainsToken(name, "battle") || ContainsToken(name, "attack") || ContainsToken(name, "att"))
            {
                mode = KamenWeaponAttachmentMode.Hand;
                return true;
            }

            mode = KamenWeaponAttachmentMode.Back;
            return false;
        }

        private static bool IsBackOnlyClipName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return false;

            var normalizedName = NormalizeClipName(name);
            return ContainsToken(normalizedName, "att_battle_1_01")
                || ContainsToken(normalizedName, "status")
                || ContainsToken(normalizedName, "normal")
                || ContainsToken(normalizedName, "dmg")
                || ContainsToken(normalizedName, "dead")
                || ContainsToken(normalizedName, "evt")
                || ContainsToken(normalizedName, "sit")
                || ContainsToken(normalizedName, "hld")
                || ContainsToken(normalizedName, "str");
        }

        private static bool MatchesAnyNamePattern(string name, string[] patterns)
        {
            if (string.IsNullOrWhiteSpace(name) || patterns == null)
                return false;

            for (var i = 0; i < patterns.Length; i++)
            {
                var pattern = patterns[i];
                if (string.IsNullOrWhiteSpace(pattern))
                    continue;

                if (name.IndexOf(pattern, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            return false;
        }

        private static bool MatchesAnyStateHash(AnimatorStateInfo stateInfo, int[] hashes)
        {
            if (hashes == null)
                return false;

            for (var i = 0; i < hashes.Length; i++)
            {
                var hash = hashes[i];
                if (stateInfo.shortNameHash == hash || stateInfo.fullPathHash == hash)
                    return true;
            }

            return false;
        }

        private static bool MatchesAnyHash(List<int> sourceHashes, int[] targetHashes)
        {
            if (sourceHashes == null || targetHashes == null)
                return false;

            for (var i = 0; i < sourceHashes.Count; i++)
            {
                var sourceHash = sourceHashes[i];
                for (var j = 0; j < targetHashes.Length; j++)
                {
                    if (sourceHash == targetHashes[j])
                        return true;
                }
            }

            return false;
        }

        private void AddBindingHashVariants(string stateName, KamenStateWeaponBinding binding)
        {
            var variants = BuildBindingStateNameVariants(stateName);
            for (var i = 0; i < variants.Count; i++)
            {
                var variant = variants[i];
                AddBindingHash(Animator.StringToHash(variant), binding);
                AddBindingHash(Animator.StringToHash("Base Layer." + variant), binding);

                var dottedName = variant.Replace('/', '.');
                if (!string.Equals(dottedName, variant, StringComparison.Ordinal))
                {
                    AddBindingHash(Animator.StringToHash(dottedName), binding);
                    AddBindingHash(Animator.StringToHash("Base Layer." + dottedName), binding);
                }
            }
        }

        private static List<string> BuildBindingStateNameVariants(string stateName)
        {
            var variants = new List<string>(16);
            if (string.IsNullOrWhiteSpace(stateName))
                return variants;

            var normalizedName = NormalizeClipName(stateName);
            AddNameVariant(variants, normalizedName);

            var strippedName = StripBindingModeSuffix(normalizedName);
            AddNameVariant(variants, strippedName);

            var tailName = GetAnimationTailName(strippedName);
            AddNameVariant(variants, tailName);

            for (var i = 0; i < BindingStatePrefixes.Length; i++)
            {
                AddNameVariant(variants, BindingStatePrefixes[i] + tailName);
            }

            return variants;
        }

        private static void AddNameVariant(List<string> variants, string variant)
        {
            if (string.IsNullOrWhiteSpace(variant))
                return;

            if (!variants.Contains(variant))
                variants.Add(variant);
        }

        private void AddBindingHash(int hash, KamenStateWeaponBinding binding)
        {
            if (!stateBindingByHash.ContainsKey(hash))
                stateBindingByHash.Add(hash, binding);
        }

        private void ApplyAnimatedSwordRootAttachment()
        {
            if (animatedSwordRoot == null || swordRoot == null || animatedSwordRoot == swordRoot)
                return;

            if (parentAnimatedSwordRootToSwordRoot)
            {
                if (animatedSwordRoot.parent != swordRoot)
                    animatedSwordRoot.SetParent(swordRoot, false);

                animatedSwordRoot.localPosition = Vector3.zero;
                animatedSwordRoot.localRotation = Quaternion.identity;
                animatedSwordRoot.localScale = Vector3.one;
                return;
            }

            if (!syncAnimatedSwordRootPose)
                return;

            animatedSwordRoot.position = swordRoot.position;
            animatedSwordRoot.rotation = swordRoot.rotation;
        }

        private Transform GetWeaponAnimationRoot()
        {
            return animatedSwordRoot != null ? animatedSwordRoot : swordRoot;
        }

        private GameObject GetSwordGameObject()
        {
            var visibleRoot = GetWeaponAnimationRoot();
            if (visibleRoot == null)
                return null;

            if (swordGameObject == null || swordGameObject.transform != visibleRoot)
                swordGameObject = visibleRoot.gameObject;

            return swordGameObject;
        }

        private void CaptureAnimatedSwordRootDefaultPose()
        {
            if (animatedSwordRoot == null || animatedSwordRootDefaultPoseCaptured)
                return;

            defaultAnimatedSwordRootLocalPosition = animatedSwordRoot.localPosition;
            defaultAnimatedSwordRootLocalRotation = animatedSwordRoot.localRotation;
            defaultAnimatedSwordRootLocalScale = animatedSwordRoot.localScale;
            animatedSwordRootDefaultPoseCaptured = true;
        }

        private void ResetAnimatedSwordRootDefaultPose()
        {
            if (animatedSwordRoot == null || !animatedSwordRootDefaultPoseCaptured)
                return;

            animatedSwordRoot.localPosition = defaultAnimatedSwordRootLocalPosition;
            animatedSwordRoot.localRotation = defaultAnimatedSwordRootLocalRotation;
            animatedSwordRoot.localScale = defaultAnimatedSwordRootLocalScale;
        }

        private bool ShouldUseAnimatedWeaponMarker(KamenWeaponAttachmentMode mode, KamenStateWeaponBinding binding)
        {
            if (!followAnimatedWeaponMarkers)
                return false;

            if (binding != null && binding.useAnimatedWeaponMarker)
                return true;

            if (mode == KamenWeaponAttachmentMode.Hand)
                return followAnimatedHandWeaponMarker;

            if (mode == KamenWeaponAttachmentMode.Back)
                return followAnimatedBackWeaponMarker;

            return false;
        }

        private Transform GetAttachmentSocket(KamenWeaponAttachmentMode mode, bool useAnimatedWeaponMarker)
        {
            if (mode == KamenWeaponAttachmentMode.Hand)
            {
                if (useAnimatedWeaponMarker && handWeaponMarker != null)
                    return handWeaponMarker;

                return handSocket;
            }

            if (mode == KamenWeaponAttachmentMode.Back)
            {
                if (useAnimatedWeaponMarker && backWeaponMarker != null)
                    return backWeaponMarker;

                if (stableBackSocket != null)
                    return stableBackSocket;

                return backSocket;
            }

            return null;
        }

        private void ResolveStableBackSocket(Transform searchRoot)
        {
            if (stableBackSocketParent == null)
                stableBackSocketParent = FindChildByName(searchRoot, stableBackSocketParentName);

            if (stableBackSocketParent == null && backWeaponMarker != null)
                stableBackSocketParent = backWeaponMarker.parent;

            if (stableBackSocketParent == null)
                return;

            if (stableBackSocket == null)
            {
                var socketObject = new GameObject("Kamen_RuntimeStableBackSocket");
                stableBackSocket = socketObject.transform;
                stableBackSocket.SetParent(stableBackSocketParent, false);
                stableBackSocket.localPosition = stableBackSocketLocalPosition;
                stableBackSocket.localRotation = stableBackSocketLocalRotation;
                stableBackSocket.localScale = Vector3.one;
                return;
            }

            if (stableBackSocket.parent == stableBackSocketParent)
            {
                stableBackSocket.localPosition = stableBackSocketLocalPosition;
                stableBackSocket.localRotation = stableBackSocketLocalRotation;
                stableBackSocket.localScale = Vector3.one;
                return;
            }

            stableBackSocket.SetParent(stableBackSocketParent, false);
            stableBackSocket.localPosition = stableBackSocketLocalPosition;
            stableBackSocket.localRotation = stableBackSocketLocalRotation;
            stableBackSocket.localScale = Vector3.one;
        }

        private static Transform FindFirstChildByName(Transform root, string preferredName, string[] fallbackNames)
        {
            var transform = FindChildByName(root, preferredName);
            if (transform != null)
                return transform;

            if (fallbackNames == null)
                return null;

            for (var i = 0; i < fallbackNames.Length; i++)
            {
                transform = FindChildByName(root, fallbackNames[i]);
                if (transform != null)
                    return transform;
            }

            return null;
        }

        private static Transform FindChildByName(Transform root, string childName)
        {
            if (root == null || string.IsNullOrWhiteSpace(childName))
                return null;

            var children = root.GetComponentsInChildren<Transform>(true);
            for (var i = 0; i < children.Length; i++)
            {
                var child = children[i];
                if (string.Equals(child.name, childName, StringComparison.Ordinal))
                    return child;
            }

            return null;
        }

        private static int[] BuildStateHashVariants(string[] stateNames)
        {
            if (stateNames == null || stateNames.Length == 0)
                return Array.Empty<int>();

            var hashes = new List<int>(stateNames.Length * 4);
            for (var i = 0; i < stateNames.Length; i++)
            {
                var stateName = stateNames[i];
                if (string.IsNullOrWhiteSpace(stateName))
                    continue;

                AddStateHash(hashes, Animator.StringToHash(stateName));
                AddStateHash(hashes, Animator.StringToHash("Base Layer." + stateName));

                var dottedName = stateName.Replace('/', '.');
                if (!string.Equals(dottedName, stateName, StringComparison.Ordinal))
                {
                    AddStateHash(hashes, Animator.StringToHash(dottedName));
                    AddStateHash(hashes, Animator.StringToHash("Base Layer." + dottedName));
                }
            }

            return hashes.Count > 0 ? hashes.ToArray() : Array.Empty<int>();
        }

        private static int[] BuildAnimParamHashVariants(AnimParamSO[] animParams)
        {
            if (animParams == null || animParams.Length == 0)
                return Array.Empty<int>();

            var hashes = new List<int>(animParams.Length * 8);
            for (var i = 0; i < animParams.Length; i++)
            {
                var animParam = animParams[i];
                if (animParam == null || string.IsNullOrWhiteSpace(animParam.ParamName))
                    continue;

                var variants = BuildBindingStateNameVariants(animParam.ParamName);
                for (var j = 0; j < variants.Count; j++)
                    AddStateHash(hashes, Animator.StringToHash(variants[j]));

                AddStateHash(hashes, animParam.ParamHash != 0
                    ? animParam.ParamHash
                    : Animator.StringToHash(animParam.ParamName));
            }

            return hashes.Count > 0 ? hashes.ToArray() : Array.Empty<int>();
        }

        private static List<int> BuildNameHashVariants(string name)
        {
            var nameVariants = BuildBindingStateNameVariants(name);
            var hashes = new List<int>(nameVariants.Count);
            for (var i = 0; i < nameVariants.Count; i++)
                AddStateHash(hashes, Animator.StringToHash(nameVariants[i]));

            return hashes;
        }

        private static void AddStateHash(List<int> hashes, int hash)
        {
            if (!hashes.Contains(hash))
                hashes.Add(hash);
        }

        private static bool HasModeSuffix(string name, string suffix)
        {
            return name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)
                || name.IndexOf(suffix + "_", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string StripBindingModeSuffix(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return string.Empty;

            var result = name.Trim();
            result = StripTrailingNumericVariant(result);

            if (result.EndsWith("_S", StringComparison.OrdinalIgnoreCase)
                || result.EndsWith("_M", StringComparison.OrdinalIgnoreCase))
            {
                result = result.Substring(0, result.Length - 2);
            }

            return result;
        }

        private static string StripTrailingNumericVariant(string name)
        {
            var index = name.LastIndexOf('_');
            if (index < 0 || index == name.Length - 1)
                return name;

            var suffix = name.Substring(index + 1);
            for (var i = 0; i < suffix.Length; i++)
            {
                if (!char.IsDigit(suffix[i]))
                    return name;
            }

            var prefix = name.Substring(0, index);
            if (prefix.EndsWith("_S", StringComparison.OrdinalIgnoreCase)
                || prefix.EndsWith("_M", StringComparison.OrdinalIgnoreCase))
                return prefix;

            return name;
        }

        private static string GetAnimationTailName(string name)
        {
            var marker = name.IndexOf("_ani__", StringComparison.OrdinalIgnoreCase);
            if (marker < 0)
                return name;

            return name.Substring(marker + "_ani__".Length);
        }

        private static string NormalizeClipName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return string.Empty;

            var normalizedName = name.Trim();
            var separator = normalizedName.LastIndexOf('|');
            if (separator >= 0 && separator < normalizedName.Length - 1)
                normalizedName = normalizedName.Substring(separator + 1);

            return normalizedName;
        }

        private static bool ContainsToken(string name, string token)
        {
            return name.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsValidMode(KamenWeaponAttachmentMode mode)
        {
            return mode == KamenWeaponAttachmentMode.Hand
                || mode == KamenWeaponAttachmentMode.Back
                || mode == KamenWeaponAttachmentMode.Hidden;
        }
    }
}
