using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace SkillEditor.Editor
{
    public sealed class SkillObjectHierarchyPanel
    {
        private readonly SkillPreviewSceneContext context;
        private readonly Action<Transform> focusRequested;
        private readonly Foldout root;
        private readonly ScrollView hierarchyView;
        private readonly Label selectedNameLabel;
        private readonly Vector3Field localPositionField;
        private readonly Vector3Field localRotationField;
        private readonly Vector3Field localScaleField;
        private readonly Label worldTransformLabel;
        private readonly HashSet<string> expandedPaths = new HashSet<string>();
        private bool syncingFields;

        public SkillObjectHierarchyPanel(SkillPreviewSceneContext context, Action<Transform> focusRequested)
        {
            this.context = context;
            this.focusRequested = focusRequested;

            root = new Foldout
            {
                text = "Object Hierarchy",
                value = true
            };
            root.AddToClassList("skill-panel");

            hierarchyView = new ScrollView();
            hierarchyView.AddToClassList("skill-hierarchy");
            root.Add(hierarchyView);

            selectedNameLabel = new Label("No preview object selected.");
            selectedNameLabel.AddToClassList("skill-section-label");
            root.Add(selectedNameLabel);

            localPositionField = new Vector3Field("Local Position");
            localRotationField = new Vector3Field("Local Rotation");
            localScaleField = new Vector3Field("Local Scale");
            worldTransformLabel = new Label();
            worldTransformLabel.AddToClassList("skill-world-label");

            localPositionField.RegisterValueChangedCallback(evt => ApplyPosition(evt.newValue));
            localRotationField.RegisterValueChangedCallback(evt => ApplyRotation(evt.newValue));
            localScaleField.RegisterValueChangedCallback(evt => ApplyScale(evt.newValue));

            root.Add(localPositionField);
            root.Add(localRotationField);
            root.Add(localScaleField);
            root.Add(worldTransformLabel);
        }

        public VisualElement Root => root;

        public void Refresh()
        {
            hierarchyView.Clear();

            if (context.PreviewRoot == null)
            {
                hierarchyView.Add(new Label("No object loaded."));
                SyncTransformFields();
                return;
            }

            expandedPaths.Add(string.Empty);
            AddTransformRow(context.PreviewRoot.transform, 0);
            SyncTransformFields();
        }

        public void SyncTransformFields()
        {
            syncingFields = true;

            Transform selected = context.SelectedTransform;
            bool hasSelection = selected != null;
            localPositionField.SetEnabled(hasSelection);
            localRotationField.SetEnabled(hasSelection);
            localScaleField.SetEnabled(hasSelection);

            if (!hasSelection)
            {
                selectedNameLabel.text = "No preview object selected.";
                localPositionField.SetValueWithoutNotify(Vector3.zero);
                localRotationField.SetValueWithoutNotify(Vector3.zero);
                localScaleField.SetValueWithoutNotify(Vector3.one);
                worldTransformLabel.text = string.Empty;
                syncingFields = false;
                return;
            }

            selectedNameLabel.text = selected.name;
            localPositionField.SetValueWithoutNotify(SkillTransformInspectorUtility.GetLocalPosition(selected));
            localRotationField.SetValueWithoutNotify(SkillTransformInspectorUtility.GetLocalEulerAngles(selected));
            localScaleField.SetValueWithoutNotify(SkillTransformInspectorUtility.GetLocalScale(selected));
            worldTransformLabel.text = $"World Position {selected.position:F3} | World Rotation {selected.rotation.eulerAngles:F3} | Lossy Scale {selected.lossyScale:F3}";

            syncingFields = false;
        }

        private void AddTransformRow(Transform transform, int depth)
        {
            string path = GetPath(transform);
            bool hasChildren = transform.childCount > 0;
            bool expanded = IsExpanded(path);

            VisualElement row = new VisualElement();
            row.AddToClassList("skill-hierarchy-row");
            row.style.paddingLeft = 8f + depth * 14f;

            if (transform == context.SelectedTransform)
                row.AddToClassList("skill-hierarchy-row-selected");

            Label foldoutLabel = new Label(hasChildren ? expanded ? "v" : ">" : string.Empty);
            foldoutLabel.AddToClassList("skill-hierarchy-foldout");
            row.Add(foldoutLabel);

            Label nameLabel = new Label(transform.name);
            nameLabel.AddToClassList("skill-hierarchy-name");
            row.Add(nameLabel);

            if (hasChildren)
            {
                foldoutLabel.RegisterCallback<ClickEvent>(evt =>
                {
                    ToggleExpanded(path);
                    Refresh();
                    evt.StopPropagation();
                });
            }

            row.RegisterCallback<ClickEvent>(evt =>
            {
                context.SelectTransform(transform);
                Refresh();

                if (evt.clickCount >= 2)
                    focusRequested?.Invoke(transform);
            });

            hierarchyView.Add(row);

            if (!hasChildren || !expanded)
                return;

            for (int i = 0; i < transform.childCount; i++)
            {
                AddTransformRow(transform.GetChild(i), depth + 1);
            }
        }

        private bool IsExpanded(string path)
        {
            return expandedPaths.Contains(path);
        }

        private void ToggleExpanded(string path)
        {
            if (!expandedPaths.Add(path))
                expandedPaths.Remove(path);
        }

        private string GetPath(Transform transform)
        {
            if (context.PreviewRoot == null || transform == context.PreviewRoot.transform)
                return string.Empty;

            return AnimationUtility.CalculateTransformPath(transform, context.PreviewRoot.transform);
        }

        private void ApplyPosition(Vector3 value)
        {
            if (syncingFields || context.SelectedTransform == null)
                return;

            SkillTransformInspectorUtility.SetLocalPosition(context.SelectedTransform, value);
            SyncTransformFields();
        }

        private void ApplyRotation(Vector3 value)
        {
            if (syncingFields || context.SelectedTransform == null)
                return;

            SkillTransformInspectorUtility.SetLocalEulerAngles(context.SelectedTransform, value);
            SyncTransformFields();
        }

        private void ApplyScale(Vector3 value)
        {
            if (syncingFields || context.SelectedTransform == null)
                return;

            SkillTransformInspectorUtility.SetLocalScale(context.SelectedTransform, value);
            SyncTransformFields();
        }
    }
}
