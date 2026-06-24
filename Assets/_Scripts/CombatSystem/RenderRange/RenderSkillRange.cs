using _Scripts.Boss.BossSkills;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace _Scripts.CombatSystem.RenderRange
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class RenderSkillRange : MonoBehaviour
    {
        private const string InitialLayerName = "Initial Range";
        private const string FillLayerName = "Fill Range";
        private const string EdgeLayerName = "Edge Range";

        [Header("Range")]
        [SerializeField] private RenderRangeShape shape = RenderRangeShape.Circle;
        [SerializeField] private RenderRangePlayback playback = RenderRangePlayback.FillAfterInitial;
        [SerializeField] private RenderRangePivot pivot = RenderRangePivot.ForwardBase;
        [SerializeField, Min(0.01f)] private float radius = 3f;
        [SerializeField, Min(0f)] private float innerRadius = 1.8f;
        [SerializeField, Range(1f, 360f)] private float angle = 90f;
        [SerializeField, Min(0.01f)] private float length = 5f;
        [SerializeField, Min(0.01f)] private float width = 1.5f;
        [SerializeField, Range(8, 256)] private int segments = 96;

        [Header("Projection")]
        [SerializeField] private RenderRangeProjectionMode projectionMode = RenderRangeProjectionMode.DecalProjector;
        [SerializeField, Range(-100, 100)] private int renderPriority;
        [SerializeField, Min(0.01f)] private float decalProjectionDepth = 4f;
        [SerializeField, Min(0f)] private float decalHeightOffset = 2f;
        [SerializeField, Min(0f)] private float decalDrawDistance = 120f;
        [SerializeField, Range(0f, 180f)] private float decalStartAngleFade = 180f;
        [SerializeField, Range(0f, 180f)] private float decalEndAngleFade = 180f;
        [SerializeField, Range(32, 1024)] private int decalMaskResolution = 256;

        [Header("Timing")]
        [SerializeField, Min(0.01f)] private float fillDuration = 1.8f;
        [SerializeField] private bool playOnEnable = true;
        [SerializeField] private bool loop;
        [SerializeField, Range(0f, 1f)] private float editModeFill = 1f;

        [Header("Look")]
        [SerializeField] private float layerHeightOffset = 0.025f;
        [SerializeField] private bool additiveGlow = true;
        [SerializeField] private Color initialColor = new Color(1f, 0.055f, 0.012f, 0.54f);
        [SerializeField] private Color fillColor = new Color(1f, 0.035f, 0.006f, 0.78f);
        [SerializeField] private Color edgeColor = new Color(1f, 0.52f, 0.16f, 0.94f);
        [SerializeField, Range(0f, 1f)] private float initialSolidAlpha = 0.42f;
        [SerializeField, Range(0f, 1f)] private float fillSolidAlpha = 0.72f;
        [SerializeField, Range(0f, 1f)] private float edgeSolidAlpha = 0.02f;
        [SerializeField, Range(0f, 2f)] private float textureMaskAlpha = 0.5f;
        [SerializeField, Range(0f, 2f)] private float edgeMaskAlpha = 1.15f;
        [SerializeField, Range(0.25f, 8f)] private float textureMaskPower = 1.55f;
        [SerializeField, Range(0f, 4f)] private float visualIntensity = 1.18f;
        [SerializeField, Min(0.005f)] private float edgeWorldWidth = 0.08f;

        [Header("Circle Textures")]
        [SerializeField] private bool autoAssignDefaultAssets = true;
        [SerializeField] private Texture2D circleInitialTexture;
        [SerializeField] private Texture2D circleFillTexture;
        [SerializeField] private Texture2D circleEdgeTexture;
        [SerializeField] private Texture2D donutInitialTexture;
        [SerializeField] private Texture2D donutFillTexture;
        [SerializeField] private Texture2D donutEdgeTexture;

        [Header("Cone and Line Textures")]
        [SerializeField] private Texture2D coneInitialTexture;
        [SerializeField] private Texture2D coneFillTexture;
        [SerializeField] private Texture2D coneEdgeTexture;
        [SerializeField] private Texture2D coneSoftTexture;
        [SerializeField] private Texture2D lineInitialTexture;
        [SerializeField] private Texture2D lineFillTexture;
        [SerializeField] private Texture2D lineEdgeTexture;

        private MeshFilter initialFilter;
        private MeshFilter fillFilter;
        private MeshFilter edgeFilter;
        private MeshRenderer initialRenderer;
        private MeshRenderer fillRenderer;
        private MeshRenderer edgeRenderer;
        private DecalProjector initialDecal;
        private DecalProjector fillDecal;
        private DecalProjector edgeDecal;
        private Material initialMaterial;
        private Material fillMaterial;
        private Material edgeMaterial;
        private Mesh initialMesh;
        private Mesh fillMesh;
        private Mesh edgeMesh;
        private Texture2D initialDecalMask;
        private Texture2D fillDecalMask;
        private Texture2D edgeDecalMask;
        private int initialDecalMaskKey;
        private int fillDecalMaskKey;
        private int edgeDecalMaskKey;
        private RenderRangeProjectionMode materialProjectionMode;
        private float elapsed;
        private bool isPlaying;

        public float FillProgress { get; private set; } = 1f;

        private void Reset()
        {
            AssignDefaultTexturesIfEmpty();
            Refresh();
        }

        private void Awake()
        {
            AssignDefaultTexturesIfEmpty();
            EnsureLayers();
            Refresh();
        }

        private void OnEnable()
        {
            AssignDefaultTexturesIfEmpty();
            EnsureLayers();

            if (playOnEnable && Application.isPlaying)
            {
                Play();
                return;
            }

            FillProgress = playback == RenderRangePlayback.InitialOnly ? 1f : editModeFill;
            Refresh();
        }

        private void Update()
        {
            if (!Application.isPlaying)
            {
                FillProgress = playback == RenderRangePlayback.InitialOnly ? 1f : editModeFill;
                Refresh();
                return;
            }

            if (!isPlaying) return;

            elapsed += Time.deltaTime;
            FillProgress = playback == RenderRangePlayback.InitialOnly ? 1f : Mathf.Clamp01(elapsed / fillDuration);

            if (FillProgress >= 1f)
            {
                if (loop)
                {
                    elapsed = 0f;
                    FillProgress = 0f;
                }
                else
                {
                    isPlaying = false;
                }
            }

            Refresh();
        }

        private void OnDisable()
        {
            isPlaying = false;
        }

        private void OnDestroy()
        {
#if UNITY_EDITOR
            EditorApplication.delayCall -= DelayedEditorRefresh;
#endif
            ReleaseGeneratedObjects();
        }

        private void OnValidate()
        {
            radius = Mathf.Max(0.01f, radius);
            innerRadius = Mathf.Clamp(innerRadius, 0f, Mathf.Max(0f, radius - 0.01f));
            length = Mathf.Max(0.01f, length);
            width = Mathf.Max(0.01f, width);
            fillDuration = Mathf.Max(0.01f, fillDuration);
            decalProjectionDepth = Mathf.Max(0.01f, decalProjectionDepth);
            decalHeightOffset = Mathf.Max(0f, decalHeightOffset);
            decalDrawDistance = Mathf.Max(0f, decalDrawDistance);
            decalEndAngleFade = Mathf.Clamp(decalEndAngleFade, decalStartAngleFade, 180f);
            decalMaskResolution = Mathf.Clamp(decalMaskResolution, 32, 1024);
            edgeWorldWidth = Mathf.Max(0.005f, edgeWorldWidth);

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                EditorApplication.delayCall -= DelayedEditorRefresh;
                EditorApplication.delayCall += DelayedEditorRefresh;
            }
#endif
        }

        public void Play()
        {
            elapsed = 0f;
            FillProgress = playback == RenderRangePlayback.InitialOnly ? 1f : 0f;
            isPlaying = playback == RenderRangePlayback.FillAfterInitial;
            Refresh();
        }

        public void StopAtFull()
        {
            elapsed = fillDuration;
            FillProgress = 1f;
            isPlaying = false;
            Refresh();
        }

        public void SetShape(RenderRangeShape value)
        {
            shape = value;
            Refresh();
        }

        public void SetPlayback(RenderRangePlayback value)
        {
            playback = value;
            FillProgress = value == RenderRangePlayback.InitialOnly ? 1f : FillProgress;
            Refresh();
        }

        public void SetProjectionMode(RenderRangeProjectionMode value)
        {
            projectionMode = value;
            Refresh();
        }

        public void SetRenderPriority(int priority)
        {
            renderPriority = Mathf.Clamp(priority, -100, 100);
            Refresh();
        }

        public void SetRangeSetting(RangeRenderData rangeRenderData)
        {
            shape = rangeRenderData.RangeShape;
            playback = rangeRenderData.Playback;
            pivot = rangeRenderData.Pivot;
            radius = Mathf.Max(0.01f, rangeRenderData.Radius);
            innerRadius = Mathf.Clamp(rangeRenderData.InnerRadius, 0f, Mathf.Max(0f, radius - 0.01f));
            angle = Mathf.Clamp(rangeRenderData.Angle, 1f, 360f);
            length = Mathf.Max(0.01f, rangeRenderData.Length);
            width = Mathf.Max(0.01f, rangeRenderData.Width);
            segments = Mathf.Clamp(rangeRenderData.Segments, 8, 256);
            renderPriority = Mathf.Clamp(rangeRenderData.RenderPriority, -100, 100);
            decalProjectionDepth = Mathf.Max(0.01f, rangeRenderData.DecalProjectionDepth);
            decalHeightOffset = Mathf.Max(0f, rangeRenderData.DecalHeightOffset);
            decalDrawDistance = Mathf.Max(0f, rangeRenderData.DecalDrawDistance);
            decalStartAngleFade = Mathf.Clamp(rangeRenderData.DecalStartAngleFade, 0f, 180f);
            decalEndAngleFade = Mathf.Clamp(rangeRenderData.DecalEndAngleFade, decalStartAngleFade, 180f);
            decalMaskResolution = Mathf.Clamp(rangeRenderData.DecalMaskResolution, 32, 1024);
            fillDuration = Mathf.Max(0.01f, rangeRenderData.FillDuration);
            playOnEnable = rangeRenderData.PlayOnEnable;
            loop = rangeRenderData.Loop;
            editModeFill = Mathf.Clamp01(rangeRenderData.EditModeFill);
            layerHeightOffset = rangeRenderData.LayerHeightOffset;
            additiveGlow = rangeRenderData.AdditiveGlow;
            initialColor = rangeRenderData.InitialColor;
            fillColor = rangeRenderData.FillColor;
            edgeColor = rangeRenderData.EdgeColor;
            initialSolidAlpha = Mathf.Clamp01(rangeRenderData.InitialSolidAlpha);
            fillSolidAlpha = Mathf.Clamp01(rangeRenderData.FillSolidAlpha);
            edgeSolidAlpha = Mathf.Clamp01(rangeRenderData.EdgeSolidAlpha);
            textureMaskAlpha = Mathf.Clamp(rangeRenderData.TextureMaskAlpha, 0f, 2f);
            edgeMaskAlpha = Mathf.Clamp(rangeRenderData.EdgeMaskAlpha, 0f, 2f);
            textureMaskPower = Mathf.Clamp(rangeRenderData.TextureMaskPower, 0.25f, 8f);
            visualIntensity = Mathf.Clamp(rangeRenderData.VisualIntensity, 0f, 4f);
            edgeWorldWidth = Mathf.Max(0.005f, rangeRenderData.EdgeWorldWidth);
            autoAssignDefaultAssets = rangeRenderData.AutoAssignDefaultAssets;
            circleInitialTexture = rangeRenderData.CircleInitialTexture;
            circleFillTexture = rangeRenderData.CircleFillTexture;
            circleEdgeTexture = rangeRenderData.CircleEdgeTexture;
            donutInitialTexture = rangeRenderData.DonutInitialTexture;
            donutFillTexture = rangeRenderData.DonutFillTexture;
            donutEdgeTexture = rangeRenderData.DonutEdgeTexture;
            coneInitialTexture = rangeRenderData.ConeInitialTexture;
            coneFillTexture = rangeRenderData.ConeFillTexture;
            coneEdgeTexture = rangeRenderData.ConeEdgeTexture;
            coneSoftTexture = rangeRenderData.ConeSoftTexture;
            lineInitialTexture = rangeRenderData.LineInitialTexture;
            lineFillTexture = rangeRenderData.LineFillTexture;
            lineEdgeTexture = rangeRenderData.LineEdgeTexture;

            AssignDefaultTexturesIfEmpty();
            FillProgress = playback == RenderRangePlayback.InitialOnly ? 1f : FillProgress;
            Refresh();
        }

        public void ConfigureCircle(float newRadius)
        {
            shape = RenderRangeShape.Circle;
            radius = Mathf.Max(0.01f, newRadius);
            Refresh();
        }

        public void ConfigureDonut(float newRadius, float newInnerRadius)
        {
            shape = RenderRangeShape.Donut;
            radius = Mathf.Max(0.01f, newRadius);
            innerRadius = Mathf.Clamp(newInnerRadius, 0f, radius - 0.01f);
            Refresh();
        }

        public void ConfigureCone(float newRadius, float newAngle)
        {
            shape = RenderRangeShape.Cone;
            pivot = RenderRangePivot.ForwardBase;
            radius = Mathf.Max(0.01f, newRadius);
            angle = Mathf.Clamp(newAngle, 1f, 360f);
            Refresh();
        }

        public void ConfigureRectangle(float newLength, float newWidth)
        {
            shape = RenderRangeShape.Rectangle;
            pivot = RenderRangePivot.ForwardBase;
            length = Mathf.Max(0.01f, newLength);
            width = Mathf.Max(0.01f, newWidth);
            Refresh();
        }

        public void ConfigureLine(float newLength, float newWidth)
        {
            shape = RenderRangeShape.Line;
            pivot = RenderRangePivot.ForwardBase;
            length = Mathf.Max(0.01f, newLength);
            width = Mathf.Max(0.01f, newWidth);
            Refresh();
        }

        public void SetFillProgress(float normalizedFill)
        {
            FillProgress = Mathf.Clamp01(normalizedFill);
            isPlaying = false;
            Refresh();
        }

        public void SetDuration(float duration)
        {
            fillDuration = Mathf.Max(0.01f, duration);
        }

        public void Refresh()
        {
            EnsureLayers();
            UpdateMaterials();
            UpdateGeometry();
            UpdateLayerVisibility();
        }

        private void EnsureLayers()
        {
            if (projectionMode == RenderRangeProjectionMode.Mesh)
            {
                EnsureMeshLayer(InitialLayerName, 0, ref initialFilter, ref initialRenderer);
                EnsureMeshLayer(FillLayerName, 1, ref fillFilter, ref fillRenderer);
                EnsureMeshLayer(EdgeLayerName, 2, ref edgeFilter, ref edgeRenderer);
                SetDecalEnabled(initialDecal, false);
                SetDecalEnabled(fillDecal, false);
                SetDecalEnabled(edgeDecal, false);
                return;
            }

            EnsureDecalLayer(InitialLayerName, 0, ref initialDecal);
            EnsureDecalLayer(FillLayerName, 1, ref fillDecal);
            EnsureDecalLayer(EdgeLayerName, 2, ref edgeDecal);
            SetRendererEnabled(initialRenderer, false);
            SetRendererEnabled(fillRenderer, false);
            SetRendererEnabled(edgeRenderer, false);
        }

        private void EnsureMeshLayer(string layerName, int order, ref MeshFilter meshFilter,
            ref MeshRenderer meshRenderer)
        {
            Transform layerTransform = GetOrCreateLayer(layerName);
            layerTransform.localPosition = Vector3.up * (layerHeightOffset * order);
            layerTransform.localRotation = Quaternion.identity;
            layerTransform.localScale = Vector3.one;

            if (!layerTransform.TryGetComponent(out meshFilter))
            {
                meshFilter = layerTransform.gameObject.AddComponent<MeshFilter>();
            }

            if (!layerTransform.TryGetComponent(out meshRenderer))
            {
                meshRenderer = layerTransform.gameObject.AddComponent<MeshRenderer>();
            }

            if (layerTransform.TryGetComponent(out DecalProjector decalProjector))
            {
                decalProjector.enabled = false;
            }

            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.lightProbeUsage = LightProbeUsage.Off;
            meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            meshRenderer.sortingOrder = renderPriority + order;
        }

        private void EnsureDecalLayer(string layerName, int order, ref DecalProjector decalProjector)
        {
            Transform layerTransform = GetOrCreateLayer(layerName);
            Vector3 centerOffset = RenderRangeMaskTextureBuilder.GetProjectionCenterOffset(shape, pivot, radius, angle,
                length);
            layerTransform.localPosition = centerOffset + Vector3.up * (decalHeightOffset + layerHeightOffset * order);
            layerTransform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            layerTransform.localScale = Vector3.one;

            if (!layerTransform.TryGetComponent(out decalProjector))
            {
                decalProjector = layerTransform.gameObject.AddComponent<DecalProjector>();
            }

            if (layerTransform.TryGetComponent(out MeshRenderer meshRenderer))
            {
                meshRenderer.enabled = false;
            }

            Vector2 projectionSize = RenderRangeMaskTextureBuilder.GetProjectionSize(shape, radius, angle, length,
                width);
            decalProjector.scaleMode = DecalScaleMode.ScaleInvariant;
            decalProjector.size = new Vector3(projectionSize.x, projectionSize.y, decalProjectionDepth);
            decalProjector.pivot = new Vector3(0f, 0f, decalProjectionDepth * 0.5f);
            decalProjector.drawDistance = decalDrawDistance;
            decalProjector.fadeScale = 0.95f;
            decalProjector.startAngleFade = decalStartAngleFade;
            decalProjector.endAngleFade = decalEndAngleFade;
        }

        private Transform GetOrCreateLayer(string layerName)
        {
            Transform layerTransform = transform.Find(layerName);
            if (layerTransform != null)
            {
                return layerTransform;
            }

            GameObject layerObject = new GameObject(layerName);
            layerTransform = layerObject.transform;
            layerTransform.SetParent(transform, false);
            return layerTransform;
        }

        private void UpdateMaterials()
        {
            if (initialMaterial != null && materialProjectionMode != projectionMode)
            {
                ReleaseMaterials();
            }

            materialProjectionMode = projectionMode;

            if (projectionMode == RenderRangeProjectionMode.Mesh)
            {
                UpdateMeshMaterials();
                return;
            }

            UpdateDecalMaterials();
        }

        private void UpdateMeshMaterials()
        {
            Texture2D initialTexture = GetInitialTexture();
            Texture2D fillTexture = GetFillTexture();
            Texture2D edgeTexture = GetEdgeTexture();

            if (initialMaterial == null)
            {
                initialMaterial = RenderRangeMaterialUtility.CreateMesh("LostArk Range Initial", initialTexture,
                    initialColor, false, initialSolidAlpha, textureMaskAlpha, textureMaskPower, visualIntensity,
                    renderPriority);
            }

            if (fillMaterial == null)
            {
                fillMaterial = RenderRangeMaterialUtility.CreateMesh("LostArk Range Fill", fillTexture, fillColor,
                    false, fillSolidAlpha, textureMaskAlpha, textureMaskPower, visualIntensity, renderPriority + 1);
            }

            if (edgeMaterial == null)
            {
                edgeMaterial = RenderRangeMaterialUtility.CreateMesh("LostArk Range Edge", edgeTexture, edgeColor,
                    additiveGlow, edgeSolidAlpha, edgeMaskAlpha, textureMaskPower, visualIntensity, renderPriority + 2);
            }

            RenderRangeMaterialUtility.ApplyMesh(initialMaterial, initialTexture, initialColor, false,
                initialSolidAlpha, textureMaskAlpha, textureMaskPower, visualIntensity, renderPriority);
            RenderRangeMaterialUtility.ApplyMesh(fillMaterial, fillTexture, fillColor, false, fillSolidAlpha,
                textureMaskAlpha, textureMaskPower, visualIntensity, renderPriority + 1);
            RenderRangeMaterialUtility.ApplyMesh(edgeMaterial, edgeTexture, edgeColor, additiveGlow, edgeSolidAlpha,
                edgeMaskAlpha, textureMaskPower, visualIntensity, renderPriority + 2);

            if (initialRenderer != null) initialRenderer.sharedMaterial = initialMaterial;
            if (fillRenderer != null) fillRenderer.sharedMaterial = fillMaterial;
            if (edgeRenderer != null) edgeRenderer.sharedMaterial = edgeMaterial;
        }

        private void UpdateDecalMaterials()
        {
            initialDecalMask = GetOrCreateDecalMask(ref initialDecalMask, ref initialDecalMaskKey, 1f, false,
                initialColor, "LostArk Range Initial Mask");
            fillDecalMask = GetOrCreateDecalMask(ref fillDecalMask, ref fillDecalMaskKey, FillProgress, false,
                fillColor, "LostArk Range Fill Mask");
            edgeDecalMask = GetOrCreateDecalMask(ref edgeDecalMask, ref edgeDecalMaskKey, 1f, true,
                edgeColor, "LostArk Range Edge Mask");

            if (initialMaterial == null)
            {
                initialMaterial = RenderRangeMaterialUtility.CreateDecal("LostArk Range Initial Decal",
                    initialDecalMask, initialColor, renderPriority);
            }

            if (fillMaterial == null)
            {
                fillMaterial = RenderRangeMaterialUtility.CreateDecal("LostArk Range Fill Decal", fillDecalMask,
                    fillColor, renderPriority + 1);
            }

            if (edgeMaterial == null)
            {
                edgeMaterial = RenderRangeMaterialUtility.CreateDecal("LostArk Range Edge Decal", edgeDecalMask,
                    edgeColor, renderPriority + 2);
            }

            RenderRangeMaterialUtility.ApplyDecal(initialMaterial, initialDecalMask, initialColor, renderPriority);
            RenderRangeMaterialUtility.ApplyDecal(fillMaterial, fillDecalMask, fillColor, renderPriority + 1);
            RenderRangeMaterialUtility.ApplyDecal(edgeMaterial, edgeDecalMask, edgeColor, renderPriority + 2);

            if (initialDecal != null) initialDecal.material = initialMaterial;
            if (fillDecal != null) fillDecal.material = fillMaterial;
            if (edgeDecal != null) edgeDecal.material = edgeMaterial;
        }

        private Texture2D GetOrCreateDecalMask(ref Texture2D texture, ref int cachedKey, float normalizedFill,
            bool edgeOnly, Color tint, string textureName)
        {
            int nextKey = BuildDecalMaskKey(normalizedFill, edgeOnly, tint);
            if (texture != null && cachedKey == nextKey)
            {
                return texture;
            }

            DestroyGenerated(texture);
            cachedKey = nextKey;
            return RenderRangeMaskTextureBuilder.Build(shape, pivot, radius, innerRadius, angle, length, width,
                normalizedFill, decalMaskResolution, edgeOnly, edgeWorldWidth, tint, textureName);
        }

        private int BuildDecalMaskKey(float normalizedFill, bool edgeOnly, Color tint)
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + (int)shape;
                hash = hash * 31 + (int)pivot;
                hash = hash * 31 + Quantize(radius);
                hash = hash * 31 + Quantize(innerRadius);
                hash = hash * 31 + Quantize(angle);
                hash = hash * 31 + Quantize(length);
                hash = hash * 31 + Quantize(width);
                hash = hash * 31 + Quantize(normalizedFill);
                hash = hash * 31 + decalMaskResolution;
                hash = hash * 31 + Quantize(edgeWorldWidth);
                hash = hash * 31 + Quantize(tint.r);
                hash = hash * 31 + Quantize(tint.g);
                hash = hash * 31 + Quantize(tint.b);
                hash = hash * 31 + Quantize(tint.a);
                hash = hash * 31 + (edgeOnly ? 1 : 0);
                return hash;
            }
        }

        private int Quantize(float value)
        {
            return Mathf.RoundToInt(value * 1000f);
        }

        private void UpdateGeometry()
        {
            if (projectionMode == RenderRangeProjectionMode.Mesh)
            {
                ReplaceMesh(ref initialMesh, initialFilter, BuildMesh(1f, "Initial"));
                ReplaceMesh(ref fillMesh, fillFilter, BuildMesh(FillProgress, "Fill"));
                ReplaceMesh(ref edgeMesh, edgeFilter, BuildMesh(1f, "Edge"));
                return;
            }

            UpdateDecalGeometry(initialDecal, 0);
            UpdateDecalGeometry(fillDecal, 1);
            UpdateDecalGeometry(edgeDecal, 2);
        }

        private void UpdateDecalGeometry(DecalProjector decalProjector, int order)
        {
            if (decalProjector == null) return;

            Transform layerTransform = decalProjector.transform;
            Vector3 centerOffset = RenderRangeMaskTextureBuilder.GetProjectionCenterOffset(shape, pivot, radius, angle,
                length);
            Vector2 projectionSize = RenderRangeMaskTextureBuilder.GetProjectionSize(shape, radius, angle, length,
                width);

            layerTransform.localPosition = centerOffset + Vector3.up * (decalHeightOffset + layerHeightOffset * order);
            layerTransform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            layerTransform.localScale = Vector3.one;
            decalProjector.size = new Vector3(projectionSize.x, projectionSize.y, decalProjectionDepth);
            decalProjector.pivot = new Vector3(0f, 0f, decalProjectionDepth * 0.5f);
            decalProjector.drawDistance = decalDrawDistance;
            decalProjector.fadeScale = 0.95f;
            decalProjector.startAngleFade = decalStartAngleFade;
            decalProjector.endAngleFade = decalEndAngleFade;
        }

        private Mesh BuildMesh(float normalizedFill, string meshLabel)
        {
            Mesh mesh = RenderRangeMeshBuilder.Build(shape, radius, innerRadius, angle, length, width,
                normalizedFill, pivot, segments);
            mesh.name = $"LostArk Render Range {meshLabel}";
            return mesh;
        }

        private void ReplaceMesh(ref Mesh cachedMesh, MeshFilter meshFilter, Mesh nextMesh)
        {
            if (meshFilter == null)
            {
                DestroyGenerated(nextMesh);
                return;
            }

            if (cachedMesh != null)
            {
                DestroyGenerated(cachedMesh);
            }

            cachedMesh = nextMesh;
            meshFilter.sharedMesh = cachedMesh;
        }

        private void UpdateLayerVisibility()
        {
            bool hasFill = playback == RenderRangePlayback.FillAfterInitial && FillProgress > 0.001f;

            if (projectionMode == RenderRangeProjectionMode.Mesh)
            {
                SetRendererEnabled(initialRenderer, true);
                SetRendererEnabled(fillRenderer, hasFill);
                SetRendererEnabled(edgeRenderer, true);
                return;
            }

            SetDecalEnabled(initialDecal, true);
            SetDecalEnabled(fillDecal, hasFill);
            SetDecalEnabled(edgeDecal, true);
        }

        private void SetRendererEnabled(Renderer renderer, bool enabled)
        {
            if (renderer != null)
            {
                renderer.enabled = enabled;
            }
        }

        private void SetDecalEnabled(DecalProjector decalProjector, bool enabled)
        {
            if (decalProjector != null)
            {
                decalProjector.enabled = enabled;
            }
        }

        private Texture2D GetInitialTexture()
        {
            return shape switch
            {
                RenderRangeShape.Donut => donutInitialTexture != null ? donutInitialTexture : circleInitialTexture,
                RenderRangeShape.Cone => coneInitialTexture != null ? coneInitialTexture : coneSoftTexture,
                RenderRangeShape.Line => lineInitialTexture,
                RenderRangeShape.Rectangle => lineInitialTexture,
                _ => circleInitialTexture
            };
        }

        private Texture2D GetFillTexture()
        {
            return shape switch
            {
                RenderRangeShape.Donut => donutFillTexture != null ? donutFillTexture : circleFillTexture,
                RenderRangeShape.Cone => coneFillTexture != null ? coneFillTexture : coneSoftTexture,
                RenderRangeShape.Line => lineFillTexture != null ? lineFillTexture : lineInitialTexture,
                RenderRangeShape.Rectangle => lineFillTexture != null ? lineFillTexture : lineInitialTexture,
                _ => circleFillTexture != null ? circleFillTexture : circleInitialTexture
            };
        }

        private Texture2D GetEdgeTexture()
        {
            return shape switch
            {
                RenderRangeShape.Donut => donutEdgeTexture != null ? donutEdgeTexture : circleEdgeTexture,
                RenderRangeShape.Cone => coneEdgeTexture != null ? coneEdgeTexture : coneInitialTexture,
                RenderRangeShape.Line => lineEdgeTexture != null ? lineEdgeTexture : lineInitialTexture,
                RenderRangeShape.Rectangle => lineEdgeTexture != null ? lineEdgeTexture : lineInitialTexture,
                _ => circleEdgeTexture != null ? circleEdgeTexture : circleInitialTexture
            };
        }

        private void ReleaseGeneratedObjects()
        {
            DestroyGenerated(initialMesh);
            DestroyGenerated(fillMesh);
            DestroyGenerated(edgeMesh);
            DestroyGenerated(initialDecalMask);
            DestroyGenerated(fillDecalMask);
            DestroyGenerated(edgeDecalMask);
            ReleaseMaterials();
        }

        private void ReleaseMaterials()
        {
            DestroyGenerated(initialMaterial);
            DestroyGenerated(fillMaterial);
            DestroyGenerated(edgeMaterial);
            initialMaterial = null;
            fillMaterial = null;
            edgeMaterial = null;
        }

        private void DestroyGenerated(Object target)
        {
            if (target == null) return;

            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }

        private void AssignDefaultTexturesIfEmpty()
        {
#if UNITY_EDITOR
            if (!autoAssignDefaultAssets) return;

            circleInitialTexture = LoadTextureIfEmpty(circleInitialTexture, RenderRangeAssetPaths.CircleInitial);
            circleFillTexture = LoadTextureIfEmpty(circleFillTexture, RenderRangeAssetPaths.CircleFill);
            circleEdgeTexture = LoadTextureIfEmpty(circleEdgeTexture, RenderRangeAssetPaths.CircleEdge);
            donutInitialTexture = LoadTextureIfEmpty(donutInitialTexture, RenderRangeAssetPaths.DonutInitial);
            donutFillTexture = LoadTextureIfEmpty(donutFillTexture, RenderRangeAssetPaths.DonutFill);
            donutEdgeTexture = LoadTextureIfEmpty(donutEdgeTexture, RenderRangeAssetPaths.DonutEdge);
            coneInitialTexture = LoadTextureIfEmpty(coneInitialTexture, RenderRangeAssetPaths.ConeInitial);
            coneFillTexture = LoadTextureIfEmpty(coneFillTexture, RenderRangeAssetPaths.ConeFill);
            coneEdgeTexture = LoadTextureIfEmpty(coneEdgeTexture, RenderRangeAssetPaths.ConeEdge);
            coneSoftTexture = LoadTextureIfEmpty(coneSoftTexture, RenderRangeAssetPaths.ConeSoft);
            lineInitialTexture = LoadTextureIfEmpty(lineInitialTexture, RenderRangeAssetPaths.LineInitial);
            lineFillTexture = LoadTextureIfEmpty(lineFillTexture, RenderRangeAssetPaths.LineFill);
            lineEdgeTexture = LoadTextureIfEmpty(lineEdgeTexture, RenderRangeAssetPaths.LineEdge);
#endif
        }

#if UNITY_EDITOR
        private Texture2D LoadTextureIfEmpty(Texture2D currentTexture, string assetPath)
        {
            return currentTexture != null ? currentTexture : AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        }

        private void DelayedEditorRefresh()
        {
            if (this == null) return;

            AssignDefaultTexturesIfEmpty();
            FillProgress = playback == RenderRangePlayback.InitialOnly ? 1f : editModeFill;
            Refresh();
        }
#endif
    }
}
