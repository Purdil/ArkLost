using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class LogueAssetFixer
{
    private const string RoguePath = "Assets/Logue/rogue_all.fbx";
    private const string KnifePath = "Assets/Logue/textures_and_weapons/Textures_And_Weapons/FBX/knife.fbx";
    private const string ShieldPath = "Assets/Logue/textures_and_weapons/Textures_And_Weapons/FBX/shield.fbx";

    private const string MaterialRoot = "Assets/Logue/materials";
    private const string TextureRoot = "Assets/Logue/textures_and_weapons/Textures_And_Weapons/Rogue_Textures";
    private const int FaceTextureSize = 4096;
    public const int DefaultFaceMaskX = 1458;
    public const int DefaultFaceMaskY = 1200;
    public const int DefaultFaceMaskWidth = 1180;

    [MenuItem("Tools/Logue/Reapply Rogue Materials")]
    public static void Apply()
    {
        ConfigureHeadMaterial();

        RemapMaterial(RoguePath, "Head", $"{MaterialRoot}/Head.mat");
        RemapMaterial(RoguePath, "Cloth", $"{MaterialRoot}/Cloth.mat");
        RemapMaterial(RoguePath, "LeatherA", $"{MaterialRoot}/LeatherA.mat");
        RemapMaterial(RoguePath, "LeatherB", $"{MaterialRoot}/LeatherB.mat");
        RemapMaterial(RoguePath, "Metal", $"{MaterialRoot}/Metal.mat");
        RemapMaterial(KnifePath, "knife", $"{MaterialRoot}/knife.mat");
        RemapMaterial(ShieldPath, "shield", $"{MaterialRoot}/shield.mat");

        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(RoguePath, ImportAssetOptions.ForceUpdate);
        AssetDatabase.ImportAsset(KnifePath, ImportAssetOptions.ForceUpdate);
        AssetDatabase.ImportAsset(ShieldPath, ImportAssetOptions.ForceUpdate);

        ConfigureHeadMaterial();
        LogRendererMaterialBindings();
        AssetDatabase.SaveAssets();
    }

    public static void BakeFaceFixedTextures(int x, int y, int targetWidth)
    {
        var emissionCrop = LoadTrimmedFaceCrop($"{TextureRoot}/rogueCH_Head_Emiss.1001.png");
        var baseCrop = LoadTrimmedFaceCrop($"{TextureRoot}/rogueCH_Head_BaseColor.1001.png");
        var targetHeight = Mathf.Max(1, Mathf.RoundToInt(targetWidth * (emissionCrop.height / (float)emissionCrop.width)));

        BakeFaceTexture(
            $"{TextureRoot}/rogueCH_Head_BaseColor_FaceFixed.1001.png",
            baseCrop,
            x,
            y,
            targetWidth,
            targetHeight);
        BakeFaceTexture(
            $"{TextureRoot}/rogueCH_Head_Emiss_FaceFixed.1001.png",
            emissionCrop,
            x,
            y,
            targetWidth,
            targetHeight);

        AssetDatabase.ImportAsset($"{TextureRoot}/rogueCH_Head_BaseColor_FaceFixed.1001.png", ImportAssetOptions.ForceUpdate);
        AssetDatabase.ImportAsset($"{TextureRoot}/rogueCH_Head_Emiss_FaceFixed.1001.png", ImportAssetOptions.ForceUpdate);
        ConfigureHeadMaterial();
        AssetDatabase.SaveAssets();
    }

    [MenuItem("Tools/Logue/Diagnose Head Emission UV")]
    public static void DiagnoseHeadEmissionUv()
    {
        var prefab = LoadRequired<GameObject>(RoguePath);
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        if (instance == null)
        {
            instance = UnityEngine.Object.Instantiate(prefab);
        }

        try
        {
            var renderer = instance.GetComponentsInChildren<Renderer>(true).FirstOrDefault(item => item.name == "head_geo");
            if (renderer == null)
            {
                throw new InvalidOperationException("Renderer not found: head_geo");
            }

            var sourceMesh = GetSourceMesh(renderer);
            var mesh = GetBakedMesh(renderer, sourceMesh);
            var vertices = mesh.vertices;
            var triangles = sourceMesh.triangles;
            var emission = LoadPngPixels($"{TextureRoot}/rogueCH_Head_Emiss.1001.png", out var width, out var height);
            var meshMin = ComponentMin(vertices);
            var meshMax = ComponentMax(vertices);

            Debug.Log($"[LogueAssetFixer] head_geo mesh vertices={vertices.Length}, triangles={triangles.Length / 3}, boundsY={meshMin.y:0.###}..{meshMax.y:0.###}");

            for (var channel = 0; channel < 4; channel++)
            {
                var uvs = new List<Vector2>();
                sourceMesh.GetUVs(channel, uvs);
                if (uvs.Count != vertices.Length)
                {
                    Debug.Log($"[LogueAssetFixer] UV{channel}: skipped, count={uvs.Count}");
                    continue;
                }

                foreach (var variant in new[] { "normal", "flipV", "flipU", "flipUV", "swap", "swapFlipV" })
                {
                    var result = SampleEmissionOnMesh(vertices, triangles, uvs, emission, width, height, variant, meshMin, meshMax);
                    Debug.Log($"[LogueAssetFixer] UV{channel} {variant}: {result}");
                }
            }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(instance);
        }
    }

    private static void ConfigureHeadMaterial()
    {
        var material = LoadRequired<Material>($"{MaterialRoot}/Head.mat");
        var baseMap = LoadRequired<Texture2D>($"{TextureRoot}/rogueCH_Head_BaseColor_FaceFixed.1001.png");
        var normalMap = LoadRequired<Texture2D>($"{TextureRoot}/rogueCH_Head_Normal_FaceFixed.1001.png");
        var emissionMap = LoadRequired<Texture2D>($"{TextureRoot}/rogueCH_Head_Emiss_FaceFixed.1001.png");
        var metallicMap = LoadRequired<Texture2D>($"{TextureRoot}/rogueCH_Head_MetallicSmoothness_FaceFixed.1001.png");
        var occlusionMap = LoadRequired<Texture2D>($"{TextureRoot}/rogueCH_Head_Occlusion_FaceFixed.1001.png");

        var litShader = Shader.Find("Universal Render Pipeline/Lit");
        if (litShader != null)
        {
            material.shader = litShader;
        }

        material.SetTexture("_BaseMap", baseMap);
        material.SetTexture("_MainTex", baseMap);
        material.SetTexture("_BumpMap", normalMap);
        material.SetTexture("_EmissionMap", emissionMap);
        material.SetTexture("_MetallicGlossMap", metallicMap);
        material.SetTexture("_OcclusionMap", occlusionMap);
        ApplyHeadTextureTransform(material, "_BaseMap");
        ApplyHeadTextureTransform(material, "_MainTex");
        ApplyHeadTextureTransform(material, "_BumpMap");
        ApplyHeadTextureTransform(material, "_EmissionMap");
        ApplyHeadTextureTransform(material, "_MetallicGlossMap");
        ApplyHeadTextureTransform(material, "_OcclusionMap");

        material.SetColor("_BaseColor", Color.white);
        material.SetColor("_Color", Color.white);
        material.SetColor("_EmissionColor", new Color(0.45f, 3f, 0.8f, 1f));
        material.SetFloat("_Cull", (float)CullMode.Off);
        material.SetFloat("_Surface", 0f);
        material.SetFloat("_AlphaClip", 0f);
        material.renderQueue = -1;
        material.SetOverrideTag("RenderType", "Opaque");
        material.doubleSidedGI = true;
        material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;

        material.EnableKeyword("_EMISSION");
        material.EnableKeyword("_NORMALMAP");
        material.EnableKeyword("_METALLICSPECGLOSSMAP");
        material.EnableKeyword("_OCCLUSIONMAP");
        material.DisableKeyword("_ALPHATEST_ON");
        material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.DisableKeyword("_METALLICGLOSSMAP");

        EditorUtility.SetDirty(material);
    }

    private static Texture2D LoadTrimmedFaceCrop(string assetPath)
    {
        var texture = LoadPngTexture(assetPath);
        var source = texture.GetPixels32();
        var roi = new RectInt(2650, FaceTextureSize - 1550, 900, 870);
        var minX = roi.xMax;
        var minY = roi.yMax;
        var maxX = roi.xMin;
        var maxY = roi.yMin;

        for (var y = roi.yMin; y < roi.yMax; y++)
        {
            for (var x = roi.xMin; x < roi.xMax; x++)
            {
                var color = source[y * texture.width + x];
                if (color.r + color.g + color.b <= 20)
                {
                    continue;
                }

                minX = Mathf.Min(minX, x);
                minY = Mathf.Min(minY, y);
                maxX = Mathf.Max(maxX, x);
                maxY = Mathf.Max(maxY, y);
            }
        }

        if (minX > maxX || minY > maxY)
        {
            return CropTexture(texture, roi);
        }

        return CropTexture(texture, new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1));
    }

    private static Texture2D LoadPngTexture(string assetPath)
    {
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
        if (!ImageConversion.LoadImage(texture, File.ReadAllBytes(GetAbsolutePath(assetPath))))
        {
            throw new InvalidOperationException($"Could not load image: {assetPath}");
        }

        return texture;
    }

    private static Texture2D CropTexture(Texture2D source, RectInt crop)
    {
        var result = new Texture2D(crop.width, crop.height, TextureFormat.RGBA32, false, true);
        var pixels = source.GetPixels(crop.x, crop.y, crop.width, crop.height);
        result.SetPixels(pixels);
        result.Apply();
        return result;
    }

    private static void BakeFaceTexture(string assetPath, Texture2D face, int x, int y, int targetWidth, int targetHeight)
    {
        var canvas = new Texture2D(FaceTextureSize, FaceTextureSize, TextureFormat.RGBA32, false, true);
        var black = Enumerable.Repeat(new Color32(0, 0, 0, 255), FaceTextureSize * FaceTextureSize).ToArray();
        canvas.SetPixels32(black);

        for (var targetY = 0; targetY < targetHeight; targetY++)
        {
            var canvasY = FaceTextureSize - y - targetHeight + targetY;
            if (canvasY < 0 || canvasY >= FaceTextureSize)
            {
                continue;
            }

            var v = targetHeight <= 1 ? 0f : targetY / (float)(targetHeight - 1);
            for (var targetX = 0; targetX < targetWidth; targetX++)
            {
                var canvasX = x + targetX;
                if (canvasX < 0 || canvasX >= FaceTextureSize)
                {
                    continue;
                }

                var u = targetWidth <= 1 ? 0f : targetX / (float)(targetWidth - 1);
                var sourceColor = face.GetPixelBilinear(u, v);
                if (sourceColor.r + sourceColor.g + sourceColor.b <= 0.02f)
                {
                    continue;
                }

                canvas.SetPixel(canvasX, canvasY, sourceColor);
            }
        }

        canvas.Apply();
        File.WriteAllBytes(GetAbsolutePath(assetPath), canvas.EncodeToPNG());
    }

    private static string GetAbsolutePath(string assetPath)
    {
        return Path.Combine(Directory.GetParent(Application.dataPath).FullName, assetPath);
    }

    private static void ApplyHeadTextureTransform(Material material, string propertyName)
    {
        material.SetTextureScale(propertyName, Vector2.one);
        material.SetTextureOffset(propertyName, Vector2.zero);
    }

    private static Mesh GetSourceMesh(Renderer renderer)
    {
        if (renderer is SkinnedMeshRenderer skinnedMeshRenderer)
        {
            return skinnedMeshRenderer.sharedMesh;
        }

        var filter = renderer.GetComponent<MeshFilter>();
        if (filter != null)
        {
            return filter.sharedMesh;
        }

        throw new InvalidOperationException($"Mesh not found on renderer: {renderer.name}");
    }

    private static Mesh GetBakedMesh(Renderer renderer, Mesh sourceMesh)
    {
        if (renderer is SkinnedMeshRenderer skinnedMeshRenderer)
        {
            var mesh = new Mesh();
            skinnedMeshRenderer.BakeMesh(mesh);
            return mesh;
        }

        return sourceMesh;
    }

    private static Color32[] LoadPngPixels(string assetPath, out int width, out int height)
    {
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
        if (!ImageConversion.LoadImage(texture, File.ReadAllBytes(GetAbsolutePath(assetPath))))
        {
            throw new InvalidOperationException($"Could not load image: {assetPath}");
        }

        width = texture.width;
        height = texture.height;
        return texture.GetPixels32();
    }

    private static string SampleEmissionOnMesh(
        Vector3[] vertices,
        int[] triangles,
        IReadOnlyList<Vector2> uvs,
        Color32[] pixels,
        int width,
        int height,
        string variant,
        Vector3 meshMin,
        Vector3 meshMax)
    {
        var count = 0;
        var sumPosition = Vector3.zero;
        var minPosition = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        var maxPosition = new Vector3(float.MinValue, float.MinValue, float.MinValue);

        for (var index = 0; index < triangles.Length; index += 3)
        {
            var i0 = triangles[index];
            var i1 = triangles[index + 1];
            var i2 = triangles[index + 2];

            AccumulateSample(vertices, uvs, pixels, width, height, variant, i0, i1, i2, 1f / 3f, 1f / 3f, 1f / 3f, ref count, ref sumPosition, ref minPosition, ref maxPosition);
            AccumulateSample(vertices, uvs, pixels, width, height, variant, i0, i1, i2, 0.7f, 0.15f, 0.15f, ref count, ref sumPosition, ref minPosition, ref maxPosition);
            AccumulateSample(vertices, uvs, pixels, width, height, variant, i0, i1, i2, 0.15f, 0.7f, 0.15f, ref count, ref sumPosition, ref minPosition, ref maxPosition);
            AccumulateSample(vertices, uvs, pixels, width, height, variant, i0, i1, i2, 0.15f, 0.15f, 0.7f, ref count, ref sumPosition, ref minPosition, ref maxPosition);
        }

        if (count == 0)
        {
            return "brightSamples=0";
        }

        var average = sumPosition / count;
        var ySpan = Mathf.Max(0.0001f, meshMax.y - meshMin.y);
        var avgY = (average.y - meshMin.y) / ySpan;
        var minY = (minPosition.y - meshMin.y) / ySpan;
        var maxY = (maxPosition.y - meshMin.y) / ySpan;
        return $"brightSamples={count}, avg={FormatVector(average)}, yNorm={avgY:0.###}, yNormRange={minY:0.###}..{maxY:0.###}, bbox={FormatVector(minPosition)}..{FormatVector(maxPosition)}";
    }

    private static void AccumulateSample(
        Vector3[] vertices,
        IReadOnlyList<Vector2> uvs,
        IReadOnlyList<Color32> pixels,
        int width,
        int height,
        string variant,
        int i0,
        int i1,
        int i2,
        float w0,
        float w1,
        float w2,
        ref int count,
        ref Vector3 sumPosition,
        ref Vector3 minPosition,
        ref Vector3 maxPosition)
    {
        var uv = TransformUv(uvs[i0] * w0 + uvs[i1] * w1 + uvs[i2] * w2, variant);
        var color = SampleRepeat(pixels, width, height, uv);
        var luma = (color.r + color.g + color.b) / 765f;
        if (luma < 0.16f)
        {
            return;
        }

        var position = vertices[i0] * w0 + vertices[i1] * w1 + vertices[i2] * w2;
        count++;
        sumPosition += position;
        minPosition = Vector3.Min(minPosition, position);
        maxPosition = Vector3.Max(maxPosition, position);
    }

    private static Vector2 TransformUv(Vector2 uv, string variant)
    {
        switch (variant)
        {
            case "flipV":
                return new Vector2(uv.x, 1f - uv.y);
            case "flipU":
                return new Vector2(1f - uv.x, uv.y);
            case "flipUV":
                return new Vector2(1f - uv.x, 1f - uv.y);
            case "swap":
                return new Vector2(uv.y, uv.x);
            case "swapFlipV":
                return new Vector2(uv.y, 1f - uv.x);
            default:
                return uv;
        }
    }

    private static Color32 SampleRepeat(IReadOnlyList<Color32> pixels, int width, int height, Vector2 uv)
    {
        var u = uv.x - Mathf.Floor(uv.x);
        var v = uv.y - Mathf.Floor(uv.y);
        var x = Mathf.Clamp(Mathf.RoundToInt(u * (width - 1)), 0, width - 1);
        var y = Mathf.Clamp(Mathf.RoundToInt(v * (height - 1)), 0, height - 1);
        return pixels[y * width + x];
    }

    private static Vector3 ComponentMin(IReadOnlyList<Vector3> values)
    {
        var result = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        foreach (var value in values)
        {
            result = Vector3.Min(result, value);
        }

        return result;
    }

    private static Vector3 ComponentMax(IReadOnlyList<Vector3> values)
    {
        var result = new Vector3(float.MinValue, float.MinValue, float.MinValue);
        foreach (var value in values)
        {
            result = Vector3.Max(result, value);
        }

        return result;
    }

    private static string FormatVector(Vector3 value)
    {
        return $"({value.x:0.###}, {value.y:0.###}, {value.z:0.###})";
    }

    private static void RemapMaterial(string modelPath, string sourceMaterialName, string materialPath)
    {
        var importer = AssetImporter.GetAtPath(modelPath);
        if (importer == null)
        {
            throw new InvalidOperationException($"Importer not found: {modelPath}");
        }

        var material = LoadRequired<Material>(materialPath);
        var identifier = new AssetImporter.SourceAssetIdentifier(typeof(Material), sourceMaterialName);
        importer.AddRemap(identifier, material);
        EditorUtility.SetDirty(importer);
        importer.SaveAndReimport();
    }

    private static T LoadRequired<T>(string path) where T : UnityEngine.Object
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null)
        {
            throw new InvalidOperationException($"Asset not found or wrong type: {path}");
        }

        return asset;
    }

    private static void LogRendererMaterialBindings()
    {
        var root = LoadRequired<GameObject>(RoguePath);
        var renderers = root.GetComponentsInChildren<Renderer>(true);
        var lines = renderers.Select(renderer =>
        {
            var names = string.Join(", ", renderer.sharedMaterials.Select(material => material ? material.name : "null"));
            return $"{renderer.name}: {names}";
        });

        Debug.Log("[LogueAssetFixer] Rogue renderer materials\n" + string.Join("\n", lines));
    }
}

