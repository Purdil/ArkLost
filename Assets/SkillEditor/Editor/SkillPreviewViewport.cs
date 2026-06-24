using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace SkillEditor.Editor
{
    public sealed class SkillPreviewViewport
    {
        private const int RenderTextureWidth = 1920;
        private const int RenderTextureHeight = 1080;

        private readonly SkillPreviewSceneContext context;
        private readonly SkillTransformTool transformTool;
        private readonly Foldout root;
        private readonly IMGUIContainer container;
        private RenderTexture renderTexture;
        private Vector2 lastMousePosition;
        private bool draggingTransformHandle;
        private Vector3 dragStartLocalPosition;
        private Vector3 dragStartWorldHit;
        private Transform dragStartParent;
        private int dragMode;
        private float yaw = 35f;
        private float pitch = 18f;
        private float distance = 8f;
        private Vector3 focus = Vector3.up;

        public SkillPreviewViewport(SkillPreviewSceneContext context, SkillTransformTool transformTool)
        {
            this.context = context;
            this.transformTool = transformTool;
            root = new Foldout
            {
                text = "Preview Viewport",
                value = true
            };
            root.AddToClassList("skill-panel");
            root.AddToClassList("skill-preview-foldout");
            container = new IMGUIContainer(Draw)
            {
                focusable = true
            };
            container.AddToClassList("skill-preview-viewport");
            root.Add(container);
            EnsureRenderTexture();
        }

        public VisualElement Root => root;

        public void Dispose()
        {
            if (renderTexture != null)
            {
                UnityEngine.Object.DestroyImmediate(renderTexture);
                renderTexture = null;
            }
        }

        public void Repaint()
        {
            container.MarkDirtyRepaint();
        }

        public void FocusSelected()
        {
            if (context.SelectedTransform != null)
                Focus(context.SelectedTransform);
            else if (context.PreviewRoot != null)
                Focus(context.PreviewRoot.transform);
        }

        public void Focus(Transform target)
        {
            if (target == null)
                return;

            Bounds bounds = context.CalculateBounds(target);
            focus = bounds.center;
            distance = Mathf.Clamp(Mathf.Max(2f, bounds.extents.magnitude * 2.6f), 1.5f, 100f);
            Repaint();
        }

        private void Draw()
        {
            Rect rect = new Rect(0f, 0f, Mathf.Max(1f, container.contentRect.width), Mathf.Max(1f, container.contentRect.height));
            Rect renderRect = GetRenderRect(rect);
            HandleInput(renderRect);
            Render(rect, renderRect);
            DrawOverlay(rect);
            DrawTransformOverlay(renderRect);
        }

        private void Render(Rect fullRect, Rect renderRect)
        {
            Camera camera = context.PreviewCamera;
            if (camera == null)
            {
                DrawMessage(fullRect, "Preview camera is not ready.");
                return;
            }

            EnsureRenderTexture();
            UpdateCamera(camera, renderRect);

            RenderTexture previous = RenderTexture.active;
            RenderTexture previousTarget = camera.targetTexture;
            Scene previousScene = camera.scene;
            camera.scene = context.PreviewCamera.scene;
            camera.targetTexture = renderTexture;
            RenderTexture.active = renderTexture;
            GL.Clear(true, true, camera.backgroundColor);
            camera.Render();
            camera.targetTexture = previousTarget;
            camera.scene = previousScene;
            RenderTexture.active = previous;

            EditorGUI.DrawRect(fullRect, new Color(0.08f, 0.085f, 0.095f));
            GUI.DrawTexture(renderRect, renderTexture, ScaleMode.StretchToFill, false);

            if (context.PreviewRoot == null)
                DrawMessage(fullRect, "Select a scene GameObject to preview.");
        }

        private void DrawOverlay(Rect rect)
        {
            Handles.BeginGUI();
            GUIStyle labelStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                normal = { textColor = new Color(0.82f, 0.86f, 0.9f) }
            };

            string modeText = $"Tool: {transformTool.Mode} (W/E/R)  Camera: Alt+LMB Orbit, MMB Pan, RMB Look, Wheel Zoom, F Focus";
            GUI.Label(new Rect(rect.x + 10f, rect.y + 8f, rect.width - 20f, 20f), modeText, labelStyle);
            Handles.EndGUI();
        }

        private void DrawMessage(Rect rect, string message)
        {
            EditorGUI.DrawRect(rect, new Color(0.12f, 0.13f, 0.15f));
            GUIStyle style = new GUIStyle(EditorStyles.centeredGreyMiniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 13
            };
            GUI.Label(rect, message, style);
        }

        private void HandleInput(Rect rect)
        {
            Event current = Event.current;
            if (current == null || !rect.Contains(current.mousePosition))
                return;

            if (draggingTransformHandle)
                return;

            if (current.type == EventType.KeyDown)
            {
                if (transformTool.HandleKey(current.keyCode))
                {
                    current.Use();
                    Repaint();
                    return;
                }

                if (current.keyCode == KeyCode.F)
                {
                    FocusSelected();
                    current.Use();
                    return;
                }
            }

            if (current.type == EventType.ScrollWheel)
            {
                distance = Mathf.Clamp(distance + current.delta.y * Mathf.Max(0.08f, distance * 0.06f), 0.5f, 250f);
                current.Use();
                Repaint();
                return;
            }

            if (current.type == EventType.MouseDown)
            {
                int mode = GetDragMode(current);
                if (mode != 0)
                {
                    dragMode = mode;
                    lastMousePosition = current.mousePosition;
                    container.Focus();
                    current.Use();
                }

                return;
            }

            if (current.type == EventType.MouseDrag && dragMode != 0)
            {
                Vector2 delta = current.mousePosition - lastMousePosition;
                lastMousePosition = current.mousePosition;
                ApplyCameraDrag(delta);
                current.Use();
                Repaint();
                return;
            }

            if (current.type == EventType.MouseUp && dragMode != 0)
            {
                dragMode = 0;
                current.Use();
            }
        }

        private int GetDragMode(Event current)
        {
            if (current.alt && current.button == 0)
                return 1;

            if (current.button == 2)
                return 2;

            if (current.button == 1)
                return 3;

            return 0;
        }

        private void ApplyCameraDrag(Vector2 delta)
        {
            if (dragMode == 1 || dragMode == 3)
            {
                yaw += delta.x * 0.25f;
                pitch = Mathf.Clamp(pitch + delta.y * 0.25f, -80f, 80f);
                return;
            }

            if (dragMode != 2)
                return;

            Camera camera = context.PreviewCamera;
            if (camera == null)
                return;

            float panScale = Mathf.Max(0.002f, distance * 0.0018f);
            focus += (-camera.transform.right * delta.x + camera.transform.up * delta.y) * panScale;
        }

        private void UpdateCamera(Camera camera, Rect rect)
        {
            camera.aspect = rect.width / Mathf.Max(1f, rect.height);
            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            camera.transform.position = focus + rotation * (Vector3.back * distance);
            camera.transform.LookAt(focus);
        }

        private void EnsureRenderTexture()
        {
            if (renderTexture != null)
                return;

            renderTexture = new RenderTexture(RenderTextureWidth, RenderTextureHeight, 24, RenderTextureFormat.ARGB32)
            {
                name = "SkillEditorPreviewRenderTexture",
                hideFlags = HideFlags.DontSave
            };
            renderTexture.Create();
        }

        private Rect GetRenderRect(Rect containerRect)
        {
            float textureAspect = RenderTextureWidth / (float)RenderTextureHeight;
            float containerAspect = containerRect.width / Mathf.Max(1f, containerRect.height);

            if (containerAspect > textureAspect)
            {
                float width = containerRect.height * textureAspect;
                float x = containerRect.x + (containerRect.width - width) * 0.5f;
                return new Rect(x, containerRect.y, width, containerRect.height);
            }

            float height = containerRect.width / textureAspect;
            float y = containerRect.y + (containerRect.height - height) * 0.5f;
            return new Rect(containerRect.x, y, containerRect.width, height);
        }

        private void DrawTransformOverlay(Rect rect)
        {
            Transform target = context.SelectedTransform;
            Camera camera = context.PreviewCamera;
            if (target == null || camera == null)
                return;

            Vector3 viewportPosition = camera.WorldToViewportPoint(target.position);
            if (viewportPosition.z <= 0f)
                return;

            Vector2 handlePosition = new Vector2(
                rect.x + viewportPosition.x * rect.width,
                rect.y + (1f - viewportPosition.y) * rect.height);

            Rect hitRect = new Rect(handlePosition.x - 12f, handlePosition.y - 12f, 24f, 24f);
            Event current = Event.current;

            EditorGUIUtility.AddCursorRect(hitRect, MouseCursor.MoveArrow);

            if (current.type == EventType.MouseDown && current.button == 0 && hitRect.Contains(current.mousePosition))
            {
                BeginTransformDrag(rect, target, current.mousePosition);
                current.Use();
            }
            else if (current.type == EventType.MouseDrag && draggingTransformHandle)
            {
                DragTransform(rect, target, current.mousePosition);
                current.Use();
            }
            else if (current.type == EventType.MouseUp && draggingTransformHandle)
            {
                EndTransformDrag();
                current.Use();
            }

            Handles.BeginGUI();
            Color previousColor = GUI.color;
            GUI.color = GetHandleColor();
            GUI.DrawTexture(new Rect(handlePosition.x - 5f, handlePosition.y - 5f, 10f, 10f), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(handlePosition.x + 8f, handlePosition.y - 11f, 220f, 22f), $"{target.name} ({transformTool.Mode})", EditorStyles.whiteMiniLabel);
            GUI.color = previousColor;
            Handles.EndGUI();
        }

        private void BeginTransformDrag(Rect rect, Transform target, Vector2 mousePosition)
        {
            if (transformTool.Mode != SkillTransformToolMode.Move)
                return;

            draggingTransformHandle = true;
            dragStartLocalPosition = target.localPosition;
            dragStartParent = target.parent;

            if (!TryGetPreviewPlaneHit(rect, mousePosition, target.position, out dragStartWorldHit))
                dragStartWorldHit = target.position;

            Undo.RecordObject(target, "Move Preview Transform");
        }

        private void DragTransform(Rect rect, Transform target, Vector2 mousePosition)
        {
            if (!draggingTransformHandle)
                return;

            if (!TryGetPreviewPlaneHit(rect, mousePosition, target.position, out Vector3 currentHit))
                return;

            Vector3 worldDelta = currentHit - dragStartWorldHit;
            Vector3 localDelta = dragStartParent != null
                ? dragStartParent.InverseTransformVector(worldDelta)
                : worldDelta;

            target.localPosition = dragStartLocalPosition + localDelta;
            transformTool.NotifyTransformChanged();
        }

        private void EndTransformDrag()
        {
            draggingTransformHandle = false;
            dragStartParent = null;
        }

        private bool TryGetPreviewPlaneHit(Rect rect, Vector2 mousePosition, Vector3 planePoint, out Vector3 hit)
        {
            hit = Vector3.zero;
            Camera camera = context.PreviewCamera;
            if (camera == null)
                return false;

            float viewportX = Mathf.Clamp01((mousePosition.x - rect.x) / Mathf.Max(1f, rect.width));
            float viewportY = Mathf.Clamp01(1f - (mousePosition.y - rect.y) / Mathf.Max(1f, rect.height));
            Ray ray = camera.ViewportPointToRay(new Vector3(viewportX, viewportY, 0f));
            Plane dragPlane = new Plane(camera.transform.forward, planePoint);

            if (!dragPlane.Raycast(ray, out float distanceToPlane))
                return false;

            hit = ray.GetPoint(distanceToPlane);
            return true;
        }

        private Color GetHandleColor()
        {
            if (transformTool.Mode == SkillTransformToolMode.Rotate)
                return new Color(1f, 0.72f, 0.25f, 1f);

            if (transformTool.Mode == SkillTransformToolMode.Scale)
                return new Color(0.45f, 0.75f, 1f, 1f);

            return new Color(0.2f, 0.95f, 0.58f, 1f);
        }
    }
}
