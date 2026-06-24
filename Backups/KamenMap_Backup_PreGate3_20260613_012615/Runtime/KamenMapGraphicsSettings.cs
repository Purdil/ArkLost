using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;

[ExecuteAlways]
public sealed class KamenMapGraphicsSettings : MonoBehaviour
{
    public bool applyOnEnable = true;
    public bool configureMainCamera = true;
    public Material skybox;

    [Header("Fog")]
    public bool fog = true;
    public FogMode fogMode = FogMode.ExponentialSquared;
    public Color fogColor = new Color(0.025f, 0.028f, 0.036f, 1f);
    public float fogDensity = 0.0035f;

    [Header("Ambient")]
    public Color ambientSkyColor = new Color(0.055f, 0.063f, 0.086f, 1f);
    public Color ambientEquatorColor = new Color(0.09f, 0.065f, 0.065f, 1f);
    public Color ambientGroundColor = new Color(0.025f, 0.022f, 0.024f, 1f);
    public float reflectionIntensity = 0.38f;

    void OnEnable()
    {
        if (applyOnEnable)
        {
            Apply();
        }
    }

    void OnValidate()
    {
        if (!Application.isPlaying && applyOnEnable)
        {
            Apply();
        }
    }

    [ContextMenu("Apply Kamen Map Graphics Settings")]
    public void Apply()
    {
        if (skybox != null)
        {
            RenderSettings.skybox = skybox;
        }

        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = ambientSkyColor;
        RenderSettings.ambientEquatorColor = ambientEquatorColor;
        RenderSettings.ambientGroundColor = ambientGroundColor;
        RenderSettings.reflectionIntensity = reflectionIntensity;
        RenderSettings.fog = fog;
        RenderSettings.fogMode = fogMode;
        RenderSettings.fogColor = fogColor;
        RenderSettings.fogDensity = fogDensity;

        if (configureMainCamera)
        {
            ConfigureCamera(Camera.main);
        }
    }

    static void ConfigureCamera(Camera camera)
    {
        if (camera == null)
        {
            return;
        }

        camera.allowHDR = true;
        camera.depthTextureMode |= DepthTextureMode.DepthNormals;

        var cameraDataType = Type.GetType("UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, Unity.RenderPipelines.Universal.Runtime");
        if (cameraDataType == null)
        {
            return;
        }

        var cameraData = camera.GetComponent(cameraDataType) ?? camera.gameObject.AddComponent(cameraDataType);
        SetProperty(cameraData, "renderPostProcessing", true);
        SetEnumProperty(cameraData, "antialiasing", "SubpixelMorphologicalAntiAliasing");
        SetEnumProperty(cameraData, "antialiasingQuality", "High");
    }

    static void SetProperty(object target, string propertyName, object value)
    {
        var property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (property != null && property.CanWrite)
        {
            property.SetValue(target, value);
        }
    }

    static void SetEnumProperty(object target, string propertyName, string enumName)
    {
        var property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (property == null || !property.CanWrite || !property.PropertyType.IsEnum)
        {
            return;
        }

        try
        {
            property.SetValue(target, Enum.Parse(property.PropertyType, enumName));
        }
        catch
        {
            // Older URP versions can have slightly different enum names; leaving the camera default is safe.
        }
    }
}
