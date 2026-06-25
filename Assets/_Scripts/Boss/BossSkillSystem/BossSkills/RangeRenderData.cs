using _Scripts.CombatSystem.RenderRange;
using UnityEngine;

namespace _Scripts.Boss.BossSkillSystem.BossSkills
{
    [System.Serializable]
    public struct RangeRenderData
    {
        public RenderRangeShape RangeShape;

        [Header("Range")]
        public RenderRangePlayback Playback;
        public RenderRangePivot Pivot;
        [Min(0.01f)] public float Radius;
        [Min(0f)] public float InnerRadius;
        [Range(1f, 360f)] public float Angle;
        [Min(0.01f)] public float Length;
        [Min(0.01f)] public float Width;
        [Range(8, 256)] public int Segments;

        [Header("Projection")]
        [Range(-100, 100)] public int RenderPriority;
        [Min(0.01f)] public float DecalProjectionDepth;
        [Min(0f)] public float DecalHeightOffset;
        [Min(0f)] public float DecalDrawDistance;
        [Range(0f, 180f)] public float DecalStartAngleFade;
        [Range(0f, 180f)] public float DecalEndAngleFade;
        [Range(32, 1024)] public int DecalMaskResolution;

        [Header("Timing")]
        [Min(0.01f)] public float FillDuration;
        public bool PlayOnEnable;
        public bool Loop;
        [Range(0f, 1f)] public float EditModeFill;

        [Header("Look")]
        public float LayerHeightOffset;
        public bool AdditiveGlow;
        public Color InitialColor;
        public Color FillColor;
        public Color EdgeColor;
        [Range(0f, 1f)] public float InitialSolidAlpha;
        [Range(0f, 1f)] public float FillSolidAlpha;
        [Range(0f, 1f)] public float EdgeSolidAlpha;
        [Range(0f, 2f)] public float TextureMaskAlpha;
        [Range(0f, 2f)] public float EdgeMaskAlpha;
        [Range(0.25f, 8f)] public float TextureMaskPower;
        [Range(0f, 4f)] public float VisualIntensity;
        [Min(0.005f)] public float EdgeWorldWidth;

        [Header("Circle Textures")]
        public bool AutoAssignDefaultAssets;
        public Texture2D CircleInitialTexture;
        public Texture2D CircleFillTexture;
        public Texture2D CircleEdgeTexture;
        public Texture2D DonutInitialTexture;
        public Texture2D DonutFillTexture;
        public Texture2D DonutEdgeTexture;

        [Header("Cone and Line Textures")]
        public Texture2D ConeInitialTexture;
        public Texture2D ConeFillTexture;
        public Texture2D ConeEdgeTexture;
        public Texture2D ConeSoftTexture;
        public Texture2D LineInitialTexture;
        public Texture2D LineFillTexture;
        public Texture2D LineEdgeTexture;
    }
}
