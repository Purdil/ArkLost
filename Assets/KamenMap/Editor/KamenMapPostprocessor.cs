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
    const string BuildVersion = "gate3-central-arena-only-20260613-08";

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
        EnsureFolder(root, "GeneratedMeshes");

        var rows = ReadMaterialRows(root + "/SourceMetadata/material_manifest.csv")
            .Where(row => IsGate3CuratedPackage(row.Package))
            .ToList();
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
        // Keep the user's currently open scene stable; the generated prefabs are the deliverable.

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("KamenMap import complete. Use " + root + "/Prefabs/KamenMap_WithGraphics.prefab.");
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
        if (lower.Contains("gate") || lower.Contains("pillar") || lower.Contains("wall") || lower.Contains("column"))
        {
            return new Color(0.34f, 0.52f, 0.82f, 1f);
        }
        if (lower.Contains("rock") || lower.Contains("floor") || lower.Contains("stone") || lower.Contains("kamen"))
        {
            return new Color(0.18f, 0.28f, 0.44f, 1f);
        }
        if (lower.Contains("sky") || lower.Contains("hdr"))
        {
            return new Color(0.55f, 0.62f, 0.78f, 1f);
        }
        return new Color(0.18f, 0.26f, 0.38f, 1f);
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
            if (lower.Contains("floor07_sm") && size.x > 150f && size.z > 150f) warnings.Add("large_outer_abyss_shell_candidate");
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
        var container = new GameObject("KamenMap_Gate3_Central_Arena_Only");

        var arenaMaterial = CreateGate3CentralArenaMaterial(root);
        var rimMaterial = CreateGeneratedMaterial(root, "KamenGate3_IrregularBlackArenaRim",
            new Color(0.010f, 0.014f, 0.022f, 1f), 0f, 0.38f, new Color(0.015f, 0.035f, 0.075f, 1f), 0.28f);

        var foundation = CreateChild(container.transform, "Gate3_Central_Round_Arena_DarkBlue_NoRed_NoOuterPlanet");
        var mainFloor = CreateChild(foundation.transform, "Central_Playable_Circular_Plate_Only");
        AddGate3CentralArenaDisc(mainFloor.transform, root, arenaMaterial, rimMaterial);

        var prefab = PrefabUtility.SaveAsPrefabAsset(container, prefabPath);
        UnityEngine.Object.DestroyImmediate(container);
        return prefab;
    }

    static void AddGate3CentralArenaDisc(Transform parent, string root, Material arenaMaterial, Material rimMaterial)
    {
        var arenaMesh = CreateGate3ArenaDiscMeshAsset(root, "Gate3_Central_Arena_OrganicRelief_Mesh", 29.2f, 96, 256);
        AddMeshObject(parent, "Generated_Kamen3_Central_Dark_Organic_Arena_Disc", arenaMesh,
            new Vector3(0f, 0.24f, 0f), Quaternion.identity, Vector3.one, arenaMaterial);

        var rimMesh = CreateGate3IrregularRingMeshAsset(root, "Gate3_Central_Arena_Ragged_Outer_Lip_Mesh", 28.6f, 31.1f, 256);
        AddMeshObject(parent, "Generated_Kamen3_Ragged_Black_Outer_Arena_Lip", rimMesh,
            new Vector3(0f, 0.31f, 0f), Quaternion.identity, Vector3.one, rimMaterial);

        AddRing(parent, "Generated_Kamen3_Thin_Dark_Playable_Boundary", root, 27.9f, 28.6f, 192,
            new Vector3(0f, 0.35f, 0f), rimMaterial);
    }

    static Material CreateGate3CentralArenaMaterial(string root)
    {
        var albedo = CreateGate3ArenaTextureAsset(root, "Gate3_CentralArena_DarkOrganic_Albedo", 1024, false);
        var normal = CreateGate3ArenaTextureAsset(root, "Gate3_CentralArena_DarkOrganic_Normal", 1024, true);
        var material = CreateGeneratedMaterial(root, "KamenGate3_CentralArena_DarkBlueOrganicFloor",
            Color.white, 0f, 0.24f, Color.black, 0f);

        ApplyTexture(material, "_BaseMap", "_MainTex", albedo);
        ApplyTexture(material, "_BumpMap", "_BumpMap", normal);
        SetFloatIfPresent(material, "_BumpScale", 1.75f);
        SetFloatIfPresent(material, "_Smoothness", 0.24f);
        SetFloatIfPresent(material, "_Glossiness", 0.24f);
        SetFloatIfPresent(material, "_OcclusionStrength", 0.92f);
        material.EnableKeyword("_NORMALMAP");
        material.DisableKeyword("_EMISSION");
        SetColorIfPresent(material, "_EmissionColor", Color.black);
        EditorUtility.SetDirty(material);
        return material;
    }

    static Texture2D CreateGate3ArenaTextureAsset(string root, string name, int size, bool normalMap)
    {
        EnsureFolder(root, "Textures");
        EnsureNestedFolder(root + "/Textures", "Generated");
        var path = root + "/Textures/Generated/" + Sanitize(name) + ".asset";
        AssetDatabase.DeleteAsset(path);

        var texture = new Texture2D(size, size, TextureFormat.RGBA32, true, normalMap) { name = name };
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Trilinear;
        texture.anisoLevel = 8;

        var pixels = new Color32[size * size];
        if (normalMap)
        {
            var heights = new float[size * size];
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var nx = (x + 0.5f) / size * 2f - 1f;
                    var nz = (y + 0.5f) / size * 2f - 1f;
                    heights[y * size + x] = Gate3ArenaHeight(nx, nz);
                }
            }

            const float normalStrength = 9.5f;
            for (var y = 0; y < size; y++)
            {
                var y0 = Mathf.Max(0, y - 1);
                var y1 = Mathf.Min(size - 1, y + 1);
                for (var x = 0; x < size; x++)
                {
                    var x0 = Mathf.Max(0, x - 1);
                    var x1 = Mathf.Min(size - 1, x + 1);
                    var dx = heights[y * size + x1] - heights[y * size + x0];
                    var dz = heights[y1 * size + x] - heights[y0 * size + x];
                    var n = new Vector3(-dx * normalStrength, -dz * normalStrength, 1f).normalized;
                    pixels[y * size + x] = new Color32(
                        (byte)Mathf.RoundToInt(Mathf.Clamp01(n.x * 0.5f + 0.5f) * 255f),
                        (byte)Mathf.RoundToInt(Mathf.Clamp01(n.y * 0.5f + 0.5f) * 255f),
                        (byte)Mathf.RoundToInt(Mathf.Clamp01(n.z * 0.5f + 0.5f) * 255f),
                        255);
                }
            }
        }
        else
        {
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var nx = (x + 0.5f) / size * 2f - 1f;
                    var nz = (y + 0.5f) / size * 2f - 1f;
                    pixels[y * size + x] = Gate3ArenaAlbedo(nx, nz);
                }
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(true, false);
        AssetDatabase.CreateAsset(texture, path);
        EditorUtility.SetDirty(texture);
        return texture;
    }

    static Color32 Gate3ArenaAlbedo(float x, float z)
    {
        var r = Mathf.Sqrt(x * x + z * z);
        if (r > 1f)
        {
            return new Color32(2, 4, 9, 255);
        }

        var centerGlow = Mathf.Exp(-r * r * 15f);
        var outerShade = Mathf.SmoothStep(0.45f, 1f, r);
        var vein = Gate3ArenaVeinPattern(x, z) * 0.72f;
        var fine = Mathf.PerlinNoise(x * 11.7f + 18.2f, z * 11.7f + 43.5f) - 0.5f;
        var broad = Mathf.PerlinNoise(x * 3.2f + 7.1f, z * 3.2f + 91.4f) - 0.5f;
        var edgeFalloff = Mathf.SmoothStep(0.78f, 1f, r);

        var red = 0.010f + centerGlow * 0.014f + vein * 0.008f + fine * 0.003f - edgeFalloff * 0.006f;
        var green = 0.032f + centerGlow * 0.046f + vein * 0.030f + fine * 0.007f + broad * 0.008f - outerShade * 0.013f;
        var blue = 0.078f + centerGlow * 0.120f + vein * 0.066f + fine * 0.012f + broad * 0.012f - outerShade * 0.026f;

        var darkCuts = Gate3ArenaDarkCutPattern(x, z) * Mathf.SmoothStep(0.22f, 0.92f, r);
        red -= darkCuts * 0.007f;
        green -= darkCuts * 0.018f;
        blue -= darkCuts * 0.036f;

        return new Color32(
            (byte)Mathf.RoundToInt(Mathf.Clamp01(red) * 255f),
            (byte)Mathf.RoundToInt(Mathf.Clamp01(green) * 255f),
            (byte)Mathf.RoundToInt(Mathf.Clamp01(blue) * 255f),
            255);
    }

    static float Gate3ArenaHeight(float x, float z)
    {
        var r = Mathf.Sqrt(x * x + z * z);
        if (r > 1f)
        {
            return 0f;
        }

        var vein = Gate3ArenaVeinPattern(x, z);
        var darkCuts = Gate3ArenaDarkCutPattern(x, z);
        var center = Mathf.Exp(-r * r * 18f);
        var fine = Mathf.PerlinNoise(x * 17.2f + 12.4f, z * 17.2f + 61.9f) - 0.5f;
        var petal = Mathf.Sin(r * 31f + Mathf.Atan2(x, z) * 8f) * 0.035f * Mathf.SmoothStep(0.14f, 0.86f, r);
        return center * 0.35f + vein * 1.15f - darkCuts * 0.42f + fine * 0.09f + petal;
    }

    static float Gate3ArenaVeinPattern(float x, float z)
    {
        var r = Mathf.Sqrt(x * x + z * z);
        if (r <= 0.001f || r > 1f)
        {
            return 0f;
        }

        var theta = Mathf.Atan2(x, z);
        var mask = Mathf.SmoothStep(0.08f, 0.34f, r) * (1f - Mathf.SmoothStep(0.93f, 1f, r));
        var radialBreak = 0.70f + 0.30f * Mathf.Sin(r * 27f + Mathf.Sin(theta * 5f) * 1.7f);
        var primary = Gate3RadialVein(theta, r, 16, 0.030f, 0.00f);
        var secondary = Gate3RadialVein(theta, r, 32, 0.014f, 0.19f) * 0.48f;
        var tertiary = Gate3RadialVein(theta, r, 48, 0.0085f, 0.37f) * 0.26f;
        return Mathf.Clamp01((primary + secondary + tertiary) * mask * radialBreak);
    }

    static float Gate3ArenaDarkCutPattern(float x, float z)
    {
        var r = Mathf.Sqrt(x * x + z * z);
        if (r <= 0.001f || r > 1f)
        {
            return 0f;
        }

        var theta = Mathf.Atan2(x, z);
        var step = Mathf.PI * 2f / 12f;
        var warped = theta + 0.18f * Mathf.Sin(r * 8.5f) - 0.10f * Mathf.Sin(r * 19f + theta * 3f);
        var nearest = Mathf.Round(warped / step) * step;
        var d = DeltaAngleRad(warped, nearest);
        var width = Mathf.Lerp(0.020f, 0.009f, r);
        var cut = Mathf.Exp(-(d * d) / (width * width));
        return Mathf.Clamp01(cut * Mathf.SmoothStep(0.30f, 0.95f, r) * (1f - Mathf.SmoothStep(0.96f, 1f, r)));
    }

    static float Gate3RadialVein(float theta, float r, int count, float baseWidth, float offset)
    {
        var step = Mathf.PI * 2f / count;
        var warped = theta
            + offset
            + 0.25f * Mathf.Sin(r * 5.4f + offset * 9f)
            + 0.075f * Mathf.Sin(r * 18.0f + offset * 17f);
        var nearest = Mathf.Round(warped / step) * step;
        var d = DeltaAngleRad(warped, nearest);
        var width = Mathf.Lerp(baseWidth * 1.65f, baseWidth * 0.45f, r);
        return Mathf.Exp(-(d * d) / (width * width));
    }

    static float DeltaAngleRad(float a, float b)
    {
        return Mathf.Abs(Mathf.Repeat(a - b + Mathf.PI, Mathf.PI * 2f) - Mathf.PI);
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
        settings.fogColor = new Color(0.028f, 0.035f, 0.055f, 1f);
        settings.fogDensity = 0.0022f;
        settings.ambientSkyColor = new Color(0.064f, 0.084f, 0.132f, 1f);
        settings.ambientEquatorColor = new Color(0.058f, 0.078f, 0.118f, 1f);
        settings.ambientGroundColor = new Color(0.026f, 0.030f, 0.043f, 1f);
        settings.reflectionIntensity = 0.42f;

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

    static GameObject AddSurfaceLayer(
        Dictionary<string, string> modelByName,
        string modelName,
        Transform parent,
        float targetTopY,
        Vector3 localEuler,
        Vector3 localScale,
        Dictionary<string, Material> materialByName)
    {
        var instance = AddModelInstance(modelByName, modelName, parent, Vector3.zero, localEuler, localScale, materialByName);
        if (instance != null)
        {
            AlignRendererBoundsTop(instance, targetTopY);
        }
        return instance;
    }

    static void AlignRendererBoundsTop(GameObject instance, float targetTopY)
    {
        var renderers = instance.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            return;
        }

        var bounds = renderers[0].bounds;
        for (var i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }
        instance.transform.position += Vector3.up * (targetTopY - bounds.max.y);
    }

    static void AlignRendererBoundsBottomCenterXZ(GameObject instance, float targetBottomY, Vector2 targetCenterXZ)
    {
        if (instance == null)
        {
            return;
        }

        var renderers = instance.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            return;
        }

        var bounds = renderers[0].bounds;
        for (var i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        instance.transform.position += new Vector3(
            targetCenterXZ.x - bounds.center.x,
            targetBottomY - bounds.min.y,
            targetCenterXZ.y - bounds.center.z);
    }

    static GameObject CreateChild(Transform parent, string name)
    {
        var child = new GameObject(name);
        child.transform.SetParent(parent, false);
        return child;
    }

    static void AddColosseumWallRing(
        Dictionary<string, string> modelByName,
        Dictionary<string, Material> materialByName,
        Transform parent)
    {
        var wallRing = CreateChild(parent, "Repeated_Curved_Colosseum_Wall_Sections");
        const int wallCount = 44;
        const float wallRadius = 59f;
        for (var i = 0; i < wallCount; i++)
        {
            var angle = i * 360f / wallCount;
            if (Mathf.Abs(Mathf.DeltaAngle(angle, 0f)) < 14f)
            {
                continue;
            }

            var radians = angle * Mathf.Deg2Rad;
            var position = new Vector3(Mathf.Sin(radians) * wallRadius, 0f, Mathf.Cos(radians) * wallRadius);
            var yaw = angle + 90f;
            var modelName = (i % 3 == 0) ? "bg_rad_kamen_wall01_sm_hhk" : "bg_rad_kamen_wall01_sm_alchemy";
            var scale = (i % 3 == 0) ? 2.35f : 2.05f;
            AddModelInstance(modelByName, modelName, wallRing.transform, position, new Vector3(0f, yaw, 0f), new Vector3(scale, scale, scale), materialByName);
        }

        var supports = CreateChild(parent, "Repeated_Colosseum_Pillars_And_Columns");
        const int pillarCount = 18;
        for (var i = 0; i < pillarCount; i++)
        {
            var angle = i * 360f / pillarCount;
            var radians = angle * Mathf.Deg2Rad;
            var position = new Vector3(Mathf.Sin(radians) * 61.5f, 0f, Mathf.Cos(radians) * 61.5f);
            AddModelInstance(modelByName, "bg_rad_kamen_column02_sm_jjh", supports.transform, position, new Vector3(0f, angle + 90f, 0f), new Vector3(1.55f, 1.55f, 1.55f), materialByName);
        }

        for (var i = 0; i < 12; i++)
        {
            var angle = 15f + i * 360f / 12f;
            var radians = angle * Mathf.Deg2Rad;
            var position = new Vector3(Mathf.Sin(radians) * 64f, 7.5f, Mathf.Cos(radians) * 64f);
            AddModelInstance(modelByName, "bg_rad_kamen_pillar10_sm_alchemy", supports.transform, position, new Vector3(0f, angle + 90f, 0f), Vector3.one, materialByName);
        }
    }

    static void AddReferenceSpikedBarricade(
        Dictionary<string, string> modelByName,
        Dictionary<string, Material> materialByName,
        Transform parent)
    {
        var spikes = CreateChild(parent, "Reference_Radial_Thorn_Barricade_From_Attached_Image");

        for (var i = 0; i < 18; i++)
        {
            var angle = 7f + i * 360f / 18f;
            AddRadialModel(modelByName, materialByName, spikes.transform, "bg_rad_kamen_pillar09_sm_hhk",
                angle, 42.5f, 5.9f, angle + 180f, new Vector3(2.8f, 2.8f, 2.8f), new Vector3((i % 2 == 0) ? -10f : 12f, 0f, 0f));
        }

        for (var i = 0; i < 14; i++)
        {
            var angle = 18f + i * 360f / 14f;
            AddRadialModel(modelByName, materialByName, spikes.transform, "bg_rad_kamen_pillar10_sm_alchemy",
                angle, 58f, 8.5f, angle + 180f, new Vector3(2.35f, 2.35f, 2.35f), new Vector3((i % 2 == 0) ? 6f : -8f, 0f, 0f));
        }

        for (var i = 0; i < 12; i++)
        {
            var angle = 5f + i * 360f / 12f;
            AddRadialModel(modelByName, materialByName, spikes.transform, "bg_rad_kamen_pillar08_sm_alchemy",
                angle, 70f, 0.05f, angle + 180f, new Vector3(2.05f, 2.05f, 2.05f), Vector3.zero);
        }

        for (var i = 0; i < 10; i++)
        {
            var angle = 13f + i * 360f / 10f;
            AddRadialModel(modelByName, materialByName, spikes.transform, "bg_rad_kamen_rockpillar01_sm",
                angle, 78f, 7.2f, angle + 180f, new Vector3(2.5f, 2.5f, 2.5f), new Vector3(0f, 0f, (i % 2 == 0) ? 9f : -9f));
        }

        var outerMass = CreateChild(parent, "Reference_Large_Outer_Black_Barrier_Masses");
        for (var i = 0; i < 8; i++)
        {
            var angle = 22.5f + i * 45f;
            AddRadialModel(modelByName, materialByName, outerMass.transform, "bg_rad_kamen_housedome01a_sm_hhk",
                angle, 96f, -2.0f, angle + 180f, new Vector3(1.28f, 1.28f, 1.28f), Vector3.zero);
        }

        for (var i = 0; i < 12; i++)
        {
            var angle = 15f + i * 30f;
            AddRadialModel(modelByName, materialByName, outerMass.transform, "bg_rad_kamen_rockfloor01a_sm",
                angle, 64f, -1.1f, angle + 90f, new Vector3(3.4f, 2.3f, 3.4f), Vector3.zero);
        }

        var northCrown = CreateChild(parent, "Reference_North_Crown_Gate_And_Spires");
        AddModelInstance(modelByName, "bg_rad_kamen_gate01_sm_jjh", northCrown.transform, new Vector3(0f, -0.4f, 86f), new Vector3(0f, 180f, 0f), new Vector3(2.35f, 2.35f, 2.35f), materialByName);
        AddModelInstance(modelByName, "bg_rad_kamen_pillar08_sm_alchemy", northCrown.transform, new Vector3(-24f, 0f, 82f), new Vector3(0f, 160f, 0f), new Vector3(2.2f, 2.2f, 2.2f), materialByName);
        AddModelInstance(modelByName, "bg_rad_kamen_pillar08_sm_alchemy", northCrown.transform, new Vector3(24f, 0f, 82f), new Vector3(0f, 200f, 0f), new Vector3(2.2f, 2.2f, 2.2f), materialByName);
        AddModelInstance(modelByName, "bg_rad_kamen_deco02b_sm_kjs", northCrown.transform, new Vector3(0f, 0.5f, 66f), new Vector3(0f, 180f, 0f), new Vector3(3.4f, 3.4f, 3.4f), materialByName);
    }

    static GameObject AddRadialModel(
        Dictionary<string, string> modelByName,
        Dictionary<string, Material> materialByName,
        Transform parent,
        string modelName,
        float angle,
        float radius,
        float y,
        float yaw,
        Vector3 scale,
        Vector3 extraEuler)
    {
        var radians = angle * Mathf.Deg2Rad;
        var position = new Vector3(Mathf.Sin(radians) * radius, y, Mathf.Cos(radians) * radius);
        return AddModelInstance(modelByName, modelName, parent, position, new Vector3(extraEuler.x, yaw, extraEuler.z), scale, materialByName);
    }

    static void AddReferenceBlueSealAndChains(string root, Transform parent, Material blueLightMaterial, Material blueBeamMaterial, Material abyssMaterial)
    {
        var rig = CreateChild(parent, "Reference_Blue_Seal_Beams_Chains_And_Nodes");

        AddEmissiveOrb(rig.transform, "Center_Faint_Blue_Core", new Vector3(0f, 1.15f, 0f), new Vector3(3.8f, 0.42f, 3.8f), blueLightMaterial);
        AddPointLight("Gate3 Reference Center Cold Glow", new Vector3(0f, 7f, 0f), new Color(0.23f, 0.48f, 0.95f, 1f), 34f, 1.15f, rig.transform);

        for (var i = 0; i < 10; i++)
        {
            var angle = 8f + i * 36f;
            AddRadialObsidianChain(rig.transform, "Radial_Black_Chain_" + i.ToString("00"), angle, 30f, 91f, 1.0f, abyssMaterial);
            AddRadialBlueSealNode(rig.transform, "Outer_Blue_Seal_Node_" + i.ToString("00"), angle, 82f, blueLightMaterial, blueBeamMaterial);
        }

        for (var i = 0; i < 6; i++)
        {
            var angle = 26f + i * 60f;
            AddRadialObsidianChain(rig.transform, "Long_Outer_Barrier_Chain_" + i.ToString("00"), angle, 56f, 118f, 6.5f, abyssMaterial);
        }
    }

    static void AddRadialObsidianChain(Transform parent, string name, float angle, float innerRadius, float outerRadius, float y, Material material)
    {
        var radians = angle * Mathf.Deg2Rad;
        var direction = new Vector3(Mathf.Sin(radians), 0f, Mathf.Cos(radians));
        var centerRadius = (innerRadius + outerRadius) * 0.5f;
        var length = outerRadius - innerRadius;
        var chain = AddBox(parent, name, direction * centerRadius + Vector3.up * y, new Vector3(0.42f, 0.18f, length), material);
        chain.transform.localRotation = Quaternion.Euler(0f, angle, 0f);
    }

    static void AddRadialBlueSealNode(Transform parent, string name, float angle, float radius, Material orbMaterial, Material beamMaterial)
    {
        var radians = angle * Mathf.Deg2Rad;
        var position = new Vector3(Mathf.Sin(radians) * radius, 2.2f, Mathf.Cos(radians) * radius);
        AddEmissiveOrb(parent, name + "_Orb", position, new Vector3(2.3f, 0.7f, 2.3f), orbMaterial);
        AddBox(parent, name + "_Vertical_Beam", new Vector3(position.x, 30f, position.z), new Vector3(0.48f, 58f, 0.48f), beamMaterial);
        AddPointLight(name + " Light", new Vector3(position.x, 9f, position.z), new Color(0.20f, 0.55f, 1f, 1f), 32f, 1.35f, parent);
    }

    static Material CreateGeneratedMaterial(string root, string name, Color color, float metallic, float smoothness, Color emission, float emissionStrength)
    {
        EnsureNestedFolder(root + "/Materials", "Generated");
        var materialPath = root + "/Materials/Generated/" + Sanitize(name) + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material == null)
        {
            material = new Material(FindLitShader()) { name = name };
            AssetDatabase.CreateAsset(material, materialPath);
        }
        else
        {
            material.shader = FindLitShader();
        }

        SetColorIfPresent(material, "_BaseColor", color);
        SetColorIfPresent(material, "_Color", color);
        SetFloatIfPresent(material, "_Metallic", metallic);
        SetFloatIfPresent(material, "_Smoothness", smoothness);
        SetFloatIfPresent(material, "_Glossiness", smoothness);
        if (emissionStrength > 0f)
        {
            material.EnableKeyword("_EMISSION");
            SetColorIfPresent(material, "_EmissionColor", emission * emissionStrength);
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
        else
        {
            material.DisableKeyword("_EMISSION");
            SetColorIfPresent(material, "_EmissionColor", Color.black);
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        }
        EditorUtility.SetDirty(material);
        return material;
    }

    static Material CreateTexturedGeneratedMaterial(
        string root,
        string name,
        Color color,
        string diffusePath,
        string normalPath,
        string emissivePath,
        float bumpScale,
        float smoothness,
        Color emission,
        float emissionStrength,
        Vector2 textureScale)
    {
        var material = CreateGeneratedMaterial(root, name, color, 0f, smoothness, emission, emissionStrength);
        var diffuse = LoadTexture(root, diffusePath);
        var normal = LoadTexture(root, normalPath);
        var emissive = LoadTexture(root, emissivePath);

        if (diffuse != null)
        {
            ApplyTexture(material, "_BaseMap", "_MainTex", diffuse);
            SetTextureScaleIfPresent(material, "_BaseMap", textureScale);
            SetTextureScaleIfPresent(material, "_MainTex", textureScale);
        }

        if (normal != null)
        {
            ApplyTexture(material, "_BumpMap", "_BumpMap", normal);
            SetTextureScaleIfPresent(material, "_BumpMap", textureScale);
            SetFloatIfPresent(material, "_BumpScale", bumpScale);
            material.EnableKeyword("_NORMALMAP");
        }
        else
        {
            material.DisableKeyword("_NORMALMAP");
        }

        if (emissive != null)
        {
            ApplyTexture(material, "_EmissionMap", "_EmissionMap", emissive);
            SetTextureScaleIfPresent(material, "_EmissionMap", textureScale);
            material.EnableKeyword("_EMISSION");
        }

        EditorUtility.SetDirty(material);
        return material;
    }

    static void SetTextureScaleIfPresent(Material material, string propertyName, Vector2 scale)
    {
        if (material.HasProperty(propertyName))
        {
            material.SetTextureScale(propertyName, scale);
        }
    }

    static GameObject AddBox(Transform parent, string name, Vector3 localPosition, Vector3 localScale, Material material)
    {
        var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = name;
        box.transform.SetParent(parent, false);
        box.transform.localPosition = localPosition;
        box.transform.localRotation = Quaternion.identity;
        box.transform.localScale = localScale;
        var collider = box.GetComponent<Collider>();
        if (collider != null)
        {
            UnityEngine.Object.DestroyImmediate(collider);
        }
        var renderer = box.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = material;
        }
        MarkStatic(box);
        return box;
    }

    static GameObject AddCylinder(Transform parent, string name, Vector3 localPosition, Vector3 localScale, Material material)
    {
        var cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        cylinder.name = name;
        cylinder.transform.SetParent(parent, false);
        cylinder.transform.localPosition = localPosition;
        cylinder.transform.localRotation = Quaternion.identity;
        cylinder.transform.localScale = localScale;
        var collider = cylinder.GetComponent<Collider>();
        if (collider != null)
        {
            UnityEngine.Object.DestroyImmediate(collider);
        }
        var renderer = cylinder.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = material;
        }
        MarkStatic(cylinder);
        return cylinder;
    }

    static GameObject AddDisc(Transform parent, string name, string root, float radius, int segments, Vector3 localPosition, Material material)
    {
        var mesh = CreateDiscMeshAsset(root, name + "_Mesh", radius, Mathf.Max(segments, 24));
        return AddMeshObject(parent, name, mesh, localPosition, Quaternion.identity, Vector3.one, material);
    }

    static GameObject AddRing(Transform parent, string name, string root, float innerRadius, float outerRadius, int segments, Vector3 localPosition, Material material)
    {
        var mesh = CreateRingMeshAsset(root, name + "_Mesh", innerRadius, outerRadius, Mathf.Max(segments, 24));
        return AddMeshObject(parent, name, mesh, localPosition, Quaternion.identity, Vector3.one, material);
    }

    static GameObject AddMeshObject(Transform parent, string name, Mesh mesh, Vector3 localPosition, Quaternion localRotation, Vector3 localScale, Material material)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        go.transform.localRotation = localRotation;
        go.transform.localScale = localScale;
        var filter = go.AddComponent<MeshFilter>();
        filter.sharedMesh = mesh;
        var renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        MarkStatic(go);
        return go;
    }

    static void AddRadialSeam(Transform parent, string name, float angle, float innerRadius, float outerRadius, float width, Material material)
    {
        var radians = angle * Mathf.Deg2Rad;
        var direction = new Vector3(Mathf.Sin(radians), 0f, Mathf.Cos(radians));
        var centerRadius = (innerRadius + outerRadius) * 0.5f;
        var length = outerRadius - innerRadius;
        var seam = AddBox(parent, name, direction * centerRadius + Vector3.up * 0.12f, new Vector3(width, 0.035f, length), material);
        seam.transform.localRotation = Quaternion.Euler(0f, angle, 0f);
    }

    static Mesh CreateDiscMeshAsset(string root, string name, float radius, int segments)
    {
        EnsureFolder(root, "GeneratedMeshes");
        var path = root + "/GeneratedMeshes/" + Sanitize(name) + ".asset";
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (mesh == null)
        {
            mesh = new Mesh { name = name };
            AssetDatabase.CreateAsset(mesh, path);
        }
        else
        {
            mesh.Clear();
            mesh.name = name;
        }

        var vertices = new Vector3[segments + 1];
        var normals = new Vector3[segments + 1];
        var uvs = new Vector2[segments + 1];
        var triangles = new int[segments * 3];

        vertices[0] = Vector3.zero;
        normals[0] = Vector3.up;
        uvs[0] = new Vector2(0.5f, 0.5f);
        for (var i = 0; i < segments; i++)
        {
            var angle = i * Mathf.PI * 2f / segments;
            var x = Mathf.Sin(angle) * radius;
            var z = Mathf.Cos(angle) * radius;
            vertices[i + 1] = new Vector3(x, 0f, z);
            normals[i + 1] = Vector3.up;
            uvs[i + 1] = new Vector2((x / radius + 1f) * 0.5f, (z / radius + 1f) * 0.5f);

            triangles[i * 3] = 0;
            triangles[i * 3 + 1] = i + 1;
            triangles[i * 3 + 2] = (i + 1) % segments + 1;
        }

        mesh.vertices = vertices;
        mesh.normals = normals;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
        EditorUtility.SetDirty(mesh);
        return mesh;
    }

    static Mesh CreateRingMeshAsset(string root, string name, float innerRadius, float outerRadius, int segments)
    {
        EnsureFolder(root, "GeneratedMeshes");
        var path = root + "/GeneratedMeshes/" + Sanitize(name) + ".asset";
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (mesh == null)
        {
            mesh = new Mesh { name = name };
            AssetDatabase.CreateAsset(mesh, path);
        }
        else
        {
            mesh.Clear();
            mesh.name = name;
        }

        var vertices = new Vector3[segments * 2];
        var normals = new Vector3[segments * 2];
        var uvs = new Vector2[segments * 2];
        var triangles = new int[segments * 6];

        for (var i = 0; i < segments; i++)
        {
            var angle = i * Mathf.PI * 2f / segments;
            var sin = Mathf.Sin(angle);
            var cos = Mathf.Cos(angle);
            var innerIndex = i * 2;
            var outerIndex = innerIndex + 1;
            vertices[innerIndex] = new Vector3(sin * innerRadius, 0f, cos * innerRadius);
            vertices[outerIndex] = new Vector3(sin * outerRadius, 0f, cos * outerRadius);
            normals[innerIndex] = Vector3.up;
            normals[outerIndex] = Vector3.up;
            uvs[innerIndex] = new Vector2((sin * innerRadius / outerRadius + 1f) * 0.5f, (cos * innerRadius / outerRadius + 1f) * 0.5f);
            uvs[outerIndex] = new Vector2((sin + 1f) * 0.5f, (cos + 1f) * 0.5f);

            var nextInner = ((i + 1) % segments) * 2;
            var nextOuter = nextInner + 1;
            var tri = i * 6;
            triangles[tri] = innerIndex;
            triangles[tri + 1] = outerIndex;
            triangles[tri + 2] = nextOuter;
            triangles[tri + 3] = innerIndex;
            triangles[tri + 4] = nextOuter;
            triangles[tri + 5] = nextInner;
        }

        mesh.vertices = vertices;
        mesh.normals = normals;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
        EditorUtility.SetDirty(mesh);
        return mesh;
    }

    static Mesh CreateGate3ArenaDiscMeshAsset(string root, string name, float radius, int rings, int segments)
    {
        EnsureFolder(root, "GeneratedMeshes");
        var path = root + "/GeneratedMeshes/" + Sanitize(name) + ".asset";
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (mesh == null)
        {
            mesh = new Mesh { name = name };
            AssetDatabase.CreateAsset(mesh, path);
        }
        else
        {
            mesh.Clear();
            mesh.name = name;
        }

        rings = Mathf.Max(rings, 8);
        segments = Mathf.Max(segments, 32);
        var vertices = new Vector3[1 + rings * segments];
        var uvs = new Vector2[vertices.Length];
        var triangles = new int[segments * 3 + (rings - 1) * segments * 6];

        vertices[0] = new Vector3(0f, Gate3ArenaMeshHeight(0f, 0f), 0f);
        uvs[0] = new Vector2(0.5f, 0.5f);

        for (var ring = 1; ring <= rings; ring++)
        {
            var rr = (float)ring / rings;
            for (var segment = 0; segment < segments; segment++)
            {
                var angle = segment * Mathf.PI * 2f / segments;
                var sin = Mathf.Sin(angle);
                var cos = Mathf.Cos(angle);
                var x = sin * radius * rr;
                var z = cos * radius * rr;
                var index = 1 + (ring - 1) * segments + segment;
                vertices[index] = new Vector3(x, Gate3ArenaMeshHeight(sin * rr, cos * rr), z);
                uvs[index] = new Vector2((sin * rr + 1f) * 0.5f, (cos * rr + 1f) * 0.5f);
            }
        }

        var tri = 0;
        for (var segment = 0; segment < segments; segment++)
        {
            var next = (segment + 1) % segments;
            triangles[tri++] = 0;
            triangles[tri++] = 1 + segment;
            triangles[tri++] = 1 + next;
        }

        for (var ring = 2; ring <= rings; ring++)
        {
            var innerStart = 1 + (ring - 2) * segments;
            var outerStart = 1 + (ring - 1) * segments;
            for (var segment = 0; segment < segments; segment++)
            {
                var next = (segment + 1) % segments;
                var inner = innerStart + segment;
                var innerNext = innerStart + next;
                var outer = outerStart + segment;
                var outerNext = outerStart + next;
                triangles[tri++] = inner;
                triangles[tri++] = outer;
                triangles[tri++] = outerNext;
                triangles[tri++] = inner;
                triangles[tri++] = outerNext;
                triangles[tri++] = innerNext;
            }
        }

        mesh.indexFormat = vertices.Length > 65000
            ? UnityEngine.Rendering.IndexFormat.UInt32
            : UnityEngine.Rendering.IndexFormat.UInt16;
        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        EditorUtility.SetDirty(mesh);
        return mesh;
    }

    static Mesh CreateGate3IrregularRingMeshAsset(string root, string name, float innerRadius, float outerRadius, int segments)
    {
        EnsureFolder(root, "GeneratedMeshes");
        var path = root + "/GeneratedMeshes/" + Sanitize(name) + ".asset";
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (mesh == null)
        {
            mesh = new Mesh { name = name };
            AssetDatabase.CreateAsset(mesh, path);
        }
        else
        {
            mesh.Clear();
            mesh.name = name;
        }

        segments = Mathf.Max(segments, 48);
        var vertices = new Vector3[segments * 2];
        var uvs = new Vector2[segments * 2];
        var triangles = new int[segments * 6];

        for (var i = 0; i < segments; i++)
        {
            var angle = i * Mathf.PI * 2f / segments;
            var sin = Mathf.Sin(angle);
            var cos = Mathf.Cos(angle);
            var n1 = Mathf.PerlinNoise(sin * 2.5f + 18.1f, cos * 2.5f + 44.9f) - 0.5f;
            var n2 = Mathf.PerlinNoise(sin * 9.5f + 7.2f, cos * 9.5f + 81.3f) - 0.5f;
            var inner = innerRadius + n1 * 0.42f + n2 * 0.18f;
            var outer = outerRadius + n1 * 0.78f + n2 * 0.34f;
            var h = 0.02f * Mathf.Sin(i * 0.77f) + n2 * 0.055f;
            var innerIndex = i * 2;
            var outerIndex = innerIndex + 1;
            vertices[innerIndex] = new Vector3(sin * inner, h, cos * inner);
            vertices[outerIndex] = new Vector3(sin * outer, h + 0.02f, cos * outer);
            uvs[innerIndex] = new Vector2((sin * inner / outerRadius + 1f) * 0.5f, (cos * inner / outerRadius + 1f) * 0.5f);
            uvs[outerIndex] = new Vector2((sin * outer / outerRadius + 1f) * 0.5f, (cos * outer / outerRadius + 1f) * 0.5f);

            var nextInner = ((i + 1) % segments) * 2;
            var nextOuter = nextInner + 1;
            var tri = i * 6;
            triangles[tri] = innerIndex;
            triangles[tri + 1] = outerIndex;
            triangles[tri + 2] = nextOuter;
            triangles[tri + 3] = innerIndex;
            triangles[tri + 4] = nextOuter;
            triangles[tri + 5] = nextInner;
        }

        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        EditorUtility.SetDirty(mesh);
        return mesh;
    }

    static float Gate3ArenaMeshHeight(float x, float z)
    {
        return (Gate3ArenaHeight(x, z) - 0.34f) * 0.105f;
    }

    static GameObject AddEmissiveOrb(Transform parent, string name, Vector3 localPosition, Vector3 localScale, Material material)
    {
        var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.name = name;
        sphere.transform.SetParent(parent, false);
        sphere.transform.localPosition = localPosition;
        sphere.transform.localRotation = Quaternion.identity;
        sphere.transform.localScale = localScale;
        var collider = sphere.GetComponent<Collider>();
        if (collider != null)
        {
            UnityEngine.Object.DestroyImmediate(collider);
        }
        var renderer = sphere.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = material;
        }
        MarkStatic(sphere);
        return sphere;
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
            .Where(path => IsGate3CuratedPackage(path))
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
        return IsGate3CuratedPackage(lower)
            && IsGate3StaticModelName(Path.GetFileNameWithoutExtension(lower))
            && !lower.Contains("house")
            && !lower.Contains("store")
            && !lower.Contains("bed")
            && !lower.Contains("book")
            && !lower.Contains("flower")
            && !lower.Contains("tree")
            && !lower.Contains("road")
            && !lower.Contains("fountain")
            && !lower.Contains("lowpoly");
    }

    static bool IsArenaModel(string assetPath)
    {
        var lower = Path.GetFileNameWithoutExtension(assetPath).ToLowerInvariant();
        var package = assetPath.ToLowerInvariant();
        return IsGate3CuratedPackage(package)
            && IsGate3StaticModelName(lower)
            && !lower.Contains("lowpoly");
    }

    static bool IsGate3CuratedPackage(string assetPath)
    {
        var lower = "/" + assetPath.Replace("\\", "/").Trim('/').ToLowerInvariant() + "/";
        return lower.Contains("/bg_rad_kamen_a/")
            || lower.Contains("/bg_rad_kamen_b/")
            || lower.Contains("/bg_rad_kamen_c/")
            || lower.Contains("/bg_rad_kamen_d/")
            || lower.Contains("/bg_rad_kamen_stone_a/")
            || lower.Contains("/efmaster_material_skymatte/")
            || lower.Contains("/sk_fif_hdr_00/");
    }

    static bool IsGate3StaticModelName(string lower)
    {
        return lower.Contains("kamen")
            || lower.Contains("floor")
            || lower.Contains("pillar")
            || lower.Contains("wall")
            || lower.Contains("gate")
            || lower.Contains("column")
            || lower.Contains("structure")
            || lower.Contains("stair")
            || lower.Contains("arch")
            || lower.Contains("deco")
            || lower.Contains("rock")
            || lower.Contains("stone")
            || lower.Contains("hdr")
            || lower.Contains("sky");
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
        light.intensity = 1.08f;
        light.color = new Color(0.62f, 0.76f, 1f, 1f);
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
        RenderSettings.ambientSkyColor = new Color(0.064f, 0.084f, 0.132f, 1f);
        RenderSettings.ambientEquatorColor = new Color(0.058f, 0.078f, 0.118f, 1f);
        RenderSettings.ambientGroundColor = new Color(0.026f, 0.030f, 0.043f, 1f);
        RenderSettings.reflectionIntensity = 0.42f;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = new Color(0.028f, 0.035f, 0.055f, 1f);
        RenderSettings.fogDensity = 0.0022f;
    }

    static void CreateKamenLightingRig(GameObject content, Transform parent = null)
    {
        var bounds = CalculateBounds(content);
        var center = bounds.HasValue ? bounds.Value.center : Vector3.zero;
        var radius = bounds.HasValue ? Mathf.Max(bounds.Value.extents.x, bounds.Value.extents.z) : 80f;
        center.y = 0f;
        radius = Mathf.Clamp(radius, 45f, 180f);

        AddPointLight("Kamen Cold Blue Rim", center + new Vector3(radius * 0.25f, 22f, -radius * 0.45f),
            new Color(0.25f, 0.52f, 1f, 1f), radius * 2.1f, 2.65f, parent);
        AddPointLight("Kamen Cool Floor Fill", center + new Vector3(-radius * 0.28f, 14f, radius * 0.18f),
            new Color(0.30f, 0.50f, 0.95f, 1f), radius * 1.65f, 1.55f, parent);
        AddPointLight("Kamen Low Blue Fill", center + new Vector3(0f, 5f, 0f),
            new Color(0.18f, 0.34f, 0.78f, 1f), radius * 1.15f, 0.90f, parent);

        var probeObject = new GameObject("Kamen Reflection Probe");
        if (parent != null)
        {
            probeObject.transform.SetParent(parent, true);
        }
        var probe = probeObject.AddComponent<ReflectionProbe>();
        probe.mode = ReflectionProbeMode.Realtime;
        probe.refreshMode = ReflectionProbeRefreshMode.OnAwake;
        probe.intensity = 0.52f;
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
        light.shadows = LightShadows.None;
        light.shadowStrength = 0f;
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
        color.postExposure.Override(0.20f);
        color.contrast.Override(8f);
        color.saturation.Override(-2f);
        color.colorFilter.Override(new Color(0.82f, 0.92f, 1f, 1f));

        var bloom = EnsureVolumeOverride<Bloom>(profile);
        bloom.threshold.Override(0.82f);
        bloom.intensity.Override(0.28f);
        bloom.scatter.Override(0.55f);
        bloom.tint.Override(new Color(0.46f, 0.70f, 1f, 1f));

        var vignette = EnsureVolumeOverride<Vignette>(profile);
        vignette.color.Override(new Color(0.02f, 0.018f, 0.023f, 1f));
        vignette.intensity.Override(0.20f);
        vignette.smoothness.Override(0.42f);

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
