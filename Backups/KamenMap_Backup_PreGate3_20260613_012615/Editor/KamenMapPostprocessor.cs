using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

public static class KamenMapPostprocessor
{
    const string ScriptSuffix = "/Editor/KamenMapPostprocessor.cs";
    const string BuildVersion = "graphics-prefab-20260612-01";

    [MenuItem("Tools/Lost Ark/Rebuild Kamen Map Assets")]
    public static void RebuildFromMenu()
    {
        var root = FindRoot();
        if (string.IsNullOrEmpty(root))
        {
            Debug.LogWarning("KamenMap root was not found. Put this folder under Assets/KamenMap.");
            return;
        }
        Build(root, true);
    }

    static void TryAutoBuild()
    {
        var root = FindRoot();
        if (string.IsNullOrEmpty(root))
        {
            return;
        }

        var manifestPath = ToFullPath(root + "/SourceMetadata/model_manifest.csv");
        if (!File.Exists(manifestPath))
        {
            return;
        }

        var manifestStamp = File.GetLastWriteTimeUtc(manifestPath).Ticks.ToString(CultureInfo.InvariantCulture);
        var key = "KamenMapPostprocessor.Built." + BuildVersion + "." + root + "." + manifestStamp;
        if (SessionState.GetBool(key, false))
        {
            return;
        }

        SessionState.SetBool(key, true);
        Build(root, false);
    }

