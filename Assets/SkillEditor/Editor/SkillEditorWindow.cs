using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace SkillEditor.Editor
{
    public sealed class SkillEditorWindow : EditorWindow
    {
        private const string StyleSheetPath = "Assets/SkillEditor/Editor/USS/SkillEditor.uss";

        private SkillPreviewSceneContext previewContext;
        private SkillTransformTool transformTool;
        private SkillPreviewViewport previewViewport;
        private SkillObjectHierarchyPanel hierarchyPanel;
        private SkillAnimationClipPanel clipPanel;
        private SkillTimelinePanel timelinePanel;
        private SkillAnimationEventPanel eventPanel;
        private SkillSelectedObjectInspectorPanel selectedObjectInspectorPanel;
        private SkillPlaneSettingsPanel planeSettingsPanel;
        private ObjectField sourceObjectField;
        private Label statusLabel;

        [MenuItem("Tools/SkillEditor")]
        public static void Open()
        {
            SkillEditorWindow window = GetWindow<SkillEditorWindow>();
            window.titleContent = new GUIContent("SkillEditor");
            window.minSize = new Vector2(1280f, 760f);
            window.Focus();
        }

        public void CreateGUI()
        {
            EnsureCoreObjects();
            previewViewport?.Dispose();

            rootVisualElement.Clear();
            LoadStyleSheet();
            BuildLayout();
            UseCurrentSelection();
        }

        private void OnEnable()
        {
            EnsureCoreObjects();
            EditorApplication.update += Tick;
            Selection.selectionChanged += HandleSelectionChanged;
            Undo.undoRedoPerformed += HandleUndoRedo;
            EditorSceneManager.activeSceneChangedInEditMode += HandleActiveSceneChanged;
            saveChangesMessage = "저장되지 않은 Animation Event 변경 사항이 있습니다.";
        }

        private void OnDisable()
        {
            EditorApplication.update -= Tick;
            Selection.selectionChanged -= HandleSelectionChanged;
            Undo.undoRedoPerformed -= HandleUndoRedo;
            EditorSceneManager.activeSceneChangedInEditMode -= HandleActiveSceneChanged;

            timelinePanel?.Dispose();
            previewViewport?.Dispose();
            selectedObjectInspectorPanel?.Dispose();
            previewContext?.Dispose();
            previewViewport = null;
            timelinePanel = null;
            selectedObjectInspectorPanel = null;
            previewContext = null;
            transformTool = null;
        }

        public override void SaveChanges()
        {
            eventPanel?.SaveEvents();
            hasUnsavedChanges = false;
            base.SaveChanges();
        }

        public override void DiscardChanges()
        {
            eventPanel?.LoadClip(clipPanel != null ? clipPanel.SelectedClip : null);
            hasUnsavedChanges = false;
            base.DiscardChanges();
        }

        private void EnsureCoreObjects()
        {
            if (previewContext == null)
            {
                previewContext = new SkillPreviewSceneContext();
                previewContext.Initialize();
                previewContext.PreviewRootChanged += HandlePreviewRootChanged;
                previewContext.SelectedTransformChanged += HandleSelectedTransformChanged;
            }

            if (transformTool == null)
            {
                transformTool = new SkillTransformTool();
                transformTool.ModeChanged += HandleTransformToolChanged;
                transformTool.TransformChanged += HandlePreviewTransformChanged;
            }
        }

        private void LoadStyleSheet()
        {
            StyleSheet styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(StyleSheetPath);
            if (styleSheet != null)
                rootVisualElement.styleSheets.Add(styleSheet);
        }

        private void BuildLayout()
        {
            VisualElement root = rootVisualElement;
            root.AddToClassList("skill-root");

            BuildToolbar(root);

            VisualElement body = new VisualElement();
            body.AddToClassList("skill-body");
            root.Add(body);

            VisualElement leftColumn = new VisualElement();
            leftColumn.AddToClassList("skill-left-column");
            body.Add(leftColumn);

            VisualElement centerColumn = new VisualElement();
            centerColumn.AddToClassList("skill-center-column");
            body.Add(centerColumn);

            VisualElement rightColumn = new VisualElement();
            rightColumn.AddToClassList("skill-right-column");
            body.Add(rightColumn);

            previewViewport = new SkillPreviewViewport(previewContext, transformTool);
            hierarchyPanel = new SkillObjectHierarchyPanel(previewContext, transform => previewViewport.Focus(transform));
            clipPanel = new SkillAnimationClipPanel(previewContext);
            eventPanel = new SkillAnimationEventPanel(() => timelinePanel != null ? timelinePanel.CurrentTime : 0f, time => timelinePanel?.SetTime(time, true));
            timelinePanel = new SkillTimelinePanel(previewContext);
            selectedObjectInspectorPanel = new SkillSelectedObjectInspectorPanel();
            planeSettingsPanel = new SkillPlaneSettingsPanel(previewContext);

            clipPanel.ClipSelected += HandleClipSelected;
            eventPanel.DirtyChanged += UpdateDirtyState;
            eventPanel.EventsChanged += HandleEventsChanged;
            timelinePanel.Sampled += HandleTimelineSampled;

            leftColumn.Add(hierarchyPanel.Root);
            leftColumn.Add(clipPanel.Root);
            leftColumn.Add(planeSettingsPanel.Root);
            centerColumn.Add(previewViewport.Root);
            centerColumn.Add(timelinePanel.Root);
            rightColumn.Add(eventPanel.Root);
            rightColumn.Add(selectedObjectInspectorPanel.Root);
        }

        private void BuildToolbar(VisualElement root)
        {
            VisualElement toolbar = new VisualElement();
            toolbar.AddToClassList("skill-toolbar");
            root.Add(toolbar);

            sourceObjectField = new ObjectField("Scene Object")
            {
                objectType = typeof(GameObject),
                allowSceneObjects = true
            };
            sourceObjectField.AddToClassList("skill-source-field");
            sourceObjectField.RegisterValueChangedCallback(evt =>
            {
                GameObject selected = evt.newValue as GameObject;
                LoadSourceObject(selected);
            });
            toolbar.Add(sourceObjectField);

            Button useSelectionButton = new Button(UseCurrentSelection) { text = "Use Selection" };
            toolbar.Add(useSelectionButton);

            Button refreshButton = new Button(RefreshPanels) { text = "Refresh" };
            toolbar.Add(refreshButton);

            statusLabel = new Label();
            statusLabel.AddToClassList("skill-status-label");
            toolbar.Add(statusLabel);
        }

        private void UseCurrentSelection()
        {
            GameObject selected = Selection.activeGameObject;
            if (sourceObjectField != null)
                sourceObjectField.SetValueWithoutNotify(selected);

            LoadSourceObject(selected);
        }

        private void LoadSourceObject(GameObject selected)
        {
            if (selected == null)
            {
                previewContext.ClearPreviewObject();
                SetStatus("No scene GameObject selected.");
                return;
            }

            if (EditorUtility.IsPersistent(selected))
            {
                previewContext.ClearPreviewObject();
                SetStatus("Select a GameObject from an open scene, not a project asset.");
                return;
            }

            previewContext.LoadSelection(selected);
            SetStatus($"Loaded preview object: {selected.name}");
        }

        private void RefreshPanels()
        {
            hierarchyPanel?.Refresh();
            clipPanel?.Refresh();
            previewViewport?.Repaint();
            UpdateDirtyState();
        }

        private void HandlePreviewRootChanged()
        {
            hierarchyPanel?.Refresh();
            clipPanel?.Refresh();

            if (previewContext.PreviewRoot != null)
                previewViewport?.Focus(previewContext.PreviewRoot.transform);

            previewViewport?.Repaint();
        }

        private void HandleSelectedTransformChanged(Transform selected)
        {
            hierarchyPanel?.Refresh();
            hierarchyPanel?.SyncTransformFields();
            selectedObjectInspectorPanel?.SetTarget(selected);
            previewViewport?.Repaint();
        }

        private void HandleClipSelected(AnimationClip clip)
        {
            timelinePanel?.SetClip(clip);
            eventPanel?.LoadClip(clip);
            UpdateDirtyState();
        }

        private void HandleEventsChanged()
        {
            previewViewport?.Repaint();
            UpdateDirtyState();
        }

        private void HandleTimelineSampled()
        {
            hierarchyPanel?.SyncTransformFields();
            selectedObjectInspectorPanel?.SetTarget(previewContext.SelectedTransform);
            previewViewport?.Repaint();
        }

        private void HandleTransformToolChanged()
        {
            previewViewport?.Repaint();
        }

        private void HandlePreviewTransformChanged()
        {
            hierarchyPanel?.SyncTransformFields();
            previewViewport?.Repaint();
        }

        private void HandleSelectionChanged()
        {
            GameObject selected = Selection.activeGameObject;
            if (selected != null && !EditorUtility.IsPersistent(selected))
            {
                if (sourceObjectField != null)
                    sourceObjectField.SetValueWithoutNotify(selected);

                LoadSourceObject(selected);
                return;
            }

            if (previewContext.SourceRoot == null)
            {
                previewContext.ClearPreviewObject();
                SetStatus("No scene GameObject selected.");
            }
        }

        private void HandleUndoRedo()
        {
            hierarchyPanel?.SyncTransformFields();
            previewViewport?.Repaint();
        }

        private void HandleActiveSceneChanged(Scene oldScene, Scene newScene)
        {
            previewContext.ClearPreviewObject();
            SetStatus($"Active scene changed to {newScene.name}. Preview cleared.");
        }

        private void Tick()
        {
            timelinePanel?.Tick();
            hierarchyPanel?.SyncTransformFields();
        }

        private void UpdateDirtyState()
        {
            hasUnsavedChanges = eventPanel != null && eventPanel.HasDirtyChanges;
        }

        private void SetStatus(string message)
        {
            if (statusLabel != null)
                statusLabel.text = message;
        }
    }
}
