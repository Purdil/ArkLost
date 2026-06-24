using System.Collections;
using System.Collections.Generic;
using Agents;
using UnityEngine;
using UnityEngine.Rendering;

namespace _Scripts.Boss
{
    [DisallowMultipleComponent]
    public class HitWrap : MonoBehaviour
    {
        private static readonly int HitWrapColorId = Shader.PropertyToID("_HitWrapColor");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int RimColorId = Shader.PropertyToID("_RimColor");
        private static readonly int ShellWidthId = Shader.PropertyToID("_ShellWidth");
        private static readonly int RimPowerId = Shader.PropertyToID("_RimPower");
        private static readonly int RimIntensityId = Shader.PropertyToID("_RimIntensity");
        private static readonly int NoiseScaleId = Shader.PropertyToID("_NoiseScale");
        private static readonly int NoiseStrengthId = Shader.PropertyToID("_NoiseStrength");
        private static readonly int OutlineIntensityId = Shader.PropertyToID("_OutlineIntensity");
        private static readonly int DistortionScaleId = Shader.PropertyToID("_DistortionScale");
        private static readonly int DistortionStrengthId = Shader.PropertyToID("_DistortionStrength");
        private static readonly int PulseSpeedId = Shader.PropertyToID("_PulseSpeed");
        private const string OverlayObjectPrefix = "[HitWrap] ";

        [Header("Target")]
        [SerializeField] private Transform targetRoot;
        [SerializeField] private bool includeInactiveRenderers;
        [SerializeField] private bool autoPlayOnOwnerHit = true;
        [SerializeField] private bool visibleOnEnable;

        [Header("Look")]
        [SerializeField] private Color wrapColor = new Color(0.82f, 0.96f, 1f, 1f);
        [SerializeField, Range(0f, 1f)] private float wrapAlpha = 0.24f;
        [SerializeField] private Color rimColor = new Color(0.92f, 0.98f, 1f, 1f);
        [SerializeField, Range(0f, 1f)] private float rimAlpha = 0.62f;
        [SerializeField, Min(0f)] private float shellWidth = 0.018f;
        [SerializeField, Range(0.5f, 8f)] private float rimPower = 2.2f;
        [SerializeField, Range(0f, 4f)] private float rimIntensity = 1.4f;
        [SerializeField, Range(0f, 1f)] private float outlineIntensity = 0.38f;

        [Header("Texture")]
        [SerializeField, Range(0.1f, 60f)] private float noiseScale = 18f;
        [SerializeField, Range(0f, 1f)] private float noiseStrength = 0.42f;
        [SerializeField, Range(0.1f, 40f)] private float distortionScale = 7f;
        [SerializeField, Range(0f, 1f)] private float distortionStrength = 0.24f;
        [SerializeField, Range(0f, 20f)] private float pulseSpeed = 8f;

        [Header("Timing")]
        [SerializeField, Min(0.01f)] private float autoHideDuration = 0.12f;

        private readonly List<GameObject> overlayObjects = new List<GameObject>();
        private readonly List<Renderer> overlayRenderers = new List<Renderer>();
        private readonly List<Material> repeatedMaterials = new List<Material>();

        private Agent ownerAgent;
        private Coroutine hideRoutine;
        private Material overlayMaterial;
        private MaterialPropertyBlock propertyBlock;
        private bool isVisible;

        public Color WrapColor
        {
            get => wrapColor;
            set
            {
                wrapColor = value;
                ApplyOverlayProperties();
            }
        }

        public float WrapAlpha
        {
            get => wrapAlpha;
            set
            {
                wrapAlpha = Mathf.Clamp01(value);
                ApplyOverlayProperties();
            }
        }

        public float ShellWidth
        {
            get => shellWidth;
            set
            {
                shellWidth = Mathf.Max(0f, value);
                ApplyOverlayProperties();
            }
        }

        public Color RimColor
        {
            get => rimColor;
            set
            {
                rimColor = value;
                ApplyOverlayProperties();
            }
        }

        public float RimAlpha
        {
            get => rimAlpha;
            set
            {
                rimAlpha = Mathf.Clamp01(value);
                ApplyOverlayProperties();
            }
        }

        private void Awake()
        {
            if (targetRoot == null)
            {
                targetRoot = transform;
            }

            ownerAgent = GetComponentInParent<Agent>();
            BuildOverlays();
            SetVisible(visibleOnEnable);
        }

        private void OnEnable()
        {
            if (ownerAgent == null)
            {
                ownerAgent = GetComponentInParent<Agent>();
            }

            if (autoPlayOnOwnerHit && ownerAgent != null)
            {
                ownerAgent.OnHit.AddListener(ShowForDefaultDuration);
            }

            SetVisible(visibleOnEnable);
        }

        private void OnDisable()
        {
            if (ownerAgent != null)
            {
                ownerAgent.OnHit.RemoveListener(ShowForDefaultDuration);
            }

            StopAutoHideRoutine();
            SetVisible(false);
        }

