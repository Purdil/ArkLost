using System;
using UnityEditor;
using UnityEngine;

namespace SkillEditor.Editor
{
    public sealed class SkillTransformTool
    {
        private SkillTransformToolMode mode;

        public event Action ModeChanged;
        public event Action TransformChanged;

        public SkillTransformToolMode Mode => mode;

        public void SetMode(SkillTransformToolMode nextMode)
        {
            if (mode == nextMode)
                return;

            mode = nextMode;
            ModeChanged?.Invoke();
        }

        public bool HandleKey(KeyCode keyCode)
        {
            if (keyCode == KeyCode.W)
            {
                SetMode(SkillTransformToolMode.Move);
                return true;
            }

            if (keyCode == KeyCode.E)
            {
                SetMode(SkillTransformToolMode.Rotate);
                return true;
            }

            if (keyCode == KeyCode.R)
            {
                SetMode(SkillTransformToolMode.Scale);
                return true;
            }

            return false;
        }

        public void NotifyTransformChanged()
        {
            TransformChanged?.Invoke();
        }
    }
}
