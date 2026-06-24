using UnityEditor;
using UnityEngine;

namespace SkillEditor.Editor
{
    public static class SkillTransformInspectorUtility
    {
        public static Vector3 GetLocalPosition(Transform target)
        {
            SerializedObject serializedObject = new SerializedObject(target);
            SerializedProperty property = serializedObject.FindProperty("m_LocalPosition");
            return property != null ? property.vector3Value : target.localPosition;
        }

        public static Vector3 GetLocalEulerAngles(Transform target)
        {
            SerializedObject serializedObject = new SerializedObject(target);
            SerializedProperty property = serializedObject.FindProperty("m_LocalEulerAnglesHint");
            return property != null ? property.vector3Value : target.localEulerAngles;
        }

        public static Vector3 GetLocalScale(Transform target)
        {
            SerializedObject serializedObject = new SerializedObject(target);
            SerializedProperty property = serializedObject.FindProperty("m_LocalScale");
            return property != null ? property.vector3Value : target.localScale;
        }

        public static void SetLocalPosition(Transform target, Vector3 value)
        {
            Undo.RecordObject(target, "Edit Preview Local Position");
            target.localPosition = value;
        }

        public static void SetLocalEulerAngles(Transform target, Vector3 value)
        {
            Undo.RecordObject(target, "Edit Preview Local Rotation");
            target.localEulerAngles = value;
            WriteLocalEulerAnglesHint(target, value);
        }

        public static void SetLocalScale(Transform target, Vector3 value)
        {
            Undo.RecordObject(target, "Edit Preview Local Scale");
            target.localScale = value;
        }

        public static void WriteLocalEulerAnglesHint(Transform target, Vector3 value)
        {
            SerializedObject serializedObject = new SerializedObject(target);
            SerializedProperty property = serializedObject.FindProperty("m_LocalEulerAnglesHint");
            if (property == null)
                return;

            property.vector3Value = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
