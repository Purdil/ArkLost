using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace SkillEditor.Editor
{
    public sealed class SkillPreviewSceneContext
    {
        private const int PreviewLayer = 31;

        private Scene previewScene;
        private GameObject previewOrigin;
        private GameObject previewRoot;
        private GameObject sourceRoot;
        private Camera previewCamera;
        private Light directionalLight;
        private GameObject groundPlane;
        private Material groundPlaneMaterial;
        private Transform selectedTransform;
        private Animator sourceAnimator;
        private Animator previewAnimator;
        private float planeSize = 10f;

        public event Action PreviewRootChanged;
        public event Action<Transform> SelectedTransformChanged;

        public GameObject SourceRoot => sourceRoot;
        public GameObject PreviewRoot => previewRoot;
        public Transform SelectedTransform => selectedTransform;
        public Camera PreviewCamera => previewCamera;
        public GameObject GroundPlane => groundPlane;
        public Animator PreviewAnimator => previewAnimator;
        public GameObject AnimationSampleRoot => previewAnimator != null ? previewAnimator.gameObject : previewRoot;
        public float PlaneSize => planeSize;

        public void Initialize()
        {
            if (previewScene.IsValid())
                return;

            previewScene = EditorSceneManager.NewPreviewScene();
            CreateCamera();
            CreateLight();
            CreateGroundPlane();
        }

        public void Dispose()
        {
            ClearPreviewObject();
            DestroyPreviewObject(groundPlane);
            groundPlane = null;

            if (groundPlaneMaterial != null)
            {
                Object.DestroyImmediate(groundPlaneMaterial);
                groundPlaneMaterial = null;
            }

            DestroyPreviewObject(previewCamera != null ? previewCamera.gameObject : null);
            previewCamera = null;
            DestroyPreviewObject(directionalLight != null ? directionalLight.gameObject : null);
            directionalLight = null;

            if (previewScene.IsValid())
                EditorSceneManager.ClosePreviewScene(previewScene);

            previewScene = default;
        }

        public void LoadSelection(GameObject selected)
        {
            Initialize();

            if (selected == null || EditorUtility.IsPersistent(selected))
            {
                ClearPreviewObject();
                return;
            }

            if (sourceRoot == selected && previewRoot != null)
                return;

            ClearPreviewObject();
            sourceRoot = selected;

            previewOrigin = new GameObject("Skill Preview Origin");
            previewOrigin.hideFlags = HideFlags.DontSave;
            MoveToPreviewScene(previewOrigin);
            CopySourceParentFrame(selected.transform);

            previewRoot = Object.Instantiate(selected);
            previewRoot.name = selected.name;
            MoveToPreviewScene(previewRoot);
            previewRoot.transform.SetParent(previewOrigin.transform, false);
            previewRoot.transform.localPosition = selected.transform.localPosition;
            previewRoot.transform.localRotation = selected.transform.localRotation;
            previewRoot.transform.localScale = selected.transform.localScale;
            SetHideFlags(previewRoot);
            BindPreviewAnimator();

            SelectTransform(previewRoot.transform);
            PreviewRootChanged?.Invoke();
        }

        public void ClearPreviewObject()
        {
            selectedTransform = null;
            sourceRoot = null;
            sourceAnimator = null;
            previewAnimator = null;
            DestroyPreviewObject(previewRoot);
            DestroyPreviewObject(previewOrigin);
            previewRoot = null;
            previewOrigin = null;
            SelectedTransformChanged?.Invoke(null);
            PreviewRootChanged?.Invoke();
        }

        public void SelectTransform(Transform transform)
        {
            if (transform != null && previewRoot != null && transform != previewRoot.transform && !transform.IsChildOf(previewRoot.transform))
                return;

            selectedTransform = transform;
            SelectedTransformChanged?.Invoke(selectedTransform);
        }

        public Bounds CalculateBounds(Transform root)
        {
            if (root == null)
                return new Bounds(Vector3.zero, Vector3.one);

            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            bool hasBounds = false;
            Bounds bounds = new Bounds(root.position, Vector3.one);

            foreach (Renderer renderer in renderers)
            {
                if (renderer == null)
                    continue;

                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            if (hasBounds)
                return bounds;

            Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
            foreach (Collider collider in colliders)
            {
                if (collider == null)
                    continue;

                if (!hasBounds)
                {
                    bounds = collider.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(collider.bounds);
                }
            }

            return bounds;
        }

        public void SetPlaneVisible(bool visible)
        {
            if (groundPlane != null)
                groundPlane.SetActive(visible);
        }

        public void SetPlanePosition(Vector3 position)
        {
            if (groundPlane != null)
                groundPlane.transform.position = position;
        }

        public void SetPlaneSize(float size)
        {
            planeSize = Mathf.Max(0.1f, size);
            if (groundPlane != null)
                groundPlane.transform.localScale = new Vector3(planeSize / 10f, 1f, planeSize / 10f);
        }

        private void CreateCamera()
        {
            GameObject cameraObject = new GameObject("Skill Preview Camera");
            cameraObject.hideFlags = HideFlags.DontSave;
            previewCamera = cameraObject.AddComponent<Camera>();
            previewCamera.transform.position = Vector3.zero;
            previewCamera.transform.rotation = Quaternion.identity;
            previewCamera.clearFlags = CameraClearFlags.Color;
            previewCamera.backgroundColor = new Color(0.12f, 0.13f, 0.15f);
            previewCamera.nearClipPlane = 0.01f;
            previewCamera.farClipPlane = 1000f;
            previewCamera.fieldOfView = 35f;
            previewCamera.enabled = false;
            previewCamera.cameraType = CameraType.Preview;
            previewCamera.scene = previewScene;
            previewCamera.cullingMask = 1 << PreviewLayer;
            MoveToPreviewScene(cameraObject);
        }

        private void CreateLight()
        {
            GameObject lightObject = new GameObject("Skill Preview Directional Light");
            lightObject.hideFlags = HideFlags.DontSave;
            directionalLight = lightObject.AddComponent<Light>();
            directionalLight.type = LightType.Directional;
            directionalLight.intensity = 1.35f;
            directionalLight.transform.rotation = Quaternion.Euler(45f, 35f, 0f);
            MoveToPreviewScene(lightObject);
        }

        private void CreateGroundPlane()
        {
            groundPlane = GameObject.CreatePrimitive(PrimitiveType.Plane);
            groundPlane.name = "Skill Preview Ground Plane";
            groundPlane.hideFlags = HideFlags.DontSave;
            groundPlane.layer = PreviewLayer;
            groundPlane.transform.position = Vector3.zero;
            SetPlaneSize(planeSize);

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Standard");

            if (shader != null)
            {
                groundPlaneMaterial = new Material(shader)
                {
                    hideFlags = HideFlags.DontSave,
                    color = new Color(0.22f, 0.23f, 0.24f, 1f)
                };

                MeshRenderer renderer = groundPlane.GetComponent<MeshRenderer>();
                if (renderer != null)
                    renderer.sharedMaterial = groundPlaneMaterial;
            }

            MoveToPreviewScene(groundPlane);
        }

        private void CopySourceParentFrame(Transform selected)
        {
            if (previewOrigin == null || selected == null)
                return;

            Transform parent = selected.parent;
            if (parent == null)
            {
                previewOrigin.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                previewOrigin.transform.localScale = Vector3.one;
                return;
            }

            previewOrigin.transform.SetPositionAndRotation(parent.position, parent.rotation);
            previewOrigin.transform.localScale = parent.lossyScale;
        }

        private void MoveToPreviewScene(GameObject gameObject)
        {
            if (gameObject != null && previewScene.IsValid())
                SceneManager.MoveGameObjectToScene(gameObject, previewScene);
        }

        private void SetHideFlags(GameObject root)
        {
            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            foreach (Transform child in transforms)
            {
                child.gameObject.hideFlags = HideFlags.DontSave;
                child.gameObject.layer = PreviewLayer;
            }
        }

        private void BindPreviewAnimator()
        {
            sourceAnimator = sourceRoot != null ? sourceRoot.GetComponentInChildren<Animator>(true) : null;
            previewAnimator = null;

            if (sourceAnimator == null || previewRoot == null || sourceRoot == null)
                return;

            string animatorPath = AnimationUtility.CalculateTransformPath(sourceAnimator.transform, sourceRoot.transform);
            Transform previewAnimatorTransform = string.IsNullOrEmpty(animatorPath)
                ? previewRoot.transform
                : previewRoot.transform.Find(animatorPath);

            previewAnimator = previewAnimatorTransform != null
                ? previewAnimatorTransform.GetComponent<Animator>()
                : previewRoot.GetComponentInChildren<Animator>(true);

            Animator[] animators = previewRoot.GetComponentsInChildren<Animator>(true);
            foreach (Animator animator in animators)
            {
                animator.enabled = animator == previewAnimator;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }
        }

        private void DestroyPreviewObject(GameObject gameObject)
        {
            if (gameObject != null)
                Object.DestroyImmediate(gameObject);
        }
    }
}
