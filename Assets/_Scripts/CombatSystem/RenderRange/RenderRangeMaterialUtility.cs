using UnityEngine;
using UnityEngine.Rendering;

namespace _Scripts.CombatSystem.RenderRange
{
    public static class RenderRangeMaterialUtility
    {
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int ShaderGraphBaseMapId = Shader.PropertyToID("Base_Map");
        private static readonly int ShaderGraphUnderscoreBaseMapId = Shader.PropertyToID("_Base_Map");
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        private static readonly int SurfaceId = Shader.PropertyToID("_Surface");
        private static readonly int BlendId = Shader.PropertyToID("_Blend");
        private static readonly int SrcBlendId = Shader.PropertyToID("_SrcBlend");
        private static readonly int DstBlendId = Shader.PropertyToID("_DstBlend");
        private static readonly int ZWriteId = Shader.PropertyToID("_ZWrite");
        private static readonly int CullId = Shader.PropertyToID("_Cull");
        private static readonly int SolidAlphaId = Shader.PropertyToID("_SolidAlpha");
        private static readonly int MaskAlphaId = Shader.PropertyToID("_MaskAlpha");
        private static readonly int MaskPowerId = Shader.PropertyToID("_MaskPower");
        private static readonly int IntensityId = Shader.PropertyToID("_Intensity");
        private static readonly int DrawOrderId = Shader.PropertyToID("_DrawOrder");
        private static readonly int QueueOffsetId = Shader.PropertyToID("_QueueOffset");

        public static Material CreateMesh(string materialName, Texture texture, Color color, bool additive,
            float solidAlpha, float maskAlpha, float maskPower, float intensity, int renderPriority)
        {
            Shader shader = Shader.Find("ArkLost/Combat/LostArkRenderRangeMask");
            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            }

            Material material = new Material(shader)
            {
                name = materialName,
                hideFlags = HideFlags.DontSave,
                enableInstancing = true
            };

            ApplyMesh(material, texture, color, additive, solidAlpha, maskAlpha, maskPower, intensity, renderPriority);
            return material;
        }

        public static Material CreateDecal(string materialName, Texture texture, Color color, int renderPriority)
        {
            Shader shader = Shader.Find("Shader Graphs/Decal");
            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Decal");
            }

            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            }

            Material material = new Material(shader)
            {
                name = materialName,
                hideFlags = HideFlags.DontSave,
                enableInstancing = true
            };

            ApplyDecal(material, texture, color, renderPriority);
            return material;
        }

        public static void ApplyMesh(Material material, Texture texture, Color color, bool additive, float solidAlpha,
            float maskAlpha, float maskPower, float intensity, int renderPriority)
        {
            if (material == null) return;

            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)RenderQueue.Transparent + renderPriority;

            SetTexture(material, texture);
            SetColor(material, color);

            if (material.HasProperty(EmissionColorId))
            {
                Color emission = new Color(color.r, color.g * 0.75f, color.b * 0.35f, color.a);
                material.SetColor(EmissionColorId, emission);
            }

            if (material.HasProperty(SurfaceId))
            {
                material.SetFloat(SurfaceId, 1f);
            }

            if (material.HasProperty(BlendId))
            {
                material.SetFloat(BlendId, additive ? 1f : 0f);
            }

            if (material.HasProperty(SrcBlendId))
            {
                material.SetInt(SrcBlendId, (int)BlendMode.SrcAlpha);
            }

            if (material.HasProperty(DstBlendId))
            {
                material.SetInt(DstBlendId, additive ? (int)BlendMode.One : (int)BlendMode.OneMinusSrcAlpha);
            }

            if (material.HasProperty(ZWriteId))
            {
                material.SetInt(ZWriteId, 0);
            }

            if (material.HasProperty(CullId))
            {
                material.SetInt(CullId, (int)CullMode.Off);
            }

            if (material.HasProperty(SolidAlphaId))
            {
                material.SetFloat(SolidAlphaId, Mathf.Clamp01(solidAlpha));
            }

            if (material.HasProperty(MaskAlphaId))
            {
                material.SetFloat(MaskAlphaId, Mathf.Max(0f, maskAlpha));
            }

            if (material.HasProperty(MaskPowerId))
            {
                material.SetFloat(MaskPowerId, Mathf.Max(0.25f, maskPower));
            }

            if (material.HasProperty(IntensityId))
            {
                material.SetFloat(IntensityId, Mathf.Max(0f, intensity));
            }

            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        }

        public static void ApplyDecal(Material material, Texture texture, Color color, int renderPriority)
        {
            if (material == null) return;

            SetTexture(material, texture);
            SetColor(material, Color.white);

            if (material.HasProperty(EmissionColorId))
            {
                material.SetColor(EmissionColorId, Color.black);
            }

            if (material.HasProperty(DrawOrderId))
            {
                material.SetFloat(DrawOrderId, renderPriority);
            }

            if (material.HasProperty(QueueOffsetId))
            {
                material.SetFloat(QueueOffsetId, renderPriority);
            }
        }

        private static void SetTexture(Material material, Texture texture)
        {
            if (material.HasProperty(BaseMapId))
            {
                material.SetTexture(BaseMapId, texture);
            }

            if (material.HasProperty(ShaderGraphBaseMapId))
            {
                material.SetTexture(ShaderGraphBaseMapId, texture);
            }

            if (material.HasProperty(ShaderGraphUnderscoreBaseMapId))
            {
                material.SetTexture(ShaderGraphUnderscoreBaseMapId, texture);
            }

            if (material.HasProperty(MainTexId))
            {
                material.SetTexture(MainTexId, texture);
            }
        }

        private static void SetColor(Material material, Color color)
        {
            if (material.HasProperty(BaseColorId))
            {
                material.SetColor(BaseColorId, color);
            }

            if (material.HasProperty(ColorId))
            {
                material.SetColor(ColorId, color);
            }
        }
    }
}