public sealed class LogueFaceMaskAdjuster : EditorWindow
{
    private const string XKey = "LogueFaceMaskAdjuster.X";
    private const string YKey = "LogueFaceMaskAdjuster.Y";
    private const string WidthKey = "LogueFaceMaskAdjuster.Width";

    private int x;
    private int y;
    private int width;

    [MenuItem("Tools/Logue/Face Mask Adjuster")]
    public static void Open()
    {
        var window = GetWindow<LogueFaceMaskAdjuster>("Logue Face");
        window.minSize = new Vector2(320f, 180f);
        window.Show();
    }

    private void OnEnable()
    {
        x = EditorPrefs.GetInt(XKey, LogueAssetFixer.DefaultFaceMaskX);
        y = EditorPrefs.GetInt(YKey, LogueAssetFixer.DefaultFaceMaskY);
        width = EditorPrefs.GetInt(WidthKey, LogueAssetFixer.DefaultFaceMaskWidth);
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Face Mask Placement", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Larger Y moves the mask lower on the face. Click Bake after changing values, then reimport/refresh if the Scene view does not update immediately.", MessageType.Info);

        EditorGUI.BeginChangeCheck();
        x = EditorGUILayout.IntSlider("X", x, 900, 2000);
        y = EditorGUILayout.IntSlider("Y", y, 700, 1700);
        width = EditorGUILayout.IntSlider("Width", width, 700, 1600);

        if (EditorGUI.EndChangeCheck())
        {
            EditorPrefs.SetInt(XKey, x);
            EditorPrefs.SetInt(YKey, y);
            EditorPrefs.SetInt(WidthKey, width);
        }

        EditorGUILayout.Space(8f);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Bake"))
            {
                Bake();
            }

            if (GUILayout.Button("Reset"))
            {
                x = LogueAssetFixer.DefaultFaceMaskX;
                y = LogueAssetFixer.DefaultFaceMaskY;
                width = LogueAssetFixer.DefaultFaceMaskWidth;
                EditorPrefs.SetInt(XKey, x);
                EditorPrefs.SetInt(YKey, y);
                EditorPrefs.SetInt(WidthKey, width);
            }
        }
    }

    private void Bake()
    {
        LogueAssetFixer.BakeFaceFixedTextures(x, y, width);
        Debug.Log($"[LogueFaceMaskAdjuster] Baked face mask x={x}, y={y}, width={width}");
    }
}
