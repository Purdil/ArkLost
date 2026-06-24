using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class KamenGate3MSH012SceneApplier
{
    const string Root = "Assets/KamenMap";
    const string ScenePath = "Assets/_Scenes/GameScene.unity";
    const string PrefabPath = Root + "/Prefabs/KamenGate3_MSH012_Reconstructed.prefab";
    const string LegacyGraphicsPrefabPath = Root + "/Prefabs/KamenMap_WithGraphics.prefab";
    const float ArenaFloorScale = 4.55f;
    const float ArenaEdgeRadius = 30.15f;

    [MenuItem("Tools/Kamen Map/Rebuild Gate3 MSH012 Prefab")]
    public static void BuildPrefabOnly()
    {
        BuildPrefab();
    }

    [MenuItem("Tools/Kamen Map/Apply Gate3 MSH012 To GameScene - Keep Lighting PP")]
    public static void BuildAndApply()
    {
        var prefab = BuildPrefab();
        ApplyPrefabToGameScene(prefab);
    }

    public static GameObject BuildPrefab()
    {
        EnsureFolder(Root, "Prefabs");
        EnsureFolder(Root, "Materials");
        EnsureFolder(Root, "GeneratedMeshes");
        EnsureNestedFolder(Root + "/Materials", "Generated");

        var materialByName = LoadKamenMaterials();
        var floorMaterial = LoadMaterial("BG_RAD_KAMEN_A/BG_RAD_KAMEN_A__bg_rad_kamen_floor05b_mi_lsj.mat")
            ?? LoadMaterial("BG_RAD_KAMEN_A/BG_RAD_KAMEN_A__bg_rad_kamen_floor05a_mi_lsj.mat");
        var blueBeamMaterial = CreateGeneratedMaterial(
            "KamenGate3_BlueBeam_VisualOnly",
            new Color(0.25f, 0.55f, 1f, 0.72f),
            new Color(0.16f, 0.48f, 1f, 1f),
            3.2f,
            true);
        var blackAbyssMaterial = CreateGeneratedMaterial(
            "KamenGate3_BlackAbyss_Rim_VisualOnly",
            new Color(0.006f, 0.009f, 0.015f, 1f),
            new Color(0.0f, 0.01f, 0.035f, 1f),
            0.12f,
            false);

        if (floorMaterial != null)
        {
            BoostFloorMaterial(floorMaterial);
        }

        var root = new GameObject("KamenGate3_MSH012_Reconstructed_MapOnly");
        var content = CreateChild(root.transform, "Reference_Image_Matched_Content");
        BuildArenaFloor(content.transform, materialByName, floorMaterial, blackAbyssMaterial);
        BuildThornBarricade(content.transform, materialByName);
        BuildRearThroneWall(content.transform, materialByName, blueBeamMaterial);
        BuildOuterDebris(content.transform, materialByName);
        BuildBlueBeamVisuals(content.transform, blueBeamMaterial);

        MarkStatic(root);
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Built Kamen Gate3 MSH012 reconstructed prefab: " + PrefabPath);
        return prefab;
    }

    public static void ApplyPrefabToGameScene(GameObject prefab)
    {
        if (prefab == null)
        {
            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        }
        if (prefab == null)
        {
            throw new FileNotFoundException("Reconstructed Kamen prefab not found.", PrefabPath);
        }

        var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
        if (sceneAsset == null)
        {
            throw new FileNotFoundException("GameScene not found.", ScenePath);
        }

        var previousActiveScene = SceneManager.GetActiveScene();
        var alreadyLoaded = TryGetLoadedScene(ScenePath, out var scene);
        if (!alreadyLoaded)
        {
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        }
        if (!scene.IsValid() || !scene.isLoaded)
        {
            throw new InvalidOperationException("Could not load scene: " + ScenePath);
        }

        SceneManager.SetActiveScene(scene);
        var level = FindLevel(scene);
        if (level == null)
        {
            level = new GameObject("Level");
            SceneManager.MoveGameObjectToScene(level, scene);
        }

        var preservedRoot = FindOrCreateSceneRoot(scene, "Kamen Preserved Lighting PP");
        var removed = 0;
        var preserved = 0;
        for (var i = level.transform.childCount - 1; i >= 0; i--)
        {
            var child = level.transform.GetChild(i).gameObject;
            if (!LooksLikeKamenMapRoot(child))
            {
                continue;
            }

            if (PrefabUtility.IsPartOfPrefabInstance(child))
            {
                var prefabRoot = PrefabUtility.GetOutermostPrefabInstanceRoot(child);
                if (prefabRoot == child)
                {
                    PrefabUtility.UnpackPrefabInstance(child, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                }
            }

            preserved += PreserveLightingAndPostProcessChildren(child, preservedRoot.transform);
            Object.DestroyImmediate(child);
            removed++;
        }

        var restoredLegacyGraphicsRigs = EnsureLegacyGraphicsRig(scene, preservedRoot.transform);

        var instance = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
        if (instance == null)
        {
            throw new InvalidOperationException("Could not instantiate prefab: " + PrefabPath);
        }

        instance.name = "KamenGate3_MSH012_Reconstructed";
        instance.transform.SetParent(level.transform, false);
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;
        instance.transform.localScale = Vector3.one;

        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene))
        {
            throw new InvalidOperationException("Could not save scene: " + ScenePath);
        }

        if (!alreadyLoaded)
        {
            EditorSceneManager.CloseScene(scene, true);
        }
        else if (previousActiveScene.IsValid() && previousActiveScene.isLoaded)
        {
            SceneManager.SetActiveScene(previousActiveScene);
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"Applied {PrefabPath} to {ScenePath}. Removed Kamen map roots: {removed}, preserved lighting/PP objects: {preserved}, restored legacy graphics rigs: {restoredLegacyGraphicsRigs}.");
    }

    static void BuildArenaFloor(Transform parent, Dictionary<string, Material> materialByName, Material floorMaterial, Material blackAbyssMaterial)
    {
        var arena = CreateChild(parent, "Arena_MSH012_Floor05B_Only");
        AddModel("bg_rad_kamen_floor05b_sm", arena.transform, Vector3.zero, Vector3.zero, Vector3.one * ArenaFloorScale, materialByName, floorMaterial, true);

        var rim = CreateChild(arena.transform, "Thin_Dark_Arena_Outer_Rim");
        AddFlatCylinder(rim.transform, "KamenGate3_MSH012_DarkRim", "Visual_Dark_Rim_At_Playable_Edge", ArenaEdgeRadius - 0.45f, ArenaEdgeRadius + 0.55f, 0.04f, 160, blackAbyssMaterial);
    }

    static void BuildThornBarricade(Transform parent, Dictionary<string, Material> materialByName)
    {
        AddArenaBoundaryBlockers(parent);

        var thorns = CreateChild(parent, "Arena_Line_Interwoven_Thorn_Barricade_MSH060_MSH062");
        var rng = new System.Random(30602);
        var thornModels = new[] { "bg_rad_kamen_pillar02_sm_hhk", "bg_rad_kamen_pillar02lowpoly_sm_hhk" };
        const int clusters = 68;

        for (var i = 0; i < clusters; i++)
        {
            var baseAngle = i * 360f / clusters + RandomRange(rng, -1.65f, 1.65f);
            var clusterRadius = ArenaEdgeRadius + RandomRange(rng, -0.55f, 0.45f);
            var localCount = 3 + ((i * 11) % 3);

            for (var j = 0; j < localCount; j++)
            {
                var lane = j - (localCount - 1) * 0.5f;
                var angle = baseAngle + lane * RandomRange(rng, 0.38f, 0.82f) + RandomRange(rng, -0.8f, 0.8f);
                var radius = clusterRadius + RandomRange(rng, -0.38f, 0.46f);
                var y = RandomRange(rng, -0.24f, 0.58f);
                var model = thornModels[(i + j) % thornModels.Length];
                var yaw = angle + 180f + RandomRange(rng, -46f, 46f);
                var pitch = RandomRange(rng, 47f, 74f);
                var roll = RandomRange(rng, -31f, 31f);
                var scale = Vector3.one * RandomRange(rng, 1.58f, 2.34f);
                AddModel(model, thorns.transform, RadialPosition(angle, radius, y), new Vector3(pitch, yaw, roll), scale, materialByName, null, false);
            }

            var crossCount = 1 + (i % 4 == 0 ? 1 : 0);
            for (var j = 0; j < crossCount; j++)
            {
                var crossAngle = baseAngle + RandomRange(rng, -2.25f, 2.25f);
                var crossRadius = ArenaEdgeRadius + RandomRange(rng, -0.62f, 0.64f);
                var model = thornModels[(i + j + 1) % thornModels.Length];
                var tangentSign = ((i + j) % 2 == 0) ? 1f : -1f;
                var yaw = crossAngle + 90f * tangentSign + RandomRange(rng, -28f, 28f);
                var pitch = RandomRange(rng, 16f, 39f);
                var roll = RandomRange(rng, -18f, 18f);
                var scale = Vector3.one * RandomRange(rng, 1.34f, 1.96f);
                AddModel(model, thorns.transform, RadialPosition(crossAngle, crossRadius, 0.38f), new Vector3(pitch, yaw, roll), scale, materialByName, null, false);
            }
        }
    }

    static void BuildRearThroneWall(Transform parent, Dictionary<string, Material> materialByName, Material blueBeamMaterial)
    {
        var rear = CreateChild(parent, "North_Colosseum_Throne_Wall");
        AddModel("bg_rad_kamen_housedome01a_sm_hhk", rear.transform, new Vector3(0f, -0.2f, 79f), new Vector3(0f, 180f, 0f), Vector3.one * 1.95f, materialByName, null, false);
        AddModel("bg_rad_kamen_housedome01_sm_hhk", rear.transform, new Vector3(0f, 0.0f, 67f), new Vector3(0f, 180f, 0f), Vector3.one * 1.55f, materialByName, null, false);
        AddModel("bg_rad_kamen_gate01_sm_jjh", rear.transform, new Vector3(0f, 0.25f, 56f), new Vector3(0f, 180f, 0f), Vector3.one * 3.0f, materialByName, null, false);

        AddModel("bg_rad_kamen_chair01_sm_cjy", rear.transform, new Vector3(0f, 0f, 42.5f), new Vector3(0f, 180f, 0f), Vector3.one * 3.15f, materialByName, null, false);
        AddModel("bg_rad_kamen_chair01a_sm_jjh", rear.transform, new Vector3(0f, 0.15f, 43.2f), new Vector3(0f, 180f, 0f), Vector3.one * 2.65f, materialByName, null, false);
        AddModel("bg_rad_kamen_chair01b_sm_cjy", rear.transform, new Vector3(0f, 1.8f, 42.8f), new Vector3(0f, 180f, 0f), Vector3.one * 3.0f, materialByName, null, false);

        for (var side = -1; side <= 1; side += 2)
        {
            AddModel("bg_rad_kamen_window01_sm_psy", rear.transform, new Vector3(side * 28f, 8.5f, 61f), new Vector3(0f, 180f - side * 12f, 0f), Vector3.one * 1.65f, materialByName, null, false);
            AddModel("bg_rad_kamen_column02_sm_jjh", rear.transform, new Vector3(side * 22f, 0.2f, 51f), new Vector3(0f, 180f - side * 18f, 0f), Vector3.one * 2.6f, materialByName, null, false);
            AddModel("bg_rad_kamen_pillar08_sm_alchemy", rear.transform, new Vector3(side * 17f, 1f, 47f), new Vector3(0f, 180f - side * 20f, 0f), Vector3.one * 2.0f, materialByName, null, false);
            AddBeam(rear.transform, "Blue_Beam_Rear_" + side, new Vector3(side * 19f, 19f, 49f), 34f, 0.18f, blueBeamMaterial);
        }

        AddBeam(rear.transform, "Blue_Beam_Center_Throne", new Vector3(0f, 22f, 45f), 42f, 0.24f, blueBeamMaterial);
    }

    static void BuildOuterDebris(Transform parent, Dictionary<string, Material> materialByName)
    {
        var debris = CreateChild(parent, "Outer_Rock_And_Dark_Mass_Silhouette");
        var rockModels = new[]
        {
            "bg_rad_kamen_rockpillar01_sm",
            "bg_rad_kamen_rockpillar01b_sm",
            "bg_rad_kamen_rockpillar01c_sm",
            "bg_rad_kamen_rockdeco01_sm",
            "bg_rad_kamen_rock02_sm"
        };

        for (var i = 0; i < 18; i++)
        {
            var angle = 6f + i * 360f / 18f;
            var radius = 43f + (i % 4) * 2.4f;
            var y = -0.4f + (i % 3) * 0.3f;
            var model = rockModels[i % rockModels.Length];
            AddModel(model, debris.transform, RadialPosition(angle, radius, y), new Vector3(0f, angle + 180f, 0f), Vector3.one * (2.2f + (i % 5) * 0.35f), materialByName, null, false);
        }

        var walls = CreateChild(parent, "Curved_Back_Colosseum_Wall_Segments");
        for (var i = 0; i < 18; i++)
        {
            var angle = 210f + i * 120f / 17f;
            var radius = 52f;
            var model = (i % 2 == 0) ? "bg_rad_kamen_wall01_sm_hhk" : "bg_rad_kamen_wall01_sm_alchemy";
            AddModel(model, walls.transform, RadialPosition(angle, radius, 0f), new Vector3(0f, angle + 180f, 0f), Vector3.one * 2.6f, materialByName, null, false);
        }
    }

    static void BuildBlueBeamVisuals(Transform parent, Material blueBeamMaterial)
    {
        var beams = CreateChild(parent, "Reference_Blue_Vertical_Beam_Visuals_No_Lights");
        for (var i = 0; i < 6; i++)
        {
            var angle = 30f + i * 360f / 6f;
            AddBeam(beams.transform, "Outer_Blue_Beam_" + i, RadialPosition(angle, 41f, 18f), 34f, 0.13f, blueBeamMaterial);
            AddOrb(beams.transform, "Outer_Blue_Node_" + i, RadialPosition(angle, 41f, 1.8f), 1.2f, blueBeamMaterial);
        }
        AddOrb(beams.transform, "Faint_Center_Blue_Core", new Vector3(0f, 0.75f, 0f), 1.8f, blueBeamMaterial);
    }

    static GameObject AddModel(
        string modelName,
        Transform parent,
        Vector3 localPosition,
        Vector3 localEuler,
        Vector3 localScale,
        Dictionary<string, Material> materialByName,
        Material overrideMaterial,
        bool addCollider)
    {
        var path = FindModelPath(modelName);
        if (string.IsNullOrEmpty(path))
        {
            Debug.LogWarning("Missing model: " + modelName);
            return null;
        }

        var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (model == null)
        {
            Debug.LogWarning("Could not load model asset: " + path);
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
        AssignMaterials(instance, materialByName, overrideMaterial);
        if (addCollider)
        {
            AddMeshColliders(instance);
        }
        return instance;
    }

    static string FindModelPath(string modelName)
    {
        var guids = AssetDatabase.FindAssets(modelName + " t:Model", new[] { Root + "/Models" });
        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid).Replace("\\", "/");
            if (Path.GetFileNameWithoutExtension(path).Equals(modelName, StringComparison.OrdinalIgnoreCase))
            {
                return path;
            }
        }
        return string.Empty;
    }

    static void AssignMaterials(GameObject instance, Dictionary<string, Material> materialByName, Material overrideMaterial)
    {
        foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
        {
            var slots = renderer.sharedMaterials;
            for (var i = 0; i < slots.Length; i++)
            {
                if (overrideMaterial != null)
                {
                    slots[i] = overrideMaterial;
                    continue;
                }

                var slot = slots[i];
                if (slot == null)
                {
                    continue;
                }

                var clean = CleanMaterialName(slot.name);
                if (materialByName.TryGetValue(clean, out var material))
                {
                    slots[i] = material;
                }
            }
            renderer.sharedMaterials = slots;
        }
    }

    static void AddMeshColliders(GameObject instance)
    {
        foreach (var meshFilter in instance.GetComponentsInChildren<MeshFilter>(true))
        {
            if (meshFilter.sharedMesh == null || meshFilter.GetComponent<MeshCollider>() != null)
            {
                continue;
            }
            var collider = meshFilter.gameObject.AddComponent<MeshCollider>();
            collider.sharedMesh = meshFilter.sharedMesh;
        }
    }

    static Dictionary<string, Material> LoadKamenMaterials()
    {
        var result = new Dictionary<string, Material>(StringComparer.OrdinalIgnoreCase);
        var guids = AssetDatabase.FindAssets("t:Material", new[] { Root + "/Materials" });
        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                continue;
            }
            result[CleanMaterialName(material.name)] = material;
        }
        return result;
    }

    static Material LoadMaterial(string relativePath)
    {
        return AssetDatabase.LoadAssetAtPath<Material>(Root + "/Materials/" + relativePath);
    }

    static void BoostFloorMaterial(Material material)
    {
        material.EnableKeyword("_NORMALMAP");
        SetFloatIfPresent(material, "_BumpScale", 1.95f);
        SetFloatIfPresent(material, "_Smoothness", 0.54f);
        SetFloatIfPresent(material, "_Glossiness", 0.54f);
        SetFloatIfPresent(material, "_OcclusionStrength", 0.9f);
        EditorUtility.SetDirty(material);
    }

    static Material CreateGeneratedMaterial(string name, Color color, Color emission, float emissionStrength, bool transparent)
    {
        var path = Root + "/Materials/Generated/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(FindLitShader()) { name = name };
            AssetDatabase.CreateAsset(material, path);
        }
        else
        {
            material.shader = FindLitShader();
        }

        SetColorIfPresent(material, "_BaseColor", color);
        SetColorIfPresent(material, "_Color", color);
        SetFloatIfPresent(material, "_Metallic", 0f);
        SetFloatIfPresent(material, "_Smoothness", transparent ? 0.72f : 0.42f);
        SetFloatIfPresent(material, "_Glossiness", transparent ? 0.72f : 0.42f);

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
        }

        if (transparent)
        {
            SetFloatIfPresent(material, "_Surface", 1f);
            SetFloatIfPresent(material, "_Blend", 0f);
            SetFloatIfPresent(material, "_SrcBlend", (float)BlendMode.SrcAlpha);
            SetFloatIfPresent(material, "_DstBlend", (float)BlendMode.One);
            SetFloatIfPresent(material, "_ZWrite", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
        }
        else
        {
            SetFloatIfPresent(material, "_Surface", 0f);
            SetFloatIfPresent(material, "_ZWrite", 1f);
            material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = -1;
        }

        EditorUtility.SetDirty(material);
        return material;
    }

    static Shader FindLitShader()
    {
        return Shader.Find("Universal Render Pipeline/Lit")
            ?? Shader.Find("Standard")
            ?? Shader.Find("Diffuse");
    }

    static void SetFloatIfPresent(Material material, string propertyName, float value)
    {
        if (material != null && material.HasProperty(propertyName))
        {
            material.SetFloat(propertyName, value);
        }
    }

    static void SetColorIfPresent(Material material, string propertyName, Color value)
    {
        if (material != null && material.HasProperty(propertyName))
        {
            material.SetColor(propertyName, value);
        }
    }

    static string CleanMaterialName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return string.Empty;
        }
        return name.Replace(" (Instance)", "").Trim();
    }

    static GameObject CreateChild(Transform parent, string name)
    {
        var child = new GameObject(name);
        child.transform.SetParent(parent, false);
        return child;
    }

    static Vector3 RadialPosition(float angle, float radius, float y)
    {
        var radians = angle * Mathf.Deg2Rad;
        return new Vector3(Mathf.Sin(radians) * radius, y, Mathf.Cos(radians) * radius);
    }

    static float RandomRange(System.Random rng, float min, float max)
    {
        return min + (float)rng.NextDouble() * (max - min);
    }

    static void AddArenaBoundaryBlockers(Transform parent)
    {
        var blockers = CreateChild(parent, "Gameplay_Blocking_Colliders_At_Arena_Edge");
        const int segments = 44;
        var radius = ArenaEdgeRadius + 0.35f;
        var arcLength = 2f * Mathf.PI * radius / segments;

        for (var i = 0; i < segments; i++)
        {
            var angle = (i + 0.5f) * 360f / segments;
            var segment = CreateChild(blockers.transform, "Arena_Edge_Blocker_" + i.ToString("00"));
            segment.transform.localPosition = RadialPosition(angle, radius, 1.45f);
            segment.transform.localRotation = Quaternion.Euler(0f, angle, 0f);

            var collider = segment.AddComponent<BoxCollider>();
            collider.size = new Vector3(arcLength * 1.18f, 3.9f, 2.35f);
            collider.center = Vector3.zero;
        }
    }

    static void AddBeam(Transform parent, string name, Vector3 localPosition, float height, float radius, Material material)
    {
        var beam = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        beam.name = name;
        beam.transform.SetParent(parent, false);
        beam.transform.localPosition = localPosition;
        beam.transform.localRotation = Quaternion.identity;
        beam.transform.localScale = new Vector3(radius, height * 0.5f, radius);
        var collider = beam.GetComponent<Collider>();
        if (collider != null)
        {
            Object.DestroyImmediate(collider);
        }
        var renderer = beam.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = material;
        }
    }

    static void AddOrb(Transform parent, string name, Vector3 localPosition, float radius, Material material)
    {
        var orb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        orb.name = name;
        orb.transform.SetParent(parent, false);
        orb.transform.localPosition = localPosition;
        orb.transform.localScale = Vector3.one * radius;
        var collider = orb.GetComponent<Collider>();
        if (collider != null)
        {
            Object.DestroyImmediate(collider);
        }
        var renderer = orb.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = material;
        }
    }

    static void AddFlatCylinder(Transform parent, string assetName, string name, float innerRadius, float outerRadius, float y, int segments, Material material)
    {
        var mesh = new Mesh { name = name + "_Mesh" };
        var vertices = new List<Vector3>();
        var uvs = new List<Vector2>();
        var triangles = new List<int>();
        for (var i = 0; i <= segments; i++)
        {
            var angle = i * Mathf.PI * 2f / segments;
            var sin = Mathf.Sin(angle);
            var cos = Mathf.Cos(angle);
            vertices.Add(new Vector3(sin * innerRadius, y, cos * innerRadius));
            vertices.Add(new Vector3(sin * outerRadius, y, cos * outerRadius));
            uvs.Add(new Vector2(0f, i / (float)segments));
            uvs.Add(new Vector2(1f, i / (float)segments));
        }
        for (var i = 0; i < segments; i++)
        {
            var start = i * 2;
            triangles.Add(start);
            triangles.Add(start + 1);
            triangles.Add(start + 3);
            triangles.Add(start);
            triangles.Add(start + 3);
            triangles.Add(start + 2);
        }
        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        var assetPath = Root + "/GeneratedMeshes/" + assetName + ".asset";
        AssetDatabase.DeleteAsset(assetPath);
        AssetDatabase.CreateAsset(mesh, assetPath);
        mesh = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);

        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = material;
    }

    static bool LooksLikeKamenMapRoot(GameObject go)
    {
        var name = go.name.ToLowerInvariant();
        return name.Contains("kamen")
            || name.Contains("gate3")
            || name.Contains("arena")
            || name.Contains("bg_rad")
            || name.Contains("msh012");
    }

    static int PreserveLightingAndPostProcessChildren(GameObject root, Transform preservedRoot)
    {
        var keep = new List<Transform>();
        foreach (var transform in root.GetComponentsInChildren<Transform>(true))
        {
            if (transform == root.transform)
            {
                continue;
            }
            if (ShouldPreserveGraphicsObject(transform.gameObject))
            {
                keep.Add(GetHighestPreservableAncestor(transform, root.transform));
            }
        }

        var unique = keep
            .Where(t => t != null)
            .Distinct()
            .Where(t => !keep.Any(other => other != t && t.IsChildOf(other)))
            .ToList();

        foreach (var transform in unique)
        {
            transform.SetParent(preservedRoot, true);
        }
        return unique.Count;
    }

    static int EnsureLegacyGraphicsRig(Scene scene, Transform preservedRoot)
    {
        if (FindByName(preservedRoot, "KamenMap_Graphics_Rig") != null)
        {
            return 0;
        }

        foreach (var root in scene.GetRootGameObjects())
        {
            if (FindByName(root.transform, "KamenMap_Graphics_Rig") != null)
            {
                return 0;
            }
        }

        var legacyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(LegacyGraphicsPrefabPath);
        if (legacyPrefab == null)
        {
            Debug.LogWarning("Legacy Kamen graphics prefab not found: " + LegacyGraphicsPrefabPath);
            return 0;
        }

        var temp = PrefabUtility.InstantiatePrefab(legacyPrefab, scene) as GameObject;
        if (temp == null)
        {
            Debug.LogWarning("Could not instantiate legacy Kamen graphics prefab: " + LegacyGraphicsPrefabPath);
            return 0;
        }

        temp.name = "KamenMap_WithGraphics_TempRestore";
        PrefabUtility.UnpackPrefabInstance(temp, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

        var rig = FindByName(temp.transform, "KamenMap_Graphics_Rig");
        if (rig == null)
        {
            Object.DestroyImmediate(temp);
            Debug.LogWarning("Legacy Kamen graphics rig not found inside: " + LegacyGraphicsPrefabPath);
            return 0;
        }

        rig.SetParent(preservedRoot, true);
        Object.DestroyImmediate(temp);
        return 1;
    }

    static bool ShouldPreserveGraphicsObject(GameObject go)
    {
        var lower = go.name.ToLowerInvariant();
        return go.GetComponent<Light>() != null
            || go.GetComponent<ReflectionProbe>() != null
            || go.GetComponent<Volume>() != null
            || lower.Contains("post process")
            || lower.Contains("graphics")
            || lower.Contains("lighting");
    }

    static Transform GetHighestPreservableAncestor(Transform transform, Transform stopBefore)
    {
        var current = transform;
        while (current.parent != null && current.parent != stopBefore && ShouldPreserveGraphicsObject(current.parent.gameObject))
        {
            current = current.parent;
        }
        return current;
    }

    static GameObject FindOrCreateSceneRoot(Scene scene, string name)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            if (root.name == name)
            {
                return root;
            }
        }

        var go = new GameObject(name);
        SceneManager.MoveGameObjectToScene(go, scene);
        return go;
    }

    static GameObject FindLevel(Scene scene)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            var level = FindByName(root.transform, "Level");
            if (level != null)
            {
                return level.gameObject;
            }
        }
        return null;
    }

    static Transform FindByName(Transform transform, string name)
    {
        if (transform.name == name)
        {
            return transform;
        }
        for (var i = 0; i < transform.childCount; i++)
        {
            var found = FindByName(transform.GetChild(i), name);
            if (found != null)
            {
                return found;
            }
        }
        return null;
    }

    static bool TryGetLoadedScene(string scenePath, out Scene scene)
    {
        for (var i = 0; i < SceneManager.sceneCount; i++)
        {
            var loaded = SceneManager.GetSceneAt(i);
            if (loaded.path == scenePath)
            {
                scene = loaded;
                return true;
            }
        }

        scene = default;
        return false;
    }

    static void EnsureFolder(string root, string child)
    {
        var path = root + "/" + child;
        if (!AssetDatabase.IsValidFolder(path))
        {
            AssetDatabase.CreateFolder(root, child);
        }
    }

    static void EnsureNestedFolder(string parent, string child)
    {
        var current = parent;
        foreach (var segment in child.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var next = current + "/" + segment;
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, segment);
            }
            current = next;
        }
    }

    static void MarkStatic(GameObject go)
    {
        foreach (var transform in go.GetComponentsInChildren<Transform>(true))
        {
            GameObjectUtility.SetStaticEditorFlags(transform.gameObject,
                StaticEditorFlags.BatchingStatic |
                StaticEditorFlags.OccluderStatic |
                StaticEditorFlags.OccludeeStatic |
                StaticEditorFlags.ReflectionProbeStatic);
        }
    }
}
