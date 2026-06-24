using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace SkillEditor.Editor
{
    public sealed class SkillPlaneSettingsPanel
    {
        private readonly SkillPreviewSceneContext context;
        private readonly Foldout root;
        private readonly Toggle visibleToggle;
        private readonly Vector3Field positionField;
        private readonly FloatField sizeField;

        public SkillPlaneSettingsPanel(SkillPreviewSceneContext context)
        {
            this.context = context;

            root = new Foldout
            {
                text = "Ground Plane",
                value = true
            };
            root.AddToClassList("skill-panel");

            visibleToggle = new Toggle("Visible");
            visibleToggle.SetValueWithoutNotify(true);
            visibleToggle.RegisterValueChangedCallback(evt => context.SetPlaneVisible(evt.newValue));
            root.Add(visibleToggle);

            positionField = new Vector3Field("Position");
            positionField.SetValueWithoutNotify(Vector3.zero);
            positionField.RegisterValueChangedCallback(evt => context.SetPlanePosition(evt.newValue));
            root.Add(positionField);

            sizeField = new FloatField("Size");
            sizeField.SetValueWithoutNotify(10f);
            sizeField.RegisterValueChangedCallback(evt =>
            {
                float size = Mathf.Max(0.1f, evt.newValue);
                if (!Mathf.Approximately(size, evt.newValue))
                    sizeField.SetValueWithoutNotify(size);

                context.SetPlaneSize(size);
            });
            root.Add(sizeField);
        }

        public VisualElement Root => root;
    }
}