    static string FindRoot()
    {
        var scripts = AssetDatabase.FindAssets("KamenMapPostprocessor t:Script");
        foreach (var guid in scripts)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid).Replace("\\", "/");
            if (path.EndsWith(ScriptSuffix, StringComparison.OrdinalIgnoreCase))
            {
                return path.Substring(0, path.Length - ScriptSuffix.Length);
            }
        }
        return AssetDatabase.IsValidFolder("Assets/KamenMap") ? "Assets/KamenMap" : string.Empty;
    }

    static string ToFullPath(string assetPath)
    {
        var relative = assetPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase)
            ? assetPath.Substring("Assets/".Length)
            : assetPath;
        return Path.Combine(Application.dataPath, relative.Replace("/", Path.DirectorySeparatorChar.ToString()));
    }

    static void Build(string root, bool force)
    {
        EnsureFolder(root, "Materials");
        EnsureFolder(root, "Prefabs");
        EnsureFolder(root, "Scenes");

        var rows = ReadMaterialRows(root + "/SourceMetadata/material_manifest.csv").ToList();
        if (rows.Count == 0)
        {
            Debug.LogWarning("KamenMap material manifest is empty.");
            return;
        }

        ConfigureTextures(rows, root);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        var materialByName = CreateMaterials(rows, root, out var materialAudit);
        WriteMaterialAudit(root, materialAudit);
        WriteModelMaterialAudit(root, materialByName);
        WriteGeometryAudit(root);
        BuildPrefab(root, materialByName, "KamenMap_ExtractedKamenParts.prefab", IsPlaceableModel);
        BuildPrefab(root, materialByName, "KamenMap_ArenaLikelyMeshes.prefab", IsArenaModel);
        var arenaPrefab = BuildCuratedArenaPrefab(root, materialByName);
        var graphicsPrefab = BuildGraphicsPrefab(root, arenaPrefab);
        BuildScene(root, graphicsPrefab);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("KamenMap import complete. Open " + root + "/Scenes/KamenMap.unity or use " + root + "/Prefabs/KamenMap.prefab.");
    }

    static void EnsureFolder(string root, string child)
    {
        var path = root + "/" + child;
        if (!AssetDatabase.IsValidFolder(path))
        {
            AssetDatabase.CreateFolder(root, child);
        }
    }

    static IEnumerable<MaterialRow> ReadMaterialRows(string assetPath)
    {
        var fullPath = ToFullPath(assetPath);
        if (!File.Exists(fullPath))
        {
            yield break;
        }

        var lines = File.ReadAllLines(fullPath);
        if (lines.Length < 2)
        {
            yield break;
        }

        var headers = SplitCsvLine(lines[0]);
        for (var i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i]))
            {
                continue;
            }
            var fields = SplitCsvLine(lines[i]);
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var c = 0; c < headers.Count && c < fields.Count; c++)
            {
                values[headers[c]] = fields[c];
            }
            yield return new MaterialRow
            {
                Package = Get(values, "package"),
                Name = CleanMaterialName(Get(values, "material")),
                Diffuse = Get(values, "diffuse"),
                Normal = Get(values, "normal"),
                Specular = Get(values, "specular"),
                Emissive = Get(values, "emissive")
            };
        }
    }

    static List<string> SplitCsvLine(string line)
    {
        var result = new List<string>();
        var current = new System.Text.StringBuilder();
        var quoted = false;
        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (ch == '"')
            {
                if (quoted && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else
                {
                    quoted = !quoted;
                }
            }
            else if (ch == ',' && !quoted)
            {
                result.Add(current.ToString());
                current.Length = 0;
            }
            else
            {
                current.Append(ch);
            }
        }
        result.Add(current.ToString());
        return result;
    }

    static string Get(Dictionary<string, string> values, string key)
    {
        return values.TryGetValue(key, out var value) ? value : string.Empty;
    }

    static string CleanMaterialName(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "default";
        }
        return value.Replace(" (Instance)", "").Trim();
    }

    static void ConfigureTextures(IEnumerable<MaterialRow> rows, string root)
    {
        var textureRoles = new Dictionary<string, TextureImportRole>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            RegisterTextureImport(textureRoles, root, row.Diffuse, TextureImportRole.Color);
            RegisterTextureImport(textureRoles, root, row.Normal, TextureImportRole.Normal);
            RegisterTextureImport(textureRoles, root, row.Specular, TextureImportRole.Data);
            RegisterTextureImport(textureRoles, root, row.Emissive, TextureImportRole.Color);
        }

        foreach (var entry in textureRoles)
        {
            var texturePath = entry.Key;
            var importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
            if (importer == null)
            {
                continue;
            }

            var changed = false;
            if (entry.Value == TextureImportRole.Normal)
            {
                changed |= SetImporterTextureType(importer, TextureImporterType.NormalMap);
                changed |= SetImporterSrgb(importer, false);
            }
            else
            {
                changed |= SetImporterTextureType(importer, TextureImporterType.Default);
                changed |= SetImporterSrgb(importer, entry.Value == TextureImportRole.Color);
                if (entry.Value == TextureImportRole.Color && LooksLikeAlphaTexture(texturePath))
                {
                    if (!importer.alphaIsTransparency)
                    {
                        importer.alphaIsTransparency = true;
                        changed = true;
                    }
                }
            }

            if (importer.filterMode != FilterMode.Trilinear)
            {
                importer.filterMode = FilterMode.Trilinear;
                changed = true;
            }
            if (importer.mipmapEnabled == false)
            {
                importer.mipmapEnabled = true;
                changed = true;
            }
            if (importer.anisoLevel < 8)
            {
                importer.anisoLevel = 8;
                changed = true;
            }

            if (changed)
            {
                importer.SaveAndReimport();
            }
        }
    }

    static void RegisterTextureImport(Dictionary<string, TextureImportRole> textureRoles, string root, string relativePath, TextureImportRole role)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return;
        }

        var assetPath = (root + "/" + relativePath.Replace("\\", "/")).Replace("\\", "/");
        if (!textureRoles.TryGetValue(assetPath, out var existing) || role > existing)
        {
            textureRoles[assetPath] = role;
        }
    }

    static bool LooksLikeAlphaTexture(string path)
    {
        var lower = path.Replace("\\", "/").ToLowerInvariant();
        return lower.Contains("_da_")
            || lower.Contains("_trn")
            || lower.Contains("_alpha")
            || lower.Contains("_opacity")
            || lower.Contains("window");
    }

    static bool SetImporterTextureType(TextureImporter importer, TextureImporterType textureType)
    {
        if (importer.textureType == textureType)
        {
            return false;
        }
        importer.textureType = textureType;
        return true;
    }

    static bool SetImporterSrgb(TextureImporter importer, bool sRgb)
    {
        if (importer.sRGBTexture == sRgb)
        {
            return false;
        }
        importer.sRGBTexture = sRgb;
        return true;
    }

    static Dictionary<string, Material> CreateMaterials(IEnumerable<MaterialRow> rows, string root, out List<MaterialAuditRow> auditRows)
    {
        var materialByName = new Dictionary<string, Material>(StringComparer.OrdinalIgnoreCase);
        auditRows = new List<MaterialAuditRow>();
        foreach (var row in rows)
        {
            var packageFolder = root + "/Materials/" + Sanitize(row.Package);
            EnsureNestedFolder(root + "/Materials", row.Package);
            var materialPath = packageFolder + "/" + Sanitize(row.Name) + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = new Material(FindLitShader()) { name = row.Name };
                AssetDatabase.CreateAsset(material, materialPath);
            }
            else
            {
                material.shader = FindLitShader();
            }

            var diffuse = LoadTexture(root, row.Diffuse);
            var normal = LoadTexture(root, row.Normal);
            var specular = LoadTexture(root, row.Specular);
            var emission = LoadTexture(root, row.Emissive);

            ApplyTexture(material, "_BaseMap", "_MainTex", diffuse);
            ApplyTexture(material, "_BumpMap", "_BumpMap", normal);
            ApplySpecular(material, row, specular);
            ApplyTexture(material, "_EmissionMap", "_EmissionMap", emission);
            ApplySurfaceDefaults(material, row, normal, specular, emission);
            ApplyAlphaMode(material, row);

            if (emission != null)
            {
                material.EnableKeyword("_EMISSION");
                SetColorIfPresent(material, "_EmissionColor", GuessEmissionColor(row));
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            else
            {
                material.DisableKeyword("_EMISSION");
                SetColorIfPresent(material, "_EmissionColor", Color.black);
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }

            if (normal != null)
            {
                material.EnableKeyword("_NORMALMAP");
            }
            else
            {
                material.DisableKeyword("_NORMALMAP");
            }

            auditRows.Add(new MaterialAuditRow
            {
                Package = row.Package,
                Name = row.Name,
                Shader = material.shader != null ? material.shader.name : string.Empty,
                Diffuse = row.Diffuse,
                Normal = row.Normal,
                Specular = row.Specular,
                Emissive = row.Emissive,
                DiffuseAssigned = diffuse != null,
                NormalAssigned = normal != null,
                SpecularAssigned = specular != null,
                EmissiveAssigned = emission != null,
                AlphaClip = ShouldAlphaClip(row),
                BumpScale = GetFloatOrDefault(material, "_BumpScale", 0f),
                Smoothness = GetFloatOrDefault(material, "_Smoothness", GetFloatOrDefault(material, "_Glossiness", 0f)),
                WorkflowMode = GetFloatOrDefault(material, "_WorkflowMode", -1f)
            });

            EditorUtility.SetDirty(material);
            materialByName[row.Name] = material;
        }
        return materialByName;
    }

    static void ApplyAlphaMode(Material material, MaterialRow row)
    {
        if (ShouldAlphaClip(row))
        {
            SetFloatIfPresent(material, "_Surface", 0f);
            SetFloatIfPresent(material, "_Blend", 0f);
            SetFloatIfPresent(material, "_AlphaClip", 1f);
            SetFloatIfPresent(material, "_Cutoff", GuessAlphaCutoff(row));
            SetFloatIfPresent(material, "_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
            SetFloatIfPresent(material, "_DstBlend", (float)UnityEngine.Rendering.BlendMode.Zero);
            SetFloatIfPresent(material, "_ZWrite", 1f);
            material.EnableKeyword("_ALPHATEST_ON");
            material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
            return;
        }

        SetFloatIfPresent(material, "_AlphaClip", 0f);
        SetFloatIfPresent(material, "_Cutoff", 0.5f);
        material.DisableKeyword("_ALPHATEST_ON");
        if (material.renderQueue == (int)UnityEngine.Rendering.RenderQueue.AlphaTest)
        {
            material.renderQueue = -1;
        }
    }

    static void ApplySpecular(Material material, MaterialRow row, Texture2D specular)
    {
        if (specular != null)
        {
            SetFloatIfPresent(material, "_WorkflowMode", 0f);
            if (material.HasProperty("_SpecGlossMap"))
            {
                ApplyTexture(material, "_SpecGlossMap", "_SpecGlossMap", specular);
                ApplyTexture(material, "_MetallicGlossMap", "_MetallicGlossMap", null);
            }
            else
            {
                ApplyTexture(material, "_MetallicGlossMap", "_MetallicGlossMap", specular);
            }
            material.EnableKeyword("_SPECULAR_SETUP");
            material.EnableKeyword("_SPECGLOSSMAP");
            material.DisableKeyword("_METALLICSPECGLOSSMAP");
            SetColorIfPresent(material, "_SpecColor", GuessSpecularColor(row));
            SetFloatIfPresent(material, "_Metallic", 0f);
            SetFloatIfPresent(material, "_GlossMapScale", 1f);
            SetFloatIfPresent(material, "_Glossiness", GuessSmoothness(row, true));
            SetFloatIfPresent(material, "_Smoothness", GuessSmoothness(row, true));
            return;
        }

        ApplyTexture(material, "_SpecGlossMap", "_SpecGlossMap", null);
        ApplyTexture(material, "_MetallicGlossMap", "_MetallicGlossMap", null);
        material.DisableKeyword("_SPECULAR_SETUP");
        material.DisableKeyword("_SPECGLOSSMAP");
        material.DisableKeyword("_METALLICSPECGLOSSMAP");
        SetFloatIfPresent(material, "_WorkflowMode", 1f);
        SetFloatIfPresent(material, "_Metallic", GuessMetallic(row));
        SetFloatIfPresent(material, "_GlossMapScale", GuessSmoothness(row, false));
        SetFloatIfPresent(material, "_Glossiness", GuessSmoothness(row, false));
        SetFloatIfPresent(material, "_Smoothness", GuessSmoothness(row, false));
        SetColorIfPresent(material, "_SpecColor", GuessSpecularColor(row));
    }

    static void ApplySurfaceDefaults(Material material, MaterialRow row, Texture2D normal, Texture2D specular, Texture2D emission)
    {
        SetColorIfPresent(material, "_BaseColor", Color.white);
        SetColorIfPresent(material, "_Color", Color.white);
        SetFloatIfPresent(material, "_OcclusionStrength", 0.8f);

        if (normal != null)
        {
            SetFloatIfPresent(material, "_BumpScale", GuessBumpScale(row));
        }
        else
        {
            SetFloatIfPresent(material, "_BumpScale", 1f);
        }

        if (specular == null && emission != null)
        {
            SetFloatIfPresent(material, "_Smoothness", Mathf.Max(GetFloatOrDefault(material, "_Smoothness", 0.25f), 0.34f));
            SetFloatIfPresent(material, "_Glossiness", Mathf.Max(GetFloatOrDefault(material, "_Glossiness", 0.25f), 0.34f));
        }
    }

    static bool ShouldAlphaClip(MaterialRow row)
    {
        var lower = MaterialKey(row);
        return lower.Contains("_da_")
            || lower.Contains("_trn")
            || lower.Contains("_alpha")
            || lower.Contains("_opacity")
            || lower.Contains("crack01")
            || lower.Contains("window01");
    }

    static float GuessAlphaCutoff(MaterialRow row)
    {
        var lower = MaterialKey(row);
        if (lower.Contains("crack") || lower.Contains("floor08") || lower.Contains("floor06"))
        {
            return 0.18f;
        }
        if (lower.Contains("window"))
        {
            return 0.08f;
        }
        return 0.28f;
    }

    static Shader FindLitShader()
    {
        return Shader.Find("Universal Render Pipeline/Lit")
            ?? Shader.Find("HDRP/Lit")
            ?? Shader.Find("Standard")
            ?? Shader.Find("Diffuse");
    }

    static Texture2D LoadTexture(string root, string relative)
    {
        if (string.IsNullOrWhiteSpace(relative))
        {
            return null;
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(root + "/" + relative.Replace("\\", "/"));
    }

    static void ApplyTexture(Material material, string preferredProperty, string fallbackProperty, Texture texture)
    {
        if (material.HasProperty(preferredProperty))
        {
            material.SetTexture(preferredProperty, texture);
        }
        if (fallbackProperty != preferredProperty && material.HasProperty(fallbackProperty))
        {
            material.SetTexture(fallbackProperty, texture);
        }
    }

    static void SetFloatIfPresent(Material material, string propertyName, float value)
    {
        if (material.HasProperty(propertyName))
        {
            material.SetFloat(propertyName, value);
        }
    }

    static float GetFloatOrDefault(Material material, string propertyName, float fallback)
    {
        return material.HasProperty(propertyName) ? material.GetFloat(propertyName) : fallback;
    }

    static void SetColorIfPresent(Material material, string propertyName, Color value)
    {
        if (material.HasProperty(propertyName))
        {
            material.SetColor(propertyName, value);
        }
    }

    static float GuessBumpScale(MaterialRow row)
    {
        var lower = MaterialKey(row);
        if (lower.Contains("rockfloor") || lower.Contains("rock_") || lower.Contains("rock0") || lower.Contains("stone"))
        {
            return 2.15f;
        }
        if (lower.Contains("floor") || lower.Contains("crack"))
        {
            return 1.85f;
        }
        if (lower.Contains("pillar") || lower.Contains("wall") || lower.Contains("gate") || lower.Contains("column"))
        {
            return 1.55f;
        }
        return 1.35f;
    }

    static float GuessSmoothness(MaterialRow row, bool hasSpecular)
    {
        var lower = MaterialKey(row);
        if (hasSpecular)
        {
            if (lower.Contains("metal") || lower.Contains("chain") || lower.Contains("gate"))
            {
                return 0.72f;
            }
            if (lower.Contains("floor") || lower.Contains("wall"))
            {
                return 0.56f;
            }
            return 0.5f;
        }

        if (lower.Contains("rock") || lower.Contains("stone"))
        {
            return 0.32f;
        }
        if (lower.Contains("sky") || lower.Contains("hdr"))
        {
            return 0.08f;
        }
        return 0.42f;
    }

    static float GuessMetallic(MaterialRow row)
    {
        var lower = MaterialKey(row);
        return lower.Contains("metal") || lower.Contains("chain") ? 0.45f : 0f;
    }

    static Color GuessSpecularColor(MaterialRow row)
    {
        var lower = MaterialKey(row);
        if (lower.Contains("rock") || lower.Contains("stone"))
        {
            return new Color(0.24f, 0.25f, 0.27f, 1f);
        }
        if (lower.Contains("gate") || lower.Contains("pillar") || lower.Contains("wall"))
        {
            return new Color(0.38f, 0.4f, 0.44f, 1f);
        }
        return new Color(0.32f, 0.34f, 0.38f, 1f);
    }

    static Color GuessEmissionColor(MaterialRow row)
    {
        var lower = MaterialKey(row);
        if (lower.Contains("rock") || lower.Contains("floor") || lower.Contains("stone") || lower.Contains("kamen"))
        {
            return new Color(1.55f, 0.28f, 0.14f, 1f);
        }
        if (lower.Contains("sky") || lower.Contains("hdr"))
        {
            return new Color(0.55f, 0.62f, 0.78f, 1f);
        }
        return new Color(0.95f, 0.36f, 0.24f, 1f);
    }

    static string MaterialKey(MaterialRow row)
    {
        return (row.Package + " " + row.Name + " " + row.Diffuse + " " + row.Normal + " " + row.Specular + " " + row.Emissive).ToLowerInvariant();
    }

    static void WriteMaterialAudit(string root, IEnumerable<MaterialAuditRow> rows)
    {
        var outputPath = ToFullPath(root + "/SourceMetadata/unity_material_audit.csv");
        var lines = new List<string>
        {
            "package,material,shader,diffuse,normal,specular,emissive,diffuseAssigned,normalAssigned,specularAssigned,emissiveAssigned,alphaClip,bumpScale,smoothness,workflowMode"
        };

        lines.AddRange(rows.Select(row => string.Join(",", new[]
        {
            Csv(row.Package),
            Csv(row.Name),
            Csv(row.Shader),
            Csv(row.Diffuse),
            Csv(row.Normal),
            Csv(row.Specular),
            Csv(row.Emissive),
            row.DiffuseAssigned ? "1" : "0",
            row.NormalAssigned ? "1" : "0",
            row.SpecularAssigned ? "1" : "0",
            row.EmissiveAssigned ? "1" : "0",
            row.AlphaClip ? "1" : "0",
            row.BumpScale.ToString("0.###", CultureInfo.InvariantCulture),
            row.Smoothness.ToString("0.###", CultureInfo.InvariantCulture),
            row.WorkflowMode.ToString("0.###", CultureInfo.InvariantCulture)
        })));

        File.WriteAllLines(outputPath, lines);
        AssetDatabase.ImportAsset(root + "/SourceMetadata/unity_material_audit.csv");
    }

    static void WriteModelMaterialAudit(string root, Dictionary<string, Material> materialByName)
    {
        var modelRoot = ToFullPath(root + "/Models");
        if (!Directory.Exists(modelRoot))
        {
            return;
        }

        var outputPath = ToFullPath(root + "/SourceMetadata/unity_model_material_slots.csv");
        var lines = new List<string>
        {
            "modelAsset,package,renderer,slotIndex,sourceMaterial,resolvedMaterial,status"
        };

        foreach (var fullPath in Directory.GetFiles(modelRoot, "*.obj", SearchOption.AllDirectories).OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            var assetPath = "Assets" + fullPath.Replace(Application.dataPath, string.Empty).Replace("\\", "/");
            var package = GetModelPackage(root, assetPath);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (model == null)
            {
                lines.Add(string.Join(",", new[]
                {
                    Csv(assetPath),
                    Csv(package),
                    Csv(string.Empty),
                    "-1",
                    Csv(string.Empty),
                    Csv(string.Empty),
                    Csv("model_not_loaded")
                }));
                continue;
            }

            var renderers = model.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                lines.Add(string.Join(",", new[]
                {
                    Csv(assetPath),
                    Csv(package),
                    Csv(string.Empty),
                    "-1",
                    Csv(string.Empty),
                    Csv(string.Empty),
                    Csv("no_renderer")
                }));
                continue;
            }

            foreach (var renderer in renderers)
            {
                var slots = renderer.sharedMaterials;
                if (slots == null || slots.Length == 0)
                {
                    lines.Add(string.Join(",", new[]
                    {
                        Csv(assetPath),
                        Csv(package),
                        Csv(renderer.name),
                        "-1",
                        Csv(string.Empty),
                        Csv(string.Empty),
                        Csv("no_material_slots")
                    }));
                    continue;
                }

                for (var i = 0; i < slots.Length; i++)
                {
                    var sourceName = slots[i] != null ? CleanMaterialName(slots[i].name) : string.Empty;
                    Material resolved = null;
                    var matched = !string.IsNullOrEmpty(sourceName) && materialByName.TryGetValue(sourceName, out resolved);
                    lines.Add(string.Join(",", new[]
                    {
                        Csv(assetPath),
                        Csv(package),
                        Csv(renderer.name),
                        i.ToString(CultureInfo.InvariantCulture),
                        Csv(sourceName),
                        Csv(matched && resolved != null ? resolved.name : string.Empty),
                        Csv(matched ? "matched" : "missing")
                    }));
                }
            }
        }

        File.WriteAllLines(outputPath, lines);
        AssetDatabase.ImportAsset(root + "/SourceMetadata/unity_model_material_slots.csv");
    }

    static string GetModelPackage(string root, string assetPath)
    {
        var prefix = root + "/Models/";
        if (!assetPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }
        var remainder = assetPath.Substring(prefix.Length);
        var slash = remainder.IndexOf('/');
        return slash >= 0 ? remainder.Substring(0, slash) : string.Empty;
    }

    static void WriteGeometryAudit(string root)
    {
        var manifestPath = ToFullPath(root + "/SourceMetadata/model_manifest.csv");
        if (!File.Exists(manifestPath))
        {
            return;
        }

        var outputPath = ToFullPath(root + "/SourceMetadata/unity_geometry_audit.csv");
        var lines = new List<string>
        {
            "package,model,obj,vertices,faces,uvs,normals,materialSlots,sizeX,sizeY,sizeZ,minY,maxY,warnings"
        };

        var manifestLines = File.ReadAllLines(manifestPath);
        if (manifestLines.Length < 2)
        {
            File.WriteAllLines(outputPath, lines);
            AssetDatabase.ImportAsset(root + "/SourceMetadata/unity_geometry_audit.csv");
            return;
        }

        var headers = SplitCsvLine(manifestLines[0]);
        for (var i = 1; i < manifestLines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(manifestLines[i]))
            {
                continue;
            }

            var fields = SplitCsvLine(manifestLines[i]);
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var c = 0; c < headers.Count && c < fields.Count; c++)
            {
                values[headers[c]] = fields[c];
            }

            var obj = Get(values, "obj").Replace("\\", "/");
            var package = Get(values, "package");
            var model = Path.GetFileNameWithoutExtension(obj);
            var fullPath = ToFullPath(root + "/" + obj);
            var vertices = 0;
            var faces = 0;
            var uvs = 0;
            var normals = 0;
            var materialSlots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var min = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
            var max = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);

            if (File.Exists(fullPath))
            {
                foreach (var line in File.ReadLines(fullPath))
                {
                    if (line.StartsWith("v ", StringComparison.Ordinal))
                    {
                        var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length >= 4
                            && float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var x)
                            && float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var y)
                            && float.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var z))
                        {
                            vertices++;
                            min = Vector3.Min(min, new Vector3(x, y, z));
                            max = Vector3.Max(max, new Vector3(x, y, z));
                        }
                    }
                    else if (line.StartsWith("vt ", StringComparison.Ordinal))
                    {
                        uvs++;
                    }
                    else if (line.StartsWith("vn ", StringComparison.Ordinal))
                    {
                        normals++;
                    }
                    else if (line.StartsWith("f ", StringComparison.Ordinal))
                    {
                        faces++;
                    }
                    else if (line.StartsWith("usemtl ", StringComparison.Ordinal))
                    {
                        materialSlots.Add(CleanMaterialName(line.Substring(7).Trim()));
                    }
                }
            }

            var size = vertices > 0 ? max - min : Vector3.zero;
            var warnings = new List<string>();
            var lower = model.ToLowerInvariant();
            if (vertices == 0 || faces == 0) warnings.Add("empty_geometry");
            if (vertices > 0 && uvs == 0) warnings.Add("missing_uv");
            if (vertices > 0 && normals == 0) warnings.Add("missing_normals");
            if (lower.Contains("floor07_sm") && size.x > 150f && size.z > 150f) warnings.Add("huge_smooth_cap_candidate");
            if (lower.Contains("lowpoly")) warnings.Add("lowpoly_variant");
            if (size.y < 0.05f && lower.Contains("floor")) warnings.Add("very_flat_overlay");

            lines.Add(string.Join(",", new[]
            {
                Csv(package),
                Csv(model),
                Csv(obj),
                vertices.ToString(CultureInfo.InvariantCulture),
                faces.ToString(CultureInfo.InvariantCulture),
                uvs.ToString(CultureInfo.InvariantCulture),
                normals.ToString(CultureInfo.InvariantCulture),
                materialSlots.Count.ToString(CultureInfo.InvariantCulture),
                size.x.ToString("0.###", CultureInfo.InvariantCulture),
                size.y.ToString("0.###", CultureInfo.InvariantCulture),
                size.z.ToString("0.###", CultureInfo.InvariantCulture),
                (vertices > 0 ? min.y : 0f).ToString("0.###", CultureInfo.InvariantCulture),
                (vertices > 0 ? max.y : 0f).ToString("0.###", CultureInfo.InvariantCulture),
                Csv(string.Join("|", warnings))
            }));
        }

        File.WriteAllLines(outputPath, lines);
        AssetDatabase.ImportAsset(root + "/SourceMetadata/unity_geometry_audit.csv");
    }

    static string Csv(string value)
    {
        value = value ?? string.Empty;
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }

    static void EnsureNestedFolder(string parent, string child)
    {
        var clean = Sanitize(child);
        var full = parent + "/" + clean;
        if (!AssetDatabase.IsValidFolder(full))
        {
            AssetDatabase.CreateFolder(parent, clean);
        }
    }

    static string Sanitize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "Asset";
        }
        var chars = value.Select(ch => char.IsLetterOrDigit(ch) || ch == '_' || ch == '-' || ch == '.' ? ch : '_').ToArray();
        return new string(chars).Trim('.', '_');
    }

    static GameObject BuildCuratedArenaPrefab(string root, Dictionary<string, Material> materialByName)
    {
        var prefabPath = root + "/Prefabs/KamenMap.prefab";
        var modelByName = GetModelPathByName(root);
        var container = new GameObject("KamenMap_CuratedArena");

        var foundation = CreateChild(container.transform, "Arena_Foundation");
        var underlay = CreateChild(foundation.transform, "Lowered_Underlay_Not_Surface");
        AddModelInstance(modelByName, "bg_rad_kamen_floor07_sm", underlay.transform, new Vector3(0f, -2.2f, 0f), Vector3.zero, Vector3.one, materialByName);

        var surface = CreateChild(foundation.transform, "Visible_Detail_Surface");
        AddFoundationSurface(modelByName, materialByName, surface.transform, "bg_rad_kamen_floor04_sm", 0f);
        AddFoundationSurface(modelByName, materialByName, surface.transform, "bg_rad_kamen_floor03_sm", 0.18f);
        AddFoundationSurface(modelByName, materialByName, surface.transform, "bg_rad_kamen_floor06_sm_psy", 0.36f);
        AddFoundationSurface(modelByName, materialByName, surface.transform, "bg_rad_kamen_floor01_sm", 0.5f);
        AddFoundationSurface(modelByName, materialByName, surface.transform, "bg_rad_kamen_floor02_sm", 0.5f);
        AddFoundationSurface(modelByName, materialByName, surface.transform, "bg_rad_kamen_floor05_sm", 0.58f);
        AddFoundationSurface(modelByName, materialByName, surface.transform, "bg_rad_kamen_floor05a_sm_hhk", 0.62f);
        AddFoundationSurface(modelByName, materialByName, surface.transform, "bg_rad_kamen_floor05b_sm", 0.64f);
        AddFoundationSurface(modelByName, materialByName, surface.transform, "bg_rad_kamen_floor05c_sm", 0.66f);
        AddFoundationSurface(modelByName, materialByName, surface.transform, "bg_rad_kamen_floor05d_sm", 0.66f);
        AddFoundationSurface(modelByName, materialByName, surface.transform, "bg_rad_kamen_floor05e_sm", 0.68f);
        AddFoundationSurface(modelByName, materialByName, surface.transform, "bg_rad_kamen_floor05f_sm", 0.68f);
        AddFoundationSurface(modelByName, materialByName, surface.transform, "bg_rad_kamen_floor05g_sm", 0.7f);
        AddFoundationSurface(modelByName, materialByName, surface.transform, "bg_rad_kamen_floor05h_sm", 0.7f);
        AddFoundationSurface(modelByName, materialByName, surface.transform, "bg_rad_kamen_floor07a_sm", 0.28f);
        AddFoundationSurface(modelByName, materialByName, surface.transform, "bg_rad_kamen_floor07b_sm", -0.35f);
        AddFoundationSurface(modelByName, materialByName, surface.transform, "bg_rad_kamen_floor07c_sm", 0.32f);
        AddFoundationSurface(modelByName, materialByName, surface.transform, "bg_rad_kamen_floor07d_sm", -0.35f);
        AddFoundationSurface(modelByName, materialByName, surface.transform, "bg_rad_kamen_floor07e_sm", -0.35f);
        AddFoundationSurface(modelByName, materialByName, surface.transform, "bg_rad_kamen_floor08_sm", 0.86f);
        AddFoundationSurface(modelByName, materialByName, surface.transform, "bg_rad_kamen_floor08a_sm", 0.9f);
        AddFoundationSurface(modelByName, materialByName, surface.transform, "bg_rad_kamen_floor08b_sm", 0.92f);
        AddFoundationSurface(modelByName, materialByName, surface.transform, "bg_rad_kamen_floor08c_sm", 0.92f);
        AddFoundationSurface(modelByName, materialByName, surface.transform, "bg_rad_kamen_floor08d_sm", 0.94f);
        AddFoundationSurface(modelByName, materialByName, surface.transform, "bg_rad_kamen_floor08e_sm", 0.96f);
        AddFoundationSurface(modelByName, materialByName, surface.transform, "bg_rad_kamen_floor08f_sm", 0.98f);
        AddFoundationSurface(modelByName, materialByName, surface.transform, "bg_rad_kamen_floor08g_sm", 1f);
        AddFoundationSurface(modelByName, materialByName, surface.transform, "bg_rad_kamen_floor08h_sm", 1f);
        AddFoundationSurface(modelByName, materialByName, surface.transform, "bg_rad_kamen_floor08i_sm", 1f);
        AddFoundationSurface(modelByName, materialByName, surface.transform, "bg_rad_kamen_floor09_sm_cjy", 1.05f);
        AddFoundationSurface(modelByName, materialByName, surface.transform, "bg_rad_kamen_floor10_sm", 1.08f);
        AddFoundationSurface(modelByName, materialByName, surface.transform, "bg_rad_kamen_floor11_sm", 1.1f);
        AddFoundationSurface(modelByName, materialByName, surface.transform, "bg_rad_kamen_floor11a_sm", 1.1f);
        AddFoundationSurface(modelByName, materialByName, surface.transform, "bg_rad_kamen_floor12_sm", 0.74f);
        AddFoundationSurface(modelByName, materialByName, surface.transform, "bg_rad_kamen_floor12a_sm", 0.76f);
        AddFoundationSurface(modelByName, materialByName, surface.transform, "bg_rad_kamen_rockfloor01_sm", 1.2f);
        AddFoundationSurface(modelByName, materialByName, surface.transform, "bg_rad_kamen_rockfloor01a_sm", 1.2f);
        AddFoundationSurface(modelByName, materialByName, surface.transform, "bg_rad_kamen_rockfloor02_sm", 1.18f);
        AddFoundationSurface(modelByName, materialByName, surface.transform, "bg_rad_kamen_rockfloor03_sm", 1.24f);
        AddFoundationSurface(modelByName, materialByName, surface.transform, "bg_rad_kamen_rockfloor03a_sm", 1.24f);
        AddFoundationSurface(modelByName, materialByName, surface.transform, "bg_rad_kamen_rockfloor03b_sm", 1.24f);
        AddFoundationSurface(modelByName, materialByName, surface.transform, "bg_rad_kamen_rockfloor03c_sm", 1.26f);
        AddFoundationSurface(modelByName, materialByName, surface.transform, "bg_rad_kamen_rockfloor03d_sm", 1.26f);
        AddFoundationSurface(modelByName, materialByName, surface.transform, "bg_rad_kamen_rockfloor03e_sm", 1.26f);

        AddRing(
            modelByName,
            materialByName,
            container.transform,
            "Outer_Rock_Pillars",
            new[]
            {
                "bg_rad_kamen_rockpillar01_sm",
                "bg_rad_kamen_rockpillar01a_sm",
                "bg_rad_kamen_rockpillar01b_sm",
                "bg_rad_kamen_rockpillar01c_sm",
                "bg_rad_kamen_pillar09_sm_hhk",
                "bg_rad_kamen_pillar02_sm_hhk"
            },
            16,
            92f,
            -0.8f,
            1.25f,
            11.25f);

        AddRing(
            modelByName,
            materialByName,
            container.transform,
            "Inner_Pillars",
            new[]
            {
                "bg_rad_kamen_pillar07_sm",
                "bg_rad_kamen_pillar07a_sm",
                "bg_rad_kamen_pillar01_sm_cjy",
                "bg_rad_kamen_pillar03_sm_csw"
            },
            8,
            42f,
            -0.2f,
            1.05f,
            22.5f);

        AddRing(
            modelByName,
            materialByName,
            container.transform,
            "Broken_Rocks",
            new[]
            {
                "bg_rad_kamen_rock01_sm",
                "bg_rad_kamen_rock01a_sm",
                "bg_rad_kamen_rock02_sm",
                "bg_rad_kamen_rock02a_sm",
                "bg_rad_kamen_rock02b_sm",
                "bg_rad_kamen_rock02c_sm",
                "bg_rad_kamen_rock02d_sm",
                "bg_rad_kamen_rock02e_sm",
                "bg_rad_kamen_rock02f_sm",
                "bg_rad_kamen_rock02g_sm"
            },
            24,
            72f,
            -0.5f,
            1.45f,
            7.5f);

        AddRing(
            modelByName,
            materialByName,
            container.transform,
            "Outer_Walls_And_Gate",
            new[]
            {
                "bg_rad_kamen_gate01_sm_jjh",
                "bg_rad_kamen_wall01_sm_hhk",
                "bg_rad_kamen_wall02_sm_hhk",
                "bg_rad_kamen_wall03_sm_hhk",
                "bg_rad_kamen_column01_sm_hhk",
                "bg_rad_kamen_column02_sm_jjh"
            },
            12,
            104f,
            -0.4f,
            1.15f,
            0f);

        var prefab = PrefabUtility.SaveAsPrefabAsset(container, prefabPath);
        UnityEngine.Object.DestroyImmediate(container);
        return prefab;
    }

    static GameObject BuildGraphicsPrefab(string root, GameObject arenaPrefab)
    {
        var prefabPath = root + "/Prefabs/KamenMap_WithGraphics.prefab";
        var container = new GameObject("KamenMap_WithGraphics");

        GameObject mapInstance = null;
        if (arenaPrefab != null)
        {
            mapInstance = PrefabUtility.InstantiatePrefab(arenaPrefab) as GameObject;
            if (mapInstance != null)
            {
                mapInstance.name = "KamenMap";
                mapInstance.transform.SetParent(container.transform, false);
            }
        }

        var graphicsRig = CreateChild(container.transform, "KamenMap_Graphics_Rig");
        var settings = graphicsRig.AddComponent<KamenMapGraphicsSettings>();
        settings.skybox = CreateSkybox(root);
        settings.applyOnEnable = true;
        settings.configureMainCamera = true;
        settings.fog = true;
        settings.fogMode = FogMode.ExponentialSquared;
        settings.fogColor = new Color(0.025f, 0.028f, 0.036f, 1f);
        settings.fogDensity = 0.0035f;
        settings.ambientSkyColor = new Color(0.055f, 0.063f, 0.086f, 1f);
        settings.ambientEquatorColor = new Color(0.09f, 0.065f, 0.065f, 1f);
        settings.ambientGroundColor = new Color(0.025f, 0.022f, 0.024f, 1f);
        settings.reflectionIntensity = 0.38f;

        CreateDirectionalLight(graphicsRig.transform);
        CreateKamenLightingRig(mapInstance, graphicsRig.transform);
        CreatePostProcessingVolume(root, graphicsRig.transform);

        var prefab = PrefabUtility.SaveAsPrefabAsset(container, prefabPath);
        UnityEngine.Object.DestroyImmediate(container);
        return prefab;
    }

    static void AddFoundationSurface(
        Dictionary<string, string> modelByName,
        Dictionary<string, Material> materialByName,
        Transform parent,
        string modelName,
        float yOffset)
    {
        AddModelInstance(modelByName, modelName, parent, new Vector3(0f, yOffset, 0f), Vector3.zero, Vector3.one, materialByName);
    }

    static GameObject CreateChild(Transform parent, string name)
    {
        var child = new GameObject(name);
        child.transform.SetParent(parent, false);
        return child;
    }

    static Dictionary<string, string> GetModelPathByName(string root)
    {
        var modelRoot = ToFullPath(root + "/Models");
        if (!Directory.Exists(modelRoot))
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
        return Directory.GetFiles(modelRoot, "*.obj", SearchOption.AllDirectories)
            .Select(path => "Assets" + path.Replace(Application.dataPath, string.Empty).Replace("\\", "/"))
            .Where(path => path.IndexOf("/BG_RAD_KAMEN", StringComparison.OrdinalIgnoreCase) >= 0)
            .GroupBy(path => Path.GetFileNameWithoutExtension(path), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.OrderBy(path => path.Length).First(), StringComparer.OrdinalIgnoreCase);
    }

    static GameObject AddModelInstance(
        Dictionary<string, string> modelByName,
        string modelName,
        Transform parent,
        Vector3 localPosition,
        Vector3 localEuler,
        Vector3 localScale,
        Dictionary<string, Material> materialByName)
    {
        if (!modelByName.TryGetValue(modelName, out var modelPath))
        {
            return null;
        }

        var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
        if (model == null)
        {
            return null;
        }

        var instance = PrefabUtility.InstantiatePrefab(model) as GameObject;
        if (instance == null)
        {
            return null;
        }

        instance.name = modelName;
        instance.transform.SetParent(parent, false);
        instance.transform.localPosition = localPosition;
        instance.transform.localRotation = Quaternion.Euler(localEuler);
        instance.transform.localScale = localScale;
        AssignMaterials(instance, materialByName);
        MarkStatic(instance);
        return instance;
    }

    static void AddRing(
        Dictionary<string, string> modelByName,
        Dictionary<string, Material> materialByName,
        Transform parent,
        string groupName,
        string[] modelNames,
        int count,
        float radius,
        float y,
        float baseScale,
        float angleOffset)
    {
        var group = CreateChild(parent, groupName);
        for (var i = 0; i < count; i++)
        {
            var angle = angleOffset + i * 360f / count;
            var radians = angle * Mathf.Deg2Rad;
            var ringJitter = ((i % 3) - 1) * 3.5f;
            var currentRadius = radius + ringJitter;
            var position = new Vector3(Mathf.Sin(radians) * currentRadius, y, Mathf.Cos(radians) * currentRadius);
            var scale = baseScale * (0.9f + (i % 5) * 0.055f);
            var modelName = modelNames[i % modelNames.Length];
            AddModelInstance(
                modelByName,
                modelName,
                group.transform,
                position,
                new Vector3(0f, angle + 180f, 0f),
                new Vector3(scale, scale, scale),
                materialByName);
        }
    }

    static GameObject BuildPrefab(string root, Dictionary<string, Material> materialByName, string prefabName, Func<string, bool> includeModel)
    {
        var prefabPath = root + "/Prefabs/" + prefabName;
        var container = new GameObject("KamenMap_Root");
        var modelPaths = Directory.GetFiles(ToFullPath(root + "/Models"), "*.obj", SearchOption.AllDirectories)
            .Select(path => "Assets" + path.Replace(Application.dataPath, string.Empty).Replace("\\", "/"))
            .Where(includeModel)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var modelPath in modelPaths)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (model == null)
            {
                continue;
            }
            var instance = PrefabUtility.InstantiatePrefab(model) as GameObject;
            if (instance == null)
            {
                continue;
            }
            instance.transform.SetParent(container.transform, false);
            AssignMaterials(instance, materialByName);
            MarkStatic(instance);
        }

        var prefab = PrefabUtility.SaveAsPrefabAsset(container, prefabPath);
        UnityEngine.Object.DestroyImmediate(container);
        return prefab;
    }

    static bool IsPlaceableModel(string assetPath)
    {
        var lower = assetPath.ToLowerInvariant();
        return lower.Contains("/bg_rad_kamen_")
            && !lower.Contains("lowpoly");
    }

    static bool IsArenaModel(string assetPath)
    {
        var lower = Path.GetFileNameWithoutExtension(assetPath).ToLowerInvariant();
        var package = assetPath.ToLowerInvariant();
        return package.Contains("/bg_rad_kamen_")
            && (lower.Contains("kamen")
            || lower.Contains("rockfloor")
            || lower.Contains("rockpillar")
            || lower.Contains("floor")
            || lower.Contains("pillar")
            || lower.Contains("rock")
            || lower.Contains("stone")
            || lower.Contains("wall")
            || lower.Contains("gate"))
            && !lower.Contains("lowpoly");
    }

    static void AssignMaterials(GameObject instance, Dictionary<string, Material> materialByName)
    {
        foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
        {
            var slots = renderer.sharedMaterials;
            for (var i = 0; i < slots.Length; i++)
            {
                var slot = slots[i];
                if (slot == null)
                {
                    continue;
                }
                var cleanName = CleanMaterialName(slot.name);
                if (materialByName.TryGetValue(cleanName, out var material))
                {
                    slots[i] = material;
                }
            }
            renderer.sharedMaterials = slots;
        }
    }

    static void MarkStatic(GameObject go)
    {
        foreach (Transform child in go.GetComponentsInChildren<Transform>(true))
        {
            GameObjectUtility.SetStaticEditorFlags(child.gameObject,
                StaticEditorFlags.BatchingStatic |
                StaticEditorFlags.OccluderStatic |
                StaticEditorFlags.OccludeeStatic |
                StaticEditorFlags.ReflectionProbeStatic);
        }
    }

    static void BuildScene(string root, GameObject prefab)
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        GameObject prefabInstance = null;
        if (prefab != null)
        {
            prefabInstance = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
        }

        var cameraObject = new GameObject("Camera");
        var camera = cameraObject.AddComponent<Camera>();
        cameraObject.tag = "MainCamera";
        camera.nearClipPlane = 0.1f;
        camera.farClipPlane = 2000f;
        camera.allowHDR = true;
        camera.backgroundColor = new Color(0.018f, 0.021f, 0.029f, 1f);
        camera.depthTextureMode = DepthTextureMode.DepthNormals;
        FitCameraToContent(cameraObject.transform, camera, prefabInstance);
        ConfigureCameraPostProcessing(cameraObject);

        RenderSettings.skybox = CreateSkybox(root);
        ConfigureRenderSettings();
        EditorSceneManager.SaveScene(scene, root + "/Scenes/KamenMap.unity");
    }

    static GameObject CreateDirectionalLight(Transform parent = null)
    {
        var lightObject = new GameObject("Kamen Directional Light");
        if (parent != null)
        {
            lightObject.transform.SetParent(parent, false);
        }
        var light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.05f;
        light.color = new Color(0.62f, 0.72f, 0.95f, 1f);
        lightObject.transform.rotation = Quaternion.Euler(48f, -36f, 0f);
        return lightObject;
    }

    static void ConfigureCameraPostProcessing(GameObject cameraObject)
    {
        var cameraData = cameraObject.GetComponent<UniversalAdditionalCameraData>();
        if (cameraData == null)
        {
            cameraData = cameraObject.AddComponent<UniversalAdditionalCameraData>();
        }
        cameraData.renderPostProcessing = true;
        cameraData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        cameraData.antialiasingQuality = AntialiasingQuality.High;
    }

    static void ConfigureRenderSettings()
    {
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.055f, 0.063f, 0.086f, 1f);
        RenderSettings.ambientEquatorColor = new Color(0.09f, 0.065f, 0.065f, 1f);
        RenderSettings.ambientGroundColor = new Color(0.025f, 0.022f, 0.024f, 1f);
        RenderSettings.reflectionIntensity = 0.38f;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = new Color(0.025f, 0.028f, 0.036f, 1f);
        RenderSettings.fogDensity = 0.0035f;
    }

    static void CreateKamenLightingRig(GameObject content, Transform parent = null)
    {
        var bounds = CalculateBounds(content);
        var center = bounds.HasValue ? bounds.Value.center : Vector3.zero;
        var radius = bounds.HasValue ? Mathf.Max(bounds.Value.extents.x, bounds.Value.extents.z) : 80f;
        radius = Mathf.Clamp(radius, 45f, 180f);

        AddPointLight("Kamen Red Crack Fill", center + new Vector3(-radius * 0.35f, 16f, radius * 0.18f),
            new Color(1f, 0.13f, 0.08f, 1f), radius * 1.85f, 4.2f, parent);
        AddPointLight("Kamen Cold Blue Rim", center + new Vector3(radius * 0.25f, 22f, -radius * 0.45f),
            new Color(0.28f, 0.42f, 0.95f, 1f), radius * 2.1f, 2.4f, parent);
        AddPointLight("Kamen Low Ember", center + new Vector3(0f, 4f, 0f),
            new Color(1f, 0.22f, 0.09f, 1f), radius * 1.25f, 1.6f, parent);

        var probeObject = new GameObject("Kamen Reflection Probe");
        if (parent != null)
        {
            probeObject.transform.SetParent(parent, true);
        }
        var probe = probeObject.AddComponent<ReflectionProbe>();
        probe.mode = ReflectionProbeMode.Realtime;
        probe.refreshMode = ReflectionProbeRefreshMode.OnAwake;
        probe.intensity = 0.45f;
        probe.size = new Vector3(radius * 2.6f, radius * 0.9f, radius * 2.6f);
        probeObject.transform.position = center + Vector3.up * 8f;
        probe.center = Vector3.zero;
    }

    static void AddPointLight(string name, Vector3 position, Color color, float range, float intensity, Transform parent = null)
    {
        var lightObject = new GameObject(name);
        if (parent != null)
        {
            lightObject.transform.SetParent(parent, true);
        }
        lightObject.transform.position = position;
        var light = lightObject.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = color;
        light.range = range;
        light.intensity = intensity;
        light.shadows = LightShadows.Soft;
        light.shadowStrength = 0.42f;
    }

    static void CreatePostProcessingVolume(string root, Transform parent = null)
    {
        var profilePath = root + "/Materials/KamenMap_PostProcessProfile.asset";
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, profilePath);
        }

        var tonemapping = EnsureVolumeOverride<Tonemapping>(profile);
        tonemapping.mode.Override(TonemappingMode.ACES);

        var color = EnsureVolumeOverride<ColorAdjustments>(profile);
        color.postExposure.Override(0.12f);
        color.contrast.Override(18f);
        color.saturation.Override(-4f);
        color.colorFilter.Override(new Color(0.82f, 0.88f, 1f, 1f));

        var bloom = EnsureVolumeOverride<Bloom>(profile);
        bloom.threshold.Override(0.78f);
        bloom.intensity.Override(0.38f);
        bloom.scatter.Override(0.62f);
        bloom.tint.Override(new Color(1f, 0.38f, 0.26f, 1f));

        var vignette = EnsureVolumeOverride<Vignette>(profile);
        vignette.color.Override(new Color(0.02f, 0.018f, 0.023f, 1f));
        vignette.intensity.Override(0.32f);
        vignette.smoothness.Override(0.46f);

        var chromaticAberration = EnsureVolumeOverride<ChromaticAberration>(profile);
        chromaticAberration.intensity.Override(0.04f);

        EditorUtility.SetDirty(profile);

        var volumeObject = new GameObject("Kamen Global Post Process");
        if (parent != null)
        {
            volumeObject.transform.SetParent(parent, false);
        }
        var volume = volumeObject.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 100f;
        volume.sharedProfile = profile;
    }

    static T EnsureVolumeOverride<T>(VolumeProfile profile) where T : VolumeComponent
    {
        if (!profile.TryGet(out T component))
        {
            component = profile.Add<T>(true);
        }
        component.active = true;
        return component;
    }

    static void FitCameraToContent(Transform cameraTransform, Camera camera, GameObject content)
    {
        var bounds = CalculateBounds(content);
        if (!bounds.HasValue)
        {
            cameraTransform.position = new Vector3(0f, 18f, -28f);
            cameraTransform.rotation = Quaternion.Euler(58f, 0f, 0f);
            return;
        }

        var b = bounds.Value;
        var size = Mathf.Max(b.size.x, b.size.y, b.size.z);
        if (size < 1f)
        {
            size = 20f;
        }

        var distance = Mathf.Clamp(size * 1.25f, 35f, 600f);
        cameraTransform.position = b.center + new Vector3(0f, distance * 0.6f, -distance);
        cameraTransform.rotation = Quaternion.LookRotation(b.center - cameraTransform.position, Vector3.up);
        camera.farClipPlane = Mathf.Max(2000f, distance * 4f);
    }

    static Bounds? CalculateBounds(GameObject content)
    {
        if (content == null)
        {
            return null;
        }

        var renderers = content.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            return null;
        }

        var bounds = renderers[0].bounds;
        for (var i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }
        return bounds;
    }

    static Material CreateSkybox(string root)
    {
        var skyboxPath = root + "/Materials/KamenMap_Skybox.mat";
        var skybox = AssetDatabase.LoadAssetAtPath<Material>(skyboxPath);
        var texture = PickSkyTexture(root);
        var shader = texture != null ? Shader.Find("Skybox/Panoramic") : Shader.Find("Skybox/Procedural");
        if (shader == null)
        {
            shader = Shader.Find("Skybox/Procedural") ?? Shader.Find("Standard");
        }

        if (skybox == null)
        {
            skybox = new Material(shader) { name = "KamenMap_Skybox" };
            AssetDatabase.CreateAsset(skybox, skyboxPath);
        }
        else
        {
            skybox.shader = shader;
        }

        if (texture != null && skybox.HasProperty("_MainTex"))
        {
            skybox.SetTexture("_MainTex", texture);
        }
        if (skybox.HasProperty("_Tint"))
        {
            skybox.SetColor("_Tint", new Color(0.42f, 0.46f, 0.55f, 1f));
        }
        if (skybox.HasProperty("_Exposure"))
        {
            skybox.SetFloat("_Exposure", 1.1f);
        }
        EditorUtility.SetDirty(skybox);
        return skybox;
    }

    static Texture2D PickSkyTexture(string root)
    {
        var textureRoot = ToFullPath(root + "/Textures");
        if (!Directory.Exists(textureRoot))
        {
            return null;
        }
        var preferred = Directory.GetFiles(textureRoot, "*.png", SearchOption.AllDirectories)
            .Select(path => "Assets" + path.Replace(Application.dataPath, string.Empty).Replace("\\", "/"))
            .OrderByDescending(path => ScoreSkyName(Path.GetFileNameWithoutExtension(path)))
            .FirstOrDefault(path => ScoreSkyName(Path.GetFileNameWithoutExtension(path)) > 0);
        return string.IsNullOrEmpty(preferred) ? null : AssetDatabase.LoadAssetAtPath<Texture2D>(preferred);
    }

    static int ScoreSkyName(string name)
    {
        var lower = name.ToLowerInvariant();
        var score = 0;
        if (lower.Contains("sky")) score += 20;
        if (lower.Contains("hdr")) score += 15;
        if (lower.Contains("cube")) score += 10;
        if (lower.Contains("cloud")) score += 8;
        if (lower.Contains("env")) score += 4;
        if (lower.Contains("kamen")) score += 2;
        return score;
    }

    class MaterialRow
    {
        public string Package;
        public string Name;
        public string Diffuse;
        public string Normal;
        public string Specular;
        public string Emissive;
    }

    enum TextureImportRole
    {
        Color = 0,
        Data = 1,
        Normal = 2
    }

    class MaterialAuditRow
    {
        public string Package;
        public string Name;
        public string Shader;
        public string Diffuse;
        public string Normal;
        public string Specular;
        public string Emissive;
        public bool DiffuseAssigned;
        public bool NormalAssigned;
        public bool SpecularAssigned;
        public bool EmissiveAssigned;
        public bool AlphaClip;
        public float BumpScale;
        public float Smoothness;
        public float WorkflowMode;
    }
}
