using System;
using System.Collections.Generic;
using _Scripts.CoreSystem.Effects;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using Object = UnityEngine.Object;

namespace _Scripts.Editor.AnimationVfxEventEditor
{
    public class AnimationVfxPreviewRenderer
    {
        private readonly List<GameObject> eventVfxPreviewInstances = new List<GameObject>();
        private PreviewRenderUtility previewUtility;
        private GameObject sourceCharacter;
        private GameObject previewCharacter;
        private Animator sourceAnimator;
        private Animator previewAnimator;
        private AnimationClip selectedClip;
        private Transform selectedVfxSource;
        private Vector2 orbit = new Vector2(35f, -18f);
        private Vector3 focusOffset;
        private float zoom = 1f;
        private bool isDraggingSelectedVfx;
        private Vector3 dragStartLocalPosition;
        private Vector3 dragStartPreviewHit;
        private Transform dragPreviewParent;

        public Action<Transform> SelectedVfxTransformChanged { get; set; }

        public void SetCharacter(GameObject character)
        {
            if (sourceCharacter == character)
                return;

            sourceCharacter = character;
            sourceAnimator = sourceCharacter != null ? sourceCharacter.GetComponentInChildren<Animator>(true) : null;
            RebuildPreviewCharacter();
        }

        public void SetClip(AnimationClip clip)
        {
            selectedClip = clip;
        }

        public void SetSelectedVfx(Transform vfxTransform)
        {
            selectedVfxSource = vfxTransform;
        }

        public void ForceRebuild()
        {
            RebuildPreviewCharacter();
        }

        public void ZoomBy(float wheelDelta)
        {
            zoom = Mathf.Clamp(zoom + wheelDelta * 0.08f, 0.25f, 3.5f);
        }

        public void FrameSelected()
        {
            FrameSelection();
        }

        public void OrbitCamera(Vector2 delta)
        {
            OrbitBy(delta);
        }

        public void LookCamera(Vector2 delta)
        {
            orbit.x += delta.x * 0.28f;
            orbit.y = Mathf.Clamp(orbit.y + delta.y * 0.28f, -80f, 80f);
        }

        public void PanCamera(Vector2 delta)
        {
            PanBy(delta);
        }

        public void Draw(Rect rect, float time, AnimationEvent[] animationEvents, bool showSelectedVfx)
        {
            if (rect.width <= 1f || rect.height <= 1f)
                return;

            if (sourceCharacter == null)
            {
                DrawEmpty(rect, "Select a scene character, animator child, or child VFX.");
                return;
            }

            if (sourceAnimator == null)
            {
                DrawEmpty(rect, "Selected character does not contain an Animator.");
                return;
            }

            EnsurePreviewUtility();

            if (previewCharacter == null)
                RebuildPreviewCharacter();

            if (previewCharacter == null)
            {
                DrawEmpty(rect, "Preview character could not be created.");
                return;
            }

            HandleCameraInput(rect);
            ClearEventVfxPreviewInstances();
            SampleCharacter(time);
            StopAllParticles();
            SimulateEventVfx(time, animationEvents);
            SampleCharacter(time);
            StopAllParticles();

            if (showSelectedVfx && selectedVfxSource != null)
                SimulateSourceVfx(selectedVfxSource, time);

            RenderPreview(rect);
            DrawSelectedVfxHandle(rect);
        }

        public void Dispose()
        {
            ClearEventVfxPreviewInstances();
            DestroyPreviewCharacter();

            if (previewUtility != null)
            {
                previewUtility.Cleanup();
                previewUtility = null;
            }
        }

        private void EnsurePreviewUtility()
        {
            if (previewUtility != null)
                return;

            previewUtility = new PreviewRenderUtility(true);
            previewUtility.cameraFieldOfView = 30f;
            previewUtility.ambientColor = new Color(0.42f, 0.45f, 0.5f);
            previewUtility.lights[0].intensity = 1.35f;
            previewUtility.lights[0].transform.rotation = Quaternion.Euler(40f, 40f, 0f);
            previewUtility.lights[1].intensity = 0.75f;
        }

