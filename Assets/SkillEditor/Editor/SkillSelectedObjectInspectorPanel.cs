using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace SkillEditor.Editor
{
    public sealed class SkillSelectedObjectInspectorPanel
    {
        private readonly Foldout root;
        private readonly IMGUIContainer inspectorContainer;
        private readonly List<Component> selectedComponents = new List<Component>();
        private readonly List<UnityEditor.Editor> componentEditors = new List<UnityEditor.Editor>();
        private GameObject selectedObject;
        private UnityEditor.Editor gameObjectEditor;
        private Vector2 scrollPosition;

        public SkillSelectedObjectInspectorPanel()
        {
            root = new Foldout
            {
                text = "Selected Object Inspector",
                value = true
            };
            root.AddToClassList("skill-panel");

            inspectorContainer = new IMGUIContainer(DrawInspector)
            {
                focusable = true
            };
            inspectorContainer.AddToClassList("skill-inspector-container");
            root.Add(inspectorContainer);
        }

        public VisualElement Root => root;

        public void SetTarget(Transform selectedTransform)
        {
            GameObject nextObject = selectedTransform != null ? selectedTransform.gameObject : null;
            if (selectedObject == nextObject)
                return;

            selectedObject = nextObject;
            ReleaseEditor();

            if (selectedObject != null)
                RebuildEditors();

            inspectorContainer.MarkDirtyRepaint();
        }

        public void Dispose()
        {
            ReleaseEditor();
        }

        private void DrawInspector()
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            if (selectedObject == null)
            {
                EditorGUILayout.HelpBox("No preview object selected.", MessageType.Info);
                EditorGUILayout.EndScrollView();
                return;
            }

            if (gameObjectEditor == null || componentEditors.Count != selectedComponents.Count)
                RebuildEditors();

            if (gameObjectEditor == null)
            {
                EditorGUILayout.HelpBox("Inspector could not be created.", MessageType.Warning);
                EditorGUILayout.EndScrollView();
                return;
            }

            EditorGUI.BeginChangeCheck();
            DrawGameObjectHeader();
            DrawComponentInspectors();
            if (EditorGUI.EndChangeCheck())
                inspectorContainer.MarkDirtyRepaint();

            EditorGUILayout.EndScrollView();
        }

        private void DrawGameObjectHeader()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                bool active = EditorGUILayout.ToggleLeft(selectedObject.name, selectedObject.activeSelf, EditorStyles.boldLabel);
                if (active != selectedObject.activeSelf)
                {
                    Undo.RecordObject(selectedObject, "Toggle Preview Object Active");
                    selectedObject.SetActive(active);
                }

                gameObjectEditor.OnInspectorGUI();
            }
        }

        private void DrawComponentInspectors()
        {
            for (int i = 0; i < selectedComponents.Count; i++)
            {
                Component component = selectedComponents[i];
                UnityEditor.Editor editor = i < componentEditors.Count ? componentEditors[i] : null;

                if (component == null)
                {
                    EditorGUILayout.HelpBox("Missing Component", MessageType.Warning);
                    continue;
                }

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    bool expanded = EditorGUILayout.InspectorTitlebar(true, component);
                    if (expanded && editor != null)
                        editor.OnInspectorGUI();
                }
            }
        }

        private void RebuildEditors()
        {
            ReleaseEditor();

            if (selectedObject == null)
                return;

            UnityEditor.Editor.CreateCachedEditor(selectedObject, null, ref gameObjectEditor);
            selectedObject.GetComponents(selectedComponents);

            foreach (Component component in selectedComponents)
            {
                UnityEditor.Editor editor = null;
                if (component != null)
                    UnityEditor.Editor.CreateCachedEditor(component, null, ref editor);

                componentEditors.Add(editor);
            }
        }

        private void ReleaseEditor()
        {
            if (gameObjectEditor != null)
            {
                Object.DestroyImmediate(gameObjectEditor);
                gameObjectEditor = null;
            }

            foreach (UnityEditor.Editor editor in componentEditors)
            {
                if (editor != null)
                    Object.DestroyImmediate(editor);
            }

            componentEditors.Clear();
            selectedComponents.Clear();
        }
    }
}
