#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace _Scripts.CombatSystem.RenderRange
{
    public static class RenderRangeDecalRendererFeatureInstaller
    {
        [MenuItem("Tools/ArkLost/Render Range/Enable URP Decals")]
        public static void EnableUrpDecals()
        {
            string[] rendererGuids = AssetDatabase.FindAssets("t:UniversalRendererData", new[] { "Assets/Settings" });
            int changedCount = 0;

            foreach (string rendererGuid in rendererGuids)
            {
                string rendererPath = AssetDatabase.GUIDToAssetPath(rendererGuid);
                UniversalRendererData rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
                if (rendererData == null || rendererData.TryGetRendererFeature(out DecalRendererFeature _))
                {
                    continue;
                }

                DecalRendererFeature decalFeature = ScriptableObject.CreateInstance<DecalRendererFeature>();
                decalFeature.name = "DecalRendererFeature";
                decalFeature.Create();
                AssetDatabase.AddObjectToAsset(decalFeature, rendererData);
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(decalFeature, out _, out long localId);

                SerializedObject serializedRenderer = new SerializedObject(rendererData);
                SerializedProperty features = serializedRenderer.FindProperty("m_RendererFeatures");
                SerializedProperty featureMap = serializedRenderer.FindProperty("m_RendererFeatureMap");
                int index = features.arraySize;

                features.InsertArrayElementAtIndex(index);
                features.GetArrayElementAtIndex(index).objectReferenceValue = decalFeature;
                featureMap.InsertArrayElementAtIndex(index);
                featureMap.GetArrayElementAtIndex(index).longValue = localId;
                serializedRenderer.ApplyModifiedPropertiesWithoutUndo();

                rendererData.SetDirty();
                EditorUtility.SetDirty(decalFeature);
                EditorUtility.SetDirty(rendererData);
                changedCount++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Render Range: enabled URP decals on {changedCount} renderer asset(s).");
        }
    }
}
#endif
