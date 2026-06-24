#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace HovlStudio
{
    [InitializeOnLoad]
    public class RPChanger : EditorWindow
    {
        private const string HovlRoot = "Assets/Hovl Studio";
        private const string FallbackShaderName = "Hovl/URP/Unlit VFX Fallback";

        private static readonly string[] HovlFolders = { HovlRoot };

        private static readonly Dictionary<string, string[]> UrpShaderCandidates = new Dictionary<string, string[]>
        {
            { "Hovl/Particles/Add_CenterGlow", new[] { "Shader Graphs/HS_Blend_CG" } },
            { "Hovl/Particles/Blend_CenterGlow", new[] { "Shader Graphs/HS_Blend_CG" } },
            { "Hovl/Particles/Blend_TwoSides", new[] { "Shader Graphs/HS_Blend_TwoSides" } },
            { "Hovl/Particles/BlendDistort", new[] { "Shader Graphs/HS_BlendDistort" } },
            { "Hovl/Particles/Distortion", new[] { "Shader Graphs/HS_Distortion" } },
            { "Hovl/Particles/Explosion", new[] { "Shader Graphs/HS_Explosion" } },
            { "Hovl/Particles/SwordSlash", new[] { "Shader Graphs/HS_SwordSlash" } },
            { "Hovl/Particles/Lit_CenterGlow", new[] { "Shader Graphs/HS_Lit_CenterGlow", "Shader Graphs/HS_LitFresnel" } },
            { "Hovl/Particles/LightGlow", new[] { "Shader Graphs/HS_LightGlow" } },
            { "Hovl/Particles/Blend_Normals", new[] { "Shader Graphs/HS_Blend_Normals" } },
            { "Hovl/Particles/Ice", new[] { "Shader Graphs/HS_Ice" } },
            { "Hovl/Opaque/ParallaxIce", new[] { "Shader Graphs/HS_ParallaxIce" } },
            { "Hovl/Particles/VolumeLaser", new[] { "Shader Graphs/HS_VolumeLaser" } },
            { "Hovl/Particles/ShockWave", new[] { "Shader Graphs/HS_ShockWave" } },
            { "Hovl/Particles/SoftNoise", new[] { "Shader Graphs/HS_SoftNoise" } }
        };

        private static readonly Dictionary<string, string> BuiltInShaderCandidates = new Dictionary<string, string>
        {
            { "Shader Graphs/HS_Blend_CG", "Hovl/Particles/Blend_CenterGlow" },
            { "Shader Graphs/HS_Blend_TwoSides", "Hovl/Particles/Blend_TwoSides" },
            { "Shader Graphs/HS_BlendDistort", "Hovl/Particles/BlendDistort" },
            { "Shader Graphs/HS_Distortion", "Hovl/Particles/Distortion" },
            { "Shader Graphs/HS_Explosion", "Hovl/Particles/Explosion" },
            { "Shader Graphs/HS_SwordSlash", "Hovl/Particles/SwordSlash" },
            { "Shader Graphs/HS_LitFresnel", "Hovl/Particles/Lit_CenterGlow" }
        };

        private static readonly Dictionary<string, string> LegacyShaderPaths = new Dictionary<string, string>
        {
            { "Hovl/Particles/Add_CenterGlow", "Assets/Hovl Studio/HSFiles/Shaders/Add_CenterGlow.shader" },
            { "Hovl/Particles/Blend_CenterGlow", "Assets/Hovl Studio/HSFiles/Shaders/Blend_CenterGlow.shader" },
            { "Hovl/Particles/Blend_TwoSides", "Assets/Hovl Studio/HSFiles/Shaders/Blend_TwoSides.shader" },
            { "Hovl/Particles/Blend_Normals", "Assets/Hovl Studio/HSFiles/Shaders/Blend_Normals.shader" },
            { "Hovl/Particles/BlendDistort", "Assets/Hovl Studio/HSFiles/Shaders/BlendDistort.shader" },
            { "Hovl/Particles/Distortion", "Assets/Hovl Studio/HSFiles/Shaders/Distortion.shader" },
            { "Hovl/Particles/Electricity", "Assets/Hovl Studio/HSFiles/Shaders/Electricity.shader" },
            { "Hovl/Particles/Lightning", "Assets/Hovl Studio/HSFiles/Shaders/Lightning.shader" },
            { "Hovl/Particles/DissolveNoise", "Assets/Hovl Studio/HSFiles/Shaders/DissolveNoise.shader" },
            { "Hovl/Particles/AddTrail", "Assets/Hovl Studio/HSFiles/Shaders/AddTrail.shader" },
            { "Hovl/Particles/SmoothSmoke", "Assets/Hovl Studio/HSFiles/Shaders/SmoothSmoke.shader" },
            { "Hovl/Particles/Scroll", "Assets/Hovl Studio/HSFiles/Shaders/Scroll.shader" },
            { "Hovl/Particles/LightGlow", "Assets/Hovl Studio/HSFiles/Shaders/LightGlow.shader" },
            { "Hovl/Particles/Lit_CenterGlow", "Assets/Hovl Studio/HSFiles/Shaders/Lit_CenterGlow.shader" }
        };

        [InitializeOnLoadMethod]
        private static void LoadWindow()
        {
            string[] checkAsset = AssetDatabase.FindAssets("HSstartupCheck", HovlFolders);
            foreach (string guid in checkAsset)
            {
                ShowWindow();
                AssetDatabase.DeleteAsset(AssetDatabase.GUIDToAssetPath(guid));
            }
        }

        [MenuItem("Tools/RP changer for Hovl Studio assets")]
        public static void ShowWindow()
        {
            RPChanger window = (RPChanger)GetWindow(typeof(RPChanger));
            window.minSize = new Vector2(310, 160);
            window.maxSize = new Vector2(310, 160);
        }

        [MenuItem("Tools/Hovl Studio/Apply URP Shaders")]
        public static void ApplyUrpShadersMenu()
        {
            int changed = ApplyUrpShaders();
            Debug.Log($"Hovl Studio: converted {changed} material(s) to URP-compatible shaders.");
            if (!Application.isBatchMode)
            {
                EditorUtility.DisplayDialog("Hovl Studio", $"Converted {changed} material(s) to URP-compatible shaders.", "OK");
            }
        }

        public static void ApplyUrpShadersBatch()
        {
            int changed = ApplyUrpShaders();
            Debug.Log($"Hovl Studio: batch converted {changed} material(s) to URP-compatible shaders.");
        }

        [MenuItem("Tools/Hovl Studio/Revert Built-in Shaders")]
        public static void RevertBuiltInShadersMenu()
        {
            int changed = ConvertHovlMaterials(false);
            Debug.Log($"Hovl Studio: reverted {changed} material(s) to Built-in shaders.");
            if (!Application.isBatchMode)
            {
                EditorUtility.DisplayDialog("Hovl Studio", $"Reverted {changed} material(s) to Built-in shaders.", "OK");
            }
        }

        private void OnGUI()
        {
            GUILayout.Label("Change Hovl VFX shaders:");
            if (GUILayout.Button("Apply URP/HDRP compatible shaders"))
            {
                ApplyUrpShadersMenu();
            }

            if (GUILayout.Button("Revert to Built-in RP shaders"))
            {
                RevertBuiltInShadersMenu();
            }

            GUILayout.Space(8);
            GUILayout.Label("URP effects need Depth Texture and Opaque Texture enabled on the URP asset.", GUILayout.ExpandWidth(true));
        }

        private static int ApplyUrpShaders()
        {
            return ConvertHovlMaterials(true);
        }

        private static int ConvertHovlMaterials(bool toUrp)
        {
            string[] materialGuids = AssetDatabase.FindAssets("t:Material", HovlFolders);
            int changed = 0;

            foreach (string guid in materialGuids)
            {
                string materialPath = AssetDatabase.GUIDToAssetPath(guid);
                Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if (material == null || material.shader == null)
                {
                    continue;
                }

                string sourceShaderName = ResolveSourceShaderName(material, materialPath);
                Shader targetShader = toUrp ? FindUrpTargetShader(sourceShaderName) : FindBuiltInTargetShader(sourceShaderName);
                if (targetShader == null || material.shader == targetShader)
                {
                    continue;
                }

                MaterialSnapshot snapshot = MaterialSnapshot.Capture(material);
                material.shader = targetShader;
                snapshot.ApplyTo(material, sourceShaderName, targetShader.name);

                EditorUtility.SetDirty(material);
                changed++;
            }

            if (changed > 0)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            return changed;
        }

        private static Shader FindUrpTargetShader(string sourceShaderName)
        {
            if (string.IsNullOrEmpty(sourceShaderName) || sourceShaderName.StartsWith("Shader Graphs/"))
            {
                return null;
            }

            if (UrpShaderCandidates.TryGetValue(sourceShaderName, out string[] shaderNames))
            {
                Shader mappedShader = FindFirstShader(shaderNames);
                if (mappedShader != null)
                {
                    return mappedShader;
                }
            }

            if (sourceShaderName.StartsWith("Hovl/"))
            {
                return FindFirstShader(
                    FallbackShaderName,
                    "Universal Render Pipeline/Particles/Unlit",
                    "Universal Render Pipeline/Unlit");
            }

            return null;
        }

        private static Shader FindBuiltInTargetShader(string sourceShaderName)
        {
            if (string.IsNullOrEmpty(sourceShaderName))
            {
                return null;
            }

            if (BuiltInShaderCandidates.TryGetValue(sourceShaderName, out string builtInName))
            {
                return Shader.Find(builtInName);
            }

            return null;
        }

        private static Shader FindFirstShader(params string[] shaderNames)
        {
            foreach (string shaderName in shaderNames)
            {
                Shader shader = Shader.Find(shaderName);
                if (shader != null)
                {
                    return shader;
                }
            }

            return null;
        }

        private static string ResolveSourceShaderName(Material material, string materialPath)
        {
            string shaderName = material.shader.name;
            if (!string.IsNullOrEmpty(shaderName) && shaderName != "Hidden/InternalErrorShader")
            {
                return shaderName;
            }

            string shaderGuid = ReadShaderGuid(materialPath);
            if (string.IsNullOrEmpty(shaderGuid))
            {
                return shaderName;
            }

            foreach (KeyValuePair<string, string> entry in LegacyShaderPaths)
            {
                if (AssetDatabase.AssetPathToGUID(entry.Value) == shaderGuid)
                {
                    return entry.Key;
                }
            }

            return shaderName;
        }

        private static string ReadShaderGuid(string materialPath)
        {
            if (!File.Exists(materialPath))
            {
                return null;
            }

            string text = File.ReadAllText(materialPath);
            int shaderIndex = text.IndexOf("m_Shader:");
            if (shaderIndex < 0)
            {
                return null;
            }

            int guidIndex = text.IndexOf("guid: ", shaderIndex);
            if (guidIndex < 0 || guidIndex + 38 > text.Length)
            {
                return null;
            }

            return text.Substring(guidIndex + 6, 32);
        }

        private struct MaterialSnapshot
        {
            private Texture mainTexture;
            private Vector2 mainTextureScale;
            private Vector2 mainTextureOffset;
            private Color color;
            private float cull;
            private float blend2;
            private float emission;
            private float opacity;
            private float zWrite;
            private bool hasMainTexture;
            private bool hasColor;
            private bool hasCull;
            private bool hasBlend2;
            private bool hasEmission;
            private bool hasOpacity;
            private bool hasZWrite;

            public static MaterialSnapshot Capture(Material material)
            {
                MaterialSnapshot snapshot = new MaterialSnapshot
                {
                    color = Color.white,
                    mainTextureScale = Vector2.one,
                    mainTextureOffset = Vector2.zero,
                    opacity = 1f,
                    emission = 1f
                };

                snapshot.CaptureMainTexture(material);
                snapshot.CaptureColor(material);
                snapshot.hasCull = TryGetFloat(material, out snapshot.cull, "_CullMode", "_Cull");
                snapshot.hasBlend2 = TryGetFloat(material, out snapshot.blend2, "_Blend2", "_BUILTIN_DstBlend", "_DstBlend");
                snapshot.hasEmission = TryGetFloat(material, out snapshot.emission, "_Emission");
                snapshot.hasOpacity = TryGetFloat(material, out snapshot.opacity, "_Opacity");
                snapshot.hasZWrite = TryGetFloat(material, out snapshot.zWrite, "_ZWrite");
                return snapshot;
            }

            public void ApplyTo(Material material, string sourceShaderName, string targetShaderName)
            {
                ApplyTexture(material);
                ApplyColor(material);
                SetFloatIfPresent(material, "_Emission", hasEmission ? emission : 1f);
                SetFloatIfPresent(material, "_Opacity", hasOpacity ? opacity : 1f);

                if (hasCull)
                {
                    SetFloatIfPresent(material, "_Cull", cull);
                    SetFloatIfPresent(material, "_CullMode", cull);
                    SetFloatIfPresent(material, "_BUILTIN_CullMode", cull);
                }

                if (hasZWrite)
                {
                    SetFloatIfPresent(material, "_ZWrite", zWrite);
                }
                else
                {
                    SetFloatIfPresent(material, "_ZWrite", 0f);
                }

                ApplyBlendMode(material, sourceShaderName, targetShaderName);
            }

            private void CaptureMainTexture(Material material)
            {
                string property = FirstExistingProperty(material, "_MainTex", "_MainTexture", "_BaseMap");
                if (string.IsNullOrEmpty(property))
                {
                    return;
                }

                mainTexture = material.GetTexture(property);
                mainTextureScale = material.GetTextureScale(property);
                mainTextureOffset = material.GetTextureOffset(property);
                hasMainTexture = mainTexture != null;
            }

            private void CaptureColor(Material material)
            {
                string property = FirstExistingProperty(material, "_Color", "_BaseColor", "_TintColor");
                if (string.IsNullOrEmpty(property))
                {
                    return;
                }

                color = material.GetColor(property);
                hasColor = true;
            }

            private void ApplyTexture(Material material)
            {
                if (!hasMainTexture)
                {
                    return;
                }

                SetTextureIfPresent(material, "_MainTex", mainTexture, mainTextureScale, mainTextureOffset);
                SetTextureIfPresent(material, "_MainTexture", mainTexture, mainTextureScale, mainTextureOffset);
                SetTextureIfPresent(material, "_BaseMap", mainTexture, mainTextureScale, mainTextureOffset);
            }

            private void ApplyColor(Material material)
            {
                if (!hasColor)
                {
                    return;
                }

                SetColorIfPresent(material, "_Color", color);
                SetColorIfPresent(material, "_BaseColor", color);
                SetColorIfPresent(material, "_TintColor", color);
            }

            private void ApplyBlendMode(Material material, string sourceShaderName, string targetShaderName)
            {
                bool additive = sourceShaderName == "Hovl/Particles/Add_CenterGlow";
                if (targetShaderName == FallbackShaderName)
                {
                    SetFloatIfPresent(material, "_SrcBlend", additive ? 1f : 5f);
                    SetFloatIfPresent(material, "_DstBlend", additive ? (hasBlend2 ? blend2 : 1f) : 10f);
                    material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                    return;
                }

                if (additive)
                {
                    SetFloatIfPresent(material, "_Blend", 2f);
                    SetFloatIfPresent(material, "_BUILTIN_Blend", 2f);
                    SetFloatIfPresent(material, "_SrcBlend", 5f);
                    SetFloatIfPresent(material, "_BUILTIN_SrcBlend", 5f);
                    SetFloatIfPresent(material, "_DstBlend", 1f);
                    SetFloatIfPresent(material, "_BUILTIN_DstBlend", 1f);
                }
                else
                {
                    SetFloatIfPresent(material, "_Blend", 0f);
                    SetFloatIfPresent(material, "_BUILTIN_Blend", 0f);
                }

                if (sourceShaderName == "Hovl/Particles/Distortion")
                {
                    material.renderQueue = 2750;
                    SetFloatIfPresent(material, "_QueueControl", 1f);
                    SetFloatIfPresent(material, "_BUILTIN_QueueControl", 1f);
                }
            }

            private static string FirstExistingProperty(Material material, params string[] propertyNames)
            {
                foreach (string propertyName in propertyNames)
                {
                    if (material.HasProperty(propertyName))
                    {
                        return propertyName;
                    }
                }

                return null;
            }

            private static bool TryGetFloat(Material material, out float value, params string[] propertyNames)
            {
                foreach (string propertyName in propertyNames)
                {
                    if (material.HasProperty(propertyName))
                    {
                        value = material.GetFloat(propertyName);
                        return true;
                    }
                }

                value = 0f;
                return false;
            }

            private static void SetTextureIfPresent(Material material, string propertyName, Texture texture, Vector2 scale, Vector2 offset)
            {
                if (!material.HasProperty(propertyName))
                {
                    return;
                }

                material.SetTexture(propertyName, texture);
                material.SetTextureScale(propertyName, scale);
                material.SetTextureOffset(propertyName, offset);
            }

            private static void SetColorIfPresent(Material material, string propertyName, Color value)
            {
                if (material.HasProperty(propertyName))
                {
                    material.SetColor(propertyName, value);
                }
            }

            private static void SetFloatIfPresent(Material material, string propertyName, float value)
            {
                if (CanSetFloat(material, propertyName))
                {
                    material.SetFloat(propertyName, value);
                }
            }

            private static bool CanSetFloat(Material material, string propertyName)
            {
                if (!material.HasProperty(propertyName) || material.shader == null)
                {
                    return false;
                }

                int propertyCount = material.shader.GetPropertyCount();
                for (int i = 0; i < propertyCount; i++)
                {
                    if (material.shader.GetPropertyName(i) != propertyName)
                    {
                        continue;
                    }

                    UnityEngine.Rendering.ShaderPropertyType propertyType = material.shader.GetPropertyType(i);
                    return propertyType == UnityEngine.Rendering.ShaderPropertyType.Float ||
                           propertyType == UnityEngine.Rendering.ShaderPropertyType.Range;
                }

                return true;
            }
        }
    }
}
#endif