        private void OnDestroy()
        {
            StopAutoHideRoutine();
            DestroyOverlays();
            DestroyGenerated(overlayMaterial);
        }

        private void OnValidate()
        {
            wrapAlpha = Mathf.Clamp01(wrapAlpha);
            rimAlpha = Mathf.Clamp01(rimAlpha);
            shellWidth = Mathf.Max(0f, shellWidth);
            rimPower = Mathf.Clamp(rimPower, 0.5f, 8f);
            rimIntensity = Mathf.Clamp(rimIntensity, 0f, 4f);
            outlineIntensity = Mathf.Clamp01(outlineIntensity);
            noiseScale = Mathf.Clamp(noiseScale, 0.1f, 60f);
            noiseStrength = Mathf.Clamp01(noiseStrength);
            distortionScale = Mathf.Clamp(distortionScale, 0.1f, 40f);
            distortionStrength = Mathf.Clamp01(distortionStrength);
            pulseSpeed = Mathf.Clamp(pulseSpeed, 0f, 20f);
            autoHideDuration = Mathf.Max(0.01f, autoHideDuration);
            ApplyOverlayProperties();
        }

        [ContextMenu("Refresh Renderers")]
        public void RefreshRenderers()
        {
            DestroyOverlays();
            BuildOverlays();
            SetVisible(isVisible);
        }

        [ContextMenu("Show")]
        public void Show()
        {
            StopAutoHideRoutine();
            SetVisible(true);
        }

        [ContextMenu("Hide")]
        public void Hide()
        {
            StopAutoHideRoutine();
            SetVisible(false);
        }

        [ContextMenu("Show For Default Duration")]
        public void ShowForDefaultDuration()
        {
            ShowForDuration(autoHideDuration);
        }

        public void ShowForDuration(float duration)
        {
            StopAutoHideRoutine();
            SetVisible(true);

            if (duration > 0f && isActiveAndEnabled)
            {
                hideRoutine = StartCoroutine(HideAfter(duration));
            }
        }

        public void SetWrapColor(Color color)
        {
            WrapColor = color;
        }

        public void SetWrapAlpha(float alpha)
        {
            WrapAlpha = alpha;
        }

        public void SetRimColor(Color color)
        {
            RimColor = color;
        }

        public void SetRimAlpha(float alpha)
        {
            RimAlpha = alpha;
        }

        private void BuildOverlays()
        {
            EnsureMaterial();
            if (targetRoot == null || overlayMaterial == null) return;

            Renderer[] renderers = targetRoot.GetComponentsInChildren<Renderer>(includeInactiveRenderers);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer source = renderers[i];
                if (source == null || source.transform.name.StartsWith(OverlayObjectPrefix)) continue;

                if (source is SkinnedMeshRenderer skinnedMeshRenderer)
                {
                    AddSkinnedOverlay(skinnedMeshRenderer);
                    continue;
                }

                if (source is MeshRenderer meshRenderer)
                {
                    AddMeshOverlay(meshRenderer);
                }
            }

            ApplyOverlayProperties();
        }

        private void AddMeshOverlay(MeshRenderer source)
        {
            MeshFilter sourceFilter = source.GetComponent<MeshFilter>();
            if (sourceFilter == null || sourceFilter.sharedMesh == null) return;

            GameObject overlayObject = CreateOverlayObject(source.transform);
            MeshFilter overlayFilter = overlayObject.AddComponent<MeshFilter>();
            MeshRenderer overlayRenderer = overlayObject.AddComponent<MeshRenderer>();

            overlayFilter.sharedMesh = sourceFilter.sharedMesh;
            ConfigureOverlayRenderer(source, overlayRenderer, sourceFilter.sharedMesh.subMeshCount);
            RegisterOverlay(overlayObject, overlayRenderer);
        }

        private void AddSkinnedOverlay(SkinnedMeshRenderer source)
        {
            if (source.sharedMesh == null) return;

            GameObject overlayObject = CreateOverlayObject(source.transform);
            SkinnedMeshRenderer overlayRenderer = overlayObject.AddComponent<SkinnedMeshRenderer>();

            overlayRenderer.sharedMesh = source.sharedMesh;
            overlayRenderer.rootBone = source.rootBone;
            overlayRenderer.bones = source.bones;
            overlayRenderer.localBounds = source.localBounds;
            overlayRenderer.updateWhenOffscreen = source.updateWhenOffscreen;
            overlayRenderer.quality = source.quality;
            ConfigureOverlayRenderer(source, overlayRenderer, source.sharedMesh.subMeshCount);
            RegisterOverlay(overlayObject, overlayRenderer);
        }