        private void RebuildPreviewCharacter()
        {
            EnsurePreviewUtility();
            ClearEventVfxPreviewInstances();
            DestroyPreviewCharacter();

            if (sourceCharacter == null)
                return;

            previewCharacter = Object.Instantiate(sourceCharacter);
            previewCharacter.name = sourceCharacter.name;
            previewCharacter.transform.SetPositionAndRotation(sourceCharacter.transform.position, sourceCharacter.transform.rotation);
            previewCharacter.transform.localScale = sourceCharacter.transform.lossyScale;
            SetHideFlags(previewCharacter);
            BindPreviewAnimator();
            RestoreStationaryPreviewRoot();
            previewUtility.AddSingleGO(previewCharacter);
        }

        private void SampleCharacter(float time)
        {
            if (selectedClip == null || previewCharacter == null || previewAnimator == null)
                return;

            float sampleTime = Mathf.Clamp(time, 0f, selectedClip.length);
            selectedClip.SampleAnimation(previewAnimator.gameObject, sampleTime);
            SampleAnimatorWithPlayable(sampleTime);
            RestoreStationaryPreviewRoot();
        }

        private void SampleAnimatorWithPlayable(float time)
        {
            if (previewAnimator == null || selectedClip == null)
                return;

            PlayableGraph graph = PlayableGraph.Create("Animation VFX Event Editor Preview");
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);

            AnimationClipPlayable clipPlayable = AnimationClipPlayable.Create(graph, selectedClip);
            clipPlayable.SetApplyFootIK(false);
            clipPlayable.SetTime(time);

            AnimationPlayableOutput output = AnimationPlayableOutput.Create(graph, "Preview", previewAnimator);
            output.SetSourcePlayable(clipPlayable);

            graph.Play();
            graph.Evaluate(0f);
            graph.Destroy();
        }

        private void RestoreStationaryPreviewRoot()
        {
            if (sourceCharacter == null || previewCharacter == null)
                return;

            previewCharacter.transform.SetPositionAndRotation(sourceCharacter.transform.position, sourceCharacter.transform.rotation);
            previewCharacter.transform.localScale = sourceCharacter.transform.lossyScale;

            if (sourceAnimator == null || previewAnimator == null)
                return;

            if (sourceAnimator.transform == sourceCharacter.transform)
                return;

            previewAnimator.transform.localPosition = sourceAnimator.transform.localPosition;
            previewAnimator.transform.localRotation = sourceAnimator.transform.localRotation;
            previewAnimator.transform.localScale = sourceAnimator.transform.localScale;
        }

