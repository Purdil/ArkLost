using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

public static class KamenGameSceneApplier
{
    const string ScenePath = "Assets/_Scenes/GameScene.unity";
    const string PreferredPrefabPath = "Assets/KamenMap/Prefabs/KamenMap_WithGraphics.prefab";
    const string FallbackPrefabPath = "Assets/KamenMap/Prefabs/KamenMap.prefab";
    const string SkyboxPath = "Assets/KamenMap/Materials/KamenMap_Skybox.mat";
    const string ProfilePath = "Assets/KamenMap/Materials/KamenMap_PostProcessProfile.asset";

    [MenuItem("Tools/Kamen Map/Apply To GameScene Level")]
    public static void ApplyFromMenu()
    {
        ApplyNow();
    }

    public static void ApplyNow()
    {
        var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
        if (sceneAsset == null)
        {
            throw new FileNotFoundException("GameScene not found.", ScenePath);
        }

        var prefabPath = AssetDatabase.LoadAssetAtPath<GameObject>(PreferredPrefabPath) != null
            ? PreferredPrefabPath
            : FallbackPrefabPath;
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
        {
            throw new FileNotFoundException("Kamen map prefab not found.", PreferredPrefabPath);
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

        var removedChildren = level.transform.childCount;
        for (var i = level.transform.childCount - 1; i >= 0; i--)
        {
            Object.DestroyImmediate(level.transform.GetChild(i).gameObject);
        }

        var instance = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
        if (instance == null)
        {
            throw new InvalidOperationException("Could not instantiate prefab: " + prefabPath);
        }

        instance.name = "KamenMap_WithGraphics";
        instance.transform.SetParent(level.transform, false);
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;
        instance.transform.localScale = Vector3.one;

        ApplyKamenSceneGraphics(scene);

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
        Debug.Log("KamenGameSceneApplier applied " + prefabPath + " to " + ScenePath + " under Level and refreshed Kamen post processing. Removed children: " + removedChildren);
    }

    static void ApplyKamenSceneGraphics(Scene scene)
    {
        var profile = RebuildKamenPostProcessProfile();
        var skybox = AssetDatabase.LoadAssetAtPath<Material>(SkyboxPath);

        if (skybox != null)
        {
            RenderSettings.skybox = skybox;
        }

        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.055f, 0.07f, 0.105f, 1f);
        RenderSettings.ambientEquatorColor = new Color(0.052f, 0.065f, 0.092f, 1f);
        RenderSettings.ambientGroundColor = new Color(0.024f, 0.027f, 0.037f, 1f);
        RenderSettings.reflectionIntensity = 0.36f;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = new Color(0.028f, 0.035f, 0.055f, 1f);
        RenderSettings.fogDensity = 0.0022f;

        DisableCompetingGlobalVolumes(scene);
        DisableCompetingLights(scene);
        CreateScenePostProcessVolume(scene, profile);
        ConfigureSceneCameras(scene);
    }

    static VolumeProfile RebuildKamenPostProcessProfile()
    {
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "KamenMap_PostProcessProfile";
            AssetDatabase.CreateAsset(profile, ProfilePath);
        }

        foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(ProfilePath))
        {
            if (asset is VolumeComponent)
            {
                Object.DestroyImmediate(asset, true);
            }
        }

        for (var i = profile.components.Count - 1; i >= 0; i--)
        {
            var component = profile.components[i];
            if (component != null)
            {
                Object.DestroyImmediate(component, true);
            }
        }
        profile.components.Clear();

        var tonemapping = AddVolumeComponent<Tonemapping>(profile);
        tonemapping.mode.Override(TonemappingMode.ACES);

        var color = AddVolumeComponent<ColorAdjustments>(profile);
        color.postExposure.Override(0.14f);
        color.contrast.Override(8f);
        color.saturation.Override(-3f);
        color.colorFilter.Override(new Color(0.86f, 0.93f, 1f, 1f));

        var bloom = AddVolumeComponent<Bloom>(profile);
        bloom.threshold.Override(0.9f);
        bloom.intensity.Override(0.16f);
        bloom.scatter.Override(0.48f);
        bloom.tint.Override(new Color(0.58f, 0.72f, 1f, 1f));

        var vignette = AddVolumeComponent<Vignette>(profile);
        vignette.color.Override(new Color(0.02f, 0.018f, 0.023f, 1f));
        vignette.intensity.Override(0.20f);
        vignette.smoothness.Override(0.42f);

        var chromaticAberration = AddVolumeComponent<ChromaticAberration>(profile);
        chromaticAberration.intensity.Override(0.04f);

        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(ProfilePath, ImportAssetOptions.ForceUpdate);
        return profile;
    }

    static T AddVolumeComponent<T>(VolumeProfile profile) where T : VolumeComponent
    {
        var component = ScriptableObject.CreateInstance<T>();
        component.name = typeof(T).Name;
        component.active = true;
        component.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
        component.SetAllOverridesTo(true);
        AssetDatabase.AddObjectToAsset(component, profile);
        profile.components.Add(component);
        EditorUtility.SetDirty(component);
        EditorUtility.SetDirty(profile);
        return component;
    }

    static void DisableCompetingGlobalVolumes(Scene scene)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var volume in root.GetComponentsInChildren<Volume>(true))
            {
                if (volume == null || volume.name.Contains("Kamen", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (volume.isGlobal)
                {
                    volume.priority = -100f;
                    volume.weight = 0f;
                    EditorUtility.SetDirty(volume);
                }
            }
        }
    }

    static void DisableCompetingLights(Scene scene)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var light in root.GetComponentsInChildren<Light>(true))
            {
                if (light == null || IsKamenObject(light.transform))
                {
                    continue;
                }

                if (light.type == LightType.Directional)
                {
                    light.enabled = false;
                    light.intensity = 0f;
                    EditorUtility.SetDirty(light);
                    EditorUtility.SetDirty(light.gameObject);
                }
            }
        }
    }

    static bool IsKamenObject(Transform transform)
    {
        while (transform != null)
        {
            if (transform.name.Contains("Kamen", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            transform = transform.parent;
        }

        return false;
    }

    static void CreateScenePostProcessVolume(Scene scene, VolumeProfile profile)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            if (root.name == "Kamen Scene Graphics")
            {
                Object.DestroyImmediate(root);
            }
        }

        var graphicsRoot = new GameObject("Kamen Scene Graphics");
        SceneManager.MoveGameObjectToScene(graphicsRoot, scene);

        var volumeObject = new GameObject("Kamen Scene Post Process");
        volumeObject.transform.SetParent(graphicsRoot.transform, false);
        var volume = volumeObject.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 1000f;
        volume.weight = 1f;
        volume.sharedProfile = profile;
    }

    static void ConfigureSceneCameras(Scene scene)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var camera in root.GetComponentsInChildren<Camera>(true))
            {
                if (camera == null)
                {
                    continue;
                }

                camera.allowHDR = true;
                camera.depthTextureMode |= DepthTextureMode.DepthNormals;

                var cameraData = camera.GetComponent<UniversalAdditionalCameraData>();
                if (cameraData == null)
                {
                    cameraData = camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
                }

                cameraData.renderPostProcessing = true;
                cameraData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                cameraData.antialiasingQuality = AntialiasingQuality.High;
                cameraData.volumeLayerMask = ~0;
                EditorUtility.SetDirty(camera);
                EditorUtility.SetDirty(cameraData);
            }
        }
    }

    static bool TryGetLoadedScene(string scenePath, out Scene scene)
    {
        for (var i = 0; i < SceneManager.sceneCount; i++)
        {
            var loadedScene = SceneManager.GetSceneAt(i);
            if (loadedScene.path == scenePath)
            {
                scene = loadedScene;
                return true;
            }
        }

        scene = default;
        return false;
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

    static string ProjectPath(string relativePath)
    {
        var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
        if (string.IsNullOrEmpty(projectRoot))
        {
            throw new InvalidOperationException("Could not resolve Unity project root.");
        }

        return Path.Combine(projectRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
    }
}
