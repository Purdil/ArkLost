using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class KamenModelBlueAccentApplier
{
    const string ScenePath = "Assets/_Scenes/GameScene.unity";
    const string ProfilePath = "Assets/KamenMap/Materials/KamenMap_PostProcessProfile.asset";
    const string AccentRootName = "Kamen Model Blue Accent";
    const string ApplyRequestPath = "ProjectSettings/KamenModelBlueAccentApply.request";

    [InitializeOnLoadMethod]
    static void ApplyRequestedAfterReload()
    {
        EditorApplication.delayCall += () =>
        {
            var requestPath = ProjectPath(ApplyRequestPath);
            if (!File.Exists(requestPath))
            {
                return;
            }

            try
            {
                ApplyNow();
                File.Delete(requestPath);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        };
    }

    [MenuItem("Tools/Kamen Map/Boost Kamen Blue Accent In GameScene")]
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

        ApplyRenderSettings();
        BoostKamenSceneLights(scene);
        CreateModelAccentRig(scene);
        BoostKamenPostProcessProfile();
        ConfigureSceneCameras(scene);

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
        Debug.Log("Boosted Kamen blue accent lighting in " + ScenePath + ".");
    }

    static void ApplyRenderSettings()
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

    static void BoostKamenSceneLights(Scene scene)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var light in root.GetComponentsInChildren<Light>(true))
            {
                if (light == null)
                {
                    continue;
                }

                switch (light.name)
                {
                    case "Kamen Directional Light":
                        light.enabled = true;
                        light.color = new Color(0.62f, 0.76f, 1f, 1f);
                        light.intensity = Mathf.Max(light.intensity, 1.12f);
                        break;
                    case "Kamen Cold Blue Rim":
                        light.color = new Color(0.25f, 0.52f, 1f, 1f);
                        light.intensity = Mathf.Max(light.intensity, 2.65f);
                        light.range = Mathf.Max(light.range, 96f);
                        break;
                    case "Kamen Cool Floor Fill":
                        light.color = new Color(0.30f, 0.50f, 0.95f, 1f);
                        light.intensity = Mathf.Max(light.intensity, 1.55f);
                        break;
                    case "Kamen Low Blue Fill":
                        light.color = new Color(0.18f, 0.34f, 0.78f, 1f);
                        light.intensity = Mathf.Max(light.intensity, 0.90f);
                        break;
                    default:
                        continue;
                }

                if (light.type == LightType.Directional)
                {
                    light.shadows = LightShadows.Soft;
                    light.shadowStrength = Mathf.Min(light.shadowStrength <= 0f ? 0.38f : light.shadowStrength, 0.45f);
                }
                else
                {
                    light.shadows = LightShadows.None;
                    light.shadowStrength = 0f;
                }
                EditorUtility.SetDirty(light);
                EditorUtility.SetDirty(light.gameObject);
            }
        }
    }

    static void CreateModelAccentRig(Scene scene)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            if (root.name == AccentRootName)
            {
                Object.DestroyImmediate(root);
            }
        }

        var center = new Vector3(0f, 2.1f, 0f);
        var radius = 4.5f;
        if (TryCalculateKamenModelBounds(scene, out var bounds))
        {
            center = bounds.center;
            radius = Mathf.Clamp(Mathf.Max(bounds.extents.x, bounds.extents.y, bounds.extents.z), 2.5f, 8f);
        }

        var accentRoot = new GameObject(AccentRootName);
        SceneManager.MoveGameObjectToScene(accentRoot, scene);

        CreateAccentLight(
            accentRoot.transform,
            "Kamen Model Blue Rim",
            LightType.Spot,
            center + new Vector3(radius * 0.65f, radius * 1.05f, -radius * 1.45f),
            center + Vector3.up * radius * 0.20f,
            new Color(0.12f, 0.40f, 1f, 1f),
            2.2f,
            radius * 4.0f,
            48f);

        CreateAccentLight(
            accentRoot.transform,
            "Kamen Model Cyan Face Catch",
            LightType.Spot,
            center + new Vector3(-radius * 0.48f, radius * 0.75f, radius * 1.35f),
            center + Vector3.up * radius * 0.18f,
            new Color(0.30f, 0.72f, 1f, 1f),
            1.25f,
            radius * 3.2f,
            42f);

        CreateAccentLight(
            accentRoot.transform,
            "Kamen Model Low Blue Lift",
            LightType.Point,
            center + new Vector3(0f, -radius * 0.20f, 0f),
            center,
            new Color(0.10f, 0.28f, 0.92f, 1f),
            0.95f,
            radius * 2.6f,
            0f);

        EditorUtility.SetDirty(accentRoot);
    }

    static Light CreateAccentLight(
        Transform parent,
        string name,
        LightType type,
        Vector3 position,
        Vector3 target,
        Color color,
        float intensity,
        float range,
        float spotAngle)
    {
        var lightObject = new GameObject(name);
        lightObject.transform.SetParent(parent, true);
        lightObject.transform.position = position;

        if (type == LightType.Spot || type == LightType.Directional)
        {
            var direction = target - position;
            if (direction.sqrMagnitude > 0.0001f)
            {
                lightObject.transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            }
        }

        var light = lightObject.AddComponent<Light>();
        light.type = type;
        light.color = color;
        light.intensity = intensity;
        light.range = range;
        light.spotAngle = spotAngle;
        light.shadows = type == LightType.Directional ? LightShadows.Soft : LightShadows.None;
        light.shadowStrength = type == LightType.Directional ? 0.36f : 0f;

        EditorUtility.SetDirty(lightObject);
        EditorUtility.SetDirty(light);
        return light;
    }

    static bool TryCalculateKamenModelBounds(Scene scene, out Bounds bounds)
    {
        var hasBounds = false;
        bounds = new Bounds(Vector3.zero, Vector3.zero);

        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null || !IsKamenModelObject(renderer.transform))
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }
        }

        return hasBounds;
    }

    static bool IsKamenModelObject(Transform transform)
    {
        while (transform != null)
        {
            var name = transform.name;
            if (name.StartsWith("Kamen_v", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("mn_cdkcn", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("wp_mn_cdkcn", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            transform = transform.parent;
        }

        return false;
    }

    static void BoostKamenPostProcessProfile()
    {
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
        if (profile == null)
        {
            return;
        }

        var color = EnsureVolumeOverride<ColorAdjustments>(profile);
        color.postExposure.Override(0.20f);
        color.contrast.Override(Mathf.Clamp(color.contrast.value, -6f, 8f));
        color.saturation.Override(-2f);
        color.colorFilter.Override(new Color(0.82f, 0.92f, 1f, 1f));

        var bloom = EnsureVolumeOverride<Bloom>(profile);
        bloom.threshold.Override(0.82f);
        bloom.intensity.Override(0.28f);
        bloom.scatter.Override(0.55f);
        bloom.tint.Override(new Color(0.46f, 0.70f, 1f, 1f));

        EditorUtility.SetDirty(profile);
        AssetDatabase.ImportAsset(ProfilePath, ImportAssetOptions.ForceUpdate);
    }

    static T EnsureVolumeOverride<T>(VolumeProfile profile) where T : VolumeComponent
    {
        if (profile.TryGet<T>(out var component))
        {
            component.SetAllOverridesTo(true);
            EditorUtility.SetDirty(component);
            return component;
        }

        component = ScriptableObject.CreateInstance<T>();
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