        private void StopAllParticles()
        {
            ParticleSystem[] particles = previewCharacter.GetComponentsInChildren<ParticleSystem>(true);
            foreach (ParticleSystem particle in particles)
            {
                particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        private void SimulateEventVfx(float time, AnimationEvent[] animationEvents)
        {
            if (animationEvents == null)
                return;

            foreach (AnimationEvent animationEvent in animationEvents)
            {
                if (animationEvent == null || animationEvent.functionName != "ExecuteVFX")
                    continue;

                Object vfxName = animationEvent.objectReferenceParameter;
                float age = time - animationEvent.time;

                if (vfxName == null || age < 0f)
                    continue;

                Component sourceVfx = FindSourcePlayableByNameAsset(vfxName);
                if (sourceVfx == null)
                    continue;

                float duration = GetVfxDuration(sourceVfx);
                if (age > duration)
                    continue;

                if (TryGetEventPose(sourceVfx.transform, animationEvent.time, out Vector3 position, out Quaternion rotation, out Vector3 scale))
                    CreateEventVfxPreview(sourceVfx.transform, position, rotation, scale, age);
            }
        }

        private bool TryGetEventPose(Transform sourceTransform, float eventTime, out Vector3 position, out Quaternion rotation, out Vector3 scale)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;
            scale = Vector3.one;

            if (sourceTransform == null || selectedClip == null)
                return false;

            SampleCharacter(eventTime);

            Transform previewTransform = FindPreviewTransform(sourceTransform);
            if (previewTransform == null)
                return false;

            previewTransform.localPosition = sourceTransform.localPosition;
            previewTransform.localRotation = sourceTransform.localRotation;
            previewTransform.localScale = sourceTransform.localScale;

            position = previewTransform.position;
            rotation = previewTransform.rotation;
            scale = previewTransform.lossyScale;
            return true;
        }

        private void CreateEventVfxPreview(Transform sourceTransform, Vector3 position, Quaternion rotation, Vector3 scale, float age)
        {
            if (sourceTransform == null)
                return;

            GameObject instance = Object.Instantiate(sourceTransform.gameObject);
            instance.name = $"{sourceTransform.name} Event Preview";
            SetHideFlags(instance);
            ActivateAllChildren(instance);
            instance.transform.SetPositionAndRotation(position, rotation);
            instance.transform.localScale = scale;

            previewUtility.AddSingleGO(instance);
            eventVfxPreviewInstances.Add(instance);
            SimulateParticles(instance, age);
        }

        private void SimulateParticles(GameObject root, float age)
        {
            ParticleSystem[] particles = root.GetComponentsInChildren<ParticleSystem>(true);
            foreach (ParticleSystem particle in particles)
            {
                particle.gameObject.SetActive(true);
                particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                particle.Simulate(Mathf.Max(0.02f, age), true, true, true);
            }
        }

        private void ActivateAllChildren(GameObject root)
        {
            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            foreach (Transform child in transforms)
            {
                child.gameObject.SetActive(true);
            }
        }

        private void SimulateSourceVfx(Transform sourceTransform, float age)
        {
            Transform previewTransform = FindPreviewTransform(sourceTransform);
            if (previewTransform == null)
                return;

            previewTransform.localPosition = sourceTransform.localPosition;
            previewTransform.localRotation = sourceTransform.localRotation;
            previewTransform.localScale = sourceTransform.localScale;
            ActivatePreviewPath(previewTransform);

            ParticleSystem[] particles = previewTransform.GetComponentsInChildren<ParticleSystem>(true);
            foreach (ParticleSystem particle in particles)
            {
                particle.gameObject.SetActive(true);
                particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                particle.Simulate(Mathf.Max(0.02f, age), true, true, true);
            }
        }

        private void ActivatePreviewPath(Transform previewTransform)
        {
            Transform current = previewTransform;
            while (current != null)
            {
                current.gameObject.SetActive(true);

                if (current == previewCharacter.transform)
                    break;

                current = current.parent;
            }
        }

        private Component FindSourcePlayableByNameAsset(Object nameAsset)
        {
            MonoBehaviour[] components = sourceCharacter.GetComponentsInChildren<MonoBehaviour>(true);
            foreach (MonoBehaviour component in components)
            {
                if (component is IPlayableVFX playableVfx && playableVfx.VfxName == nameAsset)
                    return component;
            }

            return null;
        }

        private float GetVfxDuration(Component sourceVfx)
        {
            if (sourceVfx is IPlayableVFX playableVfx && playableVfx.VfxDuration > 0f)
                return playableVfx.VfxDuration;

            return 2f;
        }

        private Transform FindPreviewTransform(Transform sourceTransform)
        {
            if (sourceTransform == null || sourceCharacter == null || previewCharacter == null)
                return null;

            string path = AnimationUtility.CalculateTransformPath(sourceTransform, sourceCharacter.transform);
            if (string.IsNullOrEmpty(path))
                return previewCharacter.transform;

            return previewCharacter.transform.Find(path);
        }

        private void RenderPreview(Rect rect)
        {
            Bounds bounds = CalculateBounds();
            Camera camera = previewUtility.camera;
            camera.clearFlags = CameraClearFlags.Color;
            camera.backgroundColor = new Color(0.105f, 0.112f, 0.13f);
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 500f;

            float radius = Mathf.Max(0.75f, bounds.extents.magnitude);
            float distance = Mathf.Max(2.5f, radius * 2.8f * zoom);
            Quaternion rotation = Quaternion.Euler(orbit.y, orbit.x, 0f);
            Vector3 focus = GetDefaultFocus(bounds) + focusOffset;

            camera.transform.position = focus + rotation * (Vector3.back * distance);
            camera.transform.LookAt(focus);

            previewUtility.BeginPreview(rect, GUIStyle.none);
            camera.Render();
            previewUtility.EndAndDrawPreview(rect);
        }

        private void DrawSelectedVfxHandle(Rect rect)
        {
            if (selectedVfxSource == null || previewCharacter == null || previewUtility == null)
                return;

            Transform previewTransform = FindPreviewTransform(selectedVfxSource);
            if (previewTransform == null)
                return;

            Camera camera = previewUtility.camera;
            Vector3 viewportPosition = camera.WorldToViewportPoint(previewTransform.position);
            if (viewportPosition.z <= 0f)
                return;

            Vector2 handlePosition = new Vector2(
                rect.x + viewportPosition.x * rect.width,
                rect.y + (1f - viewportPosition.y) * rect.height);

            Rect hitRect = new Rect(handlePosition.x - 12f, handlePosition.y - 12f, 24f, 24f);
            Event currentEvent = Event.current;

            EditorGUIUtility.AddCursorRect(hitRect, MouseCursor.MoveArrow);

            if (currentEvent.type == EventType.MouseDown && currentEvent.button == 0 && hitRect.Contains(currentEvent.mousePosition))
            {
                BeginSelectedVfxDrag(rect, previewTransform, currentEvent.mousePosition);
                currentEvent.Use();
            }
            else if (currentEvent.type == EventType.MouseDrag && isDraggingSelectedVfx)
            {
                DragSelectedVfx(rect, currentEvent.mousePosition);
                currentEvent.Use();
            }
            else if (currentEvent.type == EventType.MouseUp && isDraggingSelectedVfx)
            {
                EndSelectedVfxDrag();
                currentEvent.Use();
            }

            Handles.BeginGUI();
            Color previousColor = GUI.color;
            GUI.color = new Color(0.2f, 0.95f, 0.58f, 1f);
            GUI.DrawTexture(new Rect(handlePosition.x - 5f, handlePosition.y - 5f, 10f, 10f), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(handlePosition.x + 8f, handlePosition.y - 11f, 120f, 22f), selectedVfxSource.name, EditorStyles.whiteMiniLabel);
            GUI.color = previousColor;
            Handles.EndGUI();
        }

        private void BeginSelectedVfxDrag(Rect rect, Transform previewTransform, Vector2 mousePosition)
        {
            if (selectedVfxSource == null)
                return;

            isDraggingSelectedVfx = true;
            dragStartLocalPosition = selectedVfxSource.localPosition;
            dragPreviewParent = previewTransform.parent;

            if (!TryGetPreviewPlaneHit(rect, mousePosition, previewTransform.position, out dragStartPreviewHit))
                dragStartPreviewHit = previewTransform.position;

            Undo.RecordObject(selectedVfxSource, "Move VFX In Preview");
        }

        private void DragSelectedVfx(Rect rect, Vector2 mousePosition)
        {
            if (selectedVfxSource == null || dragPreviewParent == null)
                return;

            Transform previewTransform = FindPreviewTransform(selectedVfxSource);
            if (previewTransform == null)
                return;

            if (!TryGetPreviewPlaneHit(rect, mousePosition, previewTransform.position, out Vector3 currentHit))
                return;

            Vector3 worldDelta = currentHit - dragStartPreviewHit;
            Vector3 parentLocalDelta = dragPreviewParent.InverseTransformVector(worldDelta);
            Vector3 newLocalPosition = dragStartLocalPosition + parentLocalDelta;

            selectedVfxSource.localPosition = newLocalPosition;
            previewTransform.localPosition = newLocalPosition;
            EditorUtility.SetDirty(selectedVfxSource);
            PrefabUtility.RecordPrefabInstancePropertyModifications(selectedVfxSource);
            SelectedVfxTransformChanged?.Invoke(selectedVfxSource);
        }

        private void EndSelectedVfxDrag()
        {
            isDraggingSelectedVfx = false;
            dragPreviewParent = null;
        }

        private bool TryGetPreviewPlaneHit(Rect rect, Vector2 mousePosition, Vector3 planePoint, out Vector3 hit)
        {
            hit = Vector3.zero;

            if (previewUtility == null)
                return false;

            Camera camera = previewUtility.camera;
            float viewportX = Mathf.Clamp01((mousePosition.x - rect.x) / Mathf.Max(1f, rect.width));
            float viewportY = Mathf.Clamp01(1f - (mousePosition.y - rect.y) / Mathf.Max(1f, rect.height));
            Ray ray = camera.ViewportPointToRay(new Vector3(viewportX, viewportY, 0f));
            Plane dragPlane = new Plane(camera.transform.forward, planePoint);

            if (!dragPlane.Raycast(ray, out float distance))
                return false;

            hit = ray.GetPoint(distance);
            return true;
        }

        private Bounds CalculateBounds()
        {
            Bounds bounds = new Bounds(Vector3.up, Vector3.one);
            bool hasRenderer = false;

            if (previewCharacter == null)
                return bounds;

            Renderer[] renderers = previewCharacter.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer renderer in renderers)
            {
                if (renderer == null)
                    continue;

                if (!hasRenderer)
                {
                    bounds = renderer.bounds;
                    hasRenderer = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return bounds;
        }

        private Vector3 GetDefaultFocus(Bounds bounds)
        {
            float radius = Mathf.Max(0.75f, bounds.extents.magnitude);
            return bounds.center + Vector3.up * radius * 0.05f;
        }

        private void HandleCameraInput(Rect rect)
        {
            Event currentEvent = Event.current;
            if (currentEvent == null || !rect.Contains(currentEvent.mousePosition))
                return;

            if (isDraggingSelectedVfx)
                return;

            if (currentEvent.type == EventType.KeyDown && currentEvent.keyCode == KeyCode.F)
            {
                FrameSelection();
                currentEvent.Use();
                return;
            }

        }

        private void OrbitBy(Vector2 delta)
        {
            orbit.x -= delta.x * 0.35f;
            orbit.y = Mathf.Clamp(orbit.y + delta.y * 0.35f, -80f, 80f);
        }

        private void PanBy(Vector2 delta)
        {
            if (previewUtility == null)
                return;

            Bounds bounds = CalculateBounds();
            float radius = Mathf.Max(0.75f, bounds.extents.magnitude);
            float distance = Mathf.Max(2.5f, radius * 2.8f * zoom);
            float panScale = distance * 0.002f;
            Quaternion rotation = Quaternion.Euler(orbit.y, orbit.x, 0f);
            Vector3 right = rotation * Vector3.right;
            Vector3 up = rotation * Vector3.up;

            focusOffset += (-right * delta.x + up * delta.y) * panScale;
        }

        private void FrameSelection()
        {
            Bounds bounds = CalculateBounds();
            Vector3 defaultFocus = GetDefaultFocus(bounds);

            if (selectedVfxSource != null)
            {
                Transform previewTransform = FindPreviewTransform(selectedVfxSource);
                if (previewTransform != null)
                {
                    focusOffset = previewTransform.position - defaultFocus;
                    zoom = 0.75f;
                    return;
                }
            }

            focusOffset = Vector3.zero;
            zoom = 1f;
        }

        private void BindPreviewAnimator()
        {
            previewAnimator = null;

            if (sourceAnimator == null || previewCharacter == null)
                return;

            string animatorPath = AnimationUtility.CalculateTransformPath(sourceAnimator.transform, sourceCharacter.transform);
            Transform previewAnimatorTransform = string.IsNullOrEmpty(animatorPath)
                ? previewCharacter.transform
                : previewCharacter.transform.Find(animatorPath);

            previewAnimator = previewAnimatorTransform != null
                ? previewAnimatorTransform.GetComponent<Animator>()
                : previewCharacter.GetComponentInChildren<Animator>(true);

            Animator[] animators = previewCharacter.GetComponentsInChildren<Animator>(true);
            foreach (Animator animator in animators)
            {
                animator.enabled = animator == previewAnimator;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }
        }

        private void SetHideFlags(GameObject root)
        {
            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            foreach (Transform child in transforms)
            {
                child.gameObject.hideFlags = HideFlags.HideAndDontSave;
            }
        }

        private void DestroyPreviewCharacter()
        {
            if (previewCharacter == null)
                return;

            Object.DestroyImmediate(previewCharacter);
            previewCharacter = null;
        }

        private void ClearEventVfxPreviewInstances()
        {
            foreach (GameObject instance in eventVfxPreviewInstances)
            {
                if (instance != null)
                    Object.DestroyImmediate(instance);
            }

            eventVfxPreviewInstances.Clear();
        }

        private void DrawEmpty(Rect rect, string message)
        {
            EditorGUI.DrawRect(rect, new Color(0.105f, 0.112f, 0.13f));
            GUIStyle style = new GUIStyle(EditorStyles.centeredGreyMiniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 13
            };

            GUI.Label(rect, message, style);
        }
    }
}