        private GameObject CreateOverlayObject(Transform sourceTransform)
        {
            GameObject overlayObject = new GameObject(OverlayObjectPrefix + sourceTransform.name)
            {
                hideFlags = HideFlags.DontSave
            };

            Transform overlayTransform = overlayObject.transform;
            overlayTransform.SetParent(sourceTransform, false);
            overlayTransform.localPosition = Vector3.zero;
            overlayTransform.localRotation = Quaternion.identity;
            overlayTransform.localScale = Vector3.one;
            overlayObject.layer = sourceTransform.gameObject.layer;
            overlayObject.SetActive(false);
            return overlayObject;
        }

        private void ConfigureOverlayRenderer(Renderer source, Renderer overlay, int materialCount)
        {
            overlay.sharedMaterials = GetRepeatedMaterials(materialCount);
            overlay.shadowCastingMode = ShadowCastingMode.Off;
            overlay.receiveShadows = false;
            overlay.lightProbeUsage = LightProbeUsage.Off;
            overlay.reflectionProbeUsage = ReflectionProbeUsage.Off;
            overlay.allowOcclusionWhenDynamic = source.allowOcclusionWhenDynamic;
            overlay.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            overlay.renderingLayerMask = source.renderingLayerMask;
            overlay.sortingLayerID = source.sortingLayerID;
            overlay.sortingOrder = source.sortingOrder + 1;
        }

        private Material[] GetRepeatedMaterials(int materialCount)
        {
            materialCount = Mathf.Max(1, materialCount);
            repeatedMaterials.Clear();

            for (int i = 0; i < materialCount; i++)
            {
                repeatedMaterials.Add(overlayMaterial);
            }

            return repeatedMaterials.ToArray();
        }

        private void RegisterOverlay(GameObject overlayObject, Renderer overlayRenderer)
        {
            overlayObjects.Add(overlayObject);
            overlayRenderers.Add(overlayRenderer);
        }

        private void EnsureMaterial()
        {
            if (overlayMaterial != null) return;

            Shader shader = Shader.Find("ArkLost/Combat/BossHitWrapOverlay");
            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            }

            if (shader == null) return;

            overlayMaterial = new Material(shader)
            {
                name = "LostArk Boss Hit Wrap Overlay",
                hideFlags = HideFlags.DontSave,
                enableInstancing = true,
                renderQueue = (int)RenderQueue.Transparent + 40
            };
        }

        private void ApplyOverlayProperties()
        {
            if (overlayRenderers == null || overlayRenderers.Count == 0) return;

            if (propertyBlock == null)
            {
                propertyBlock = new MaterialPropertyBlock();
            }

            Color finalColor = new Color(wrapColor.r, wrapColor.g, wrapColor.b, wrapAlpha);
            Color finalRimColor = new Color(rimColor.r, rimColor.g, rimColor.b, rimAlpha);
            propertyBlock.Clear();
            propertyBlock.SetColor(HitWrapColorId, finalColor);
            propertyBlock.SetColor(BaseColorId, finalColor);
            propertyBlock.SetColor(ColorId, finalColor);
            propertyBlock.SetColor(RimColorId, finalRimColor);
            propertyBlock.SetFloat(ShellWidthId, shellWidth);
            propertyBlock.SetFloat(RimPowerId, rimPower);
            propertyBlock.SetFloat(RimIntensityId, rimIntensity);
            propertyBlock.SetFloat(OutlineIntensityId, outlineIntensity);
            propertyBlock.SetFloat(NoiseScaleId, noiseScale);
            propertyBlock.SetFloat(NoiseStrengthId, noiseStrength);
            propertyBlock.SetFloat(DistortionScaleId, distortionScale);
            propertyBlock.SetFloat(DistortionStrengthId, distortionStrength);
            propertyBlock.SetFloat(PulseSpeedId, pulseSpeed);

            for (int i = 0; i < overlayRenderers.Count; i++)
            {
                Renderer overlayRenderer = overlayRenderers[i];
                if (overlayRenderer != null)
                {
                    overlayRenderer.SetPropertyBlock(propertyBlock);
                }
            }
        }

        private IEnumerator HideAfter(float duration)
        {
            yield return new WaitForSeconds(duration);
            hideRoutine = null;
            SetVisible(false);
        }

        private void SetVisible(bool visible)
        {
            isVisible = visible;

            for (int i = 0; i < overlayObjects.Count; i++)
            {
                GameObject overlayObject = overlayObjects[i];
                if (overlayObject != null)
                {
                    overlayObject.SetActive(visible);
                }
            }
        }

        private void StopAutoHideRoutine()
        {
            if (hideRoutine == null) return;

            StopCoroutine(hideRoutine);
            hideRoutine = null;
        }

        private void DestroyOverlays()
        {
            for (int i = 0; i < overlayObjects.Count; i++)
            {
                DestroyGenerated(overlayObjects[i]);
            }

            overlayObjects.Clear();
            overlayRenderers.Clear();
        }

        private void DestroyGenerated(Object generatedObject)
        {
            if (generatedObject == null) return;

            if (Application.isPlaying)
            {
                Destroy(generatedObject);
            }
            else
            {
                DestroyImmediate(generatedObject);
            }
        }
    }
}
