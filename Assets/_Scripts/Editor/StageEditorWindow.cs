using System;
using System.Linq;
using Editor;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

public class StageEditorWindow : EditorWindow
{
    [SerializeField]
    private VisualTreeAsset editorView = default;

    private ObjectField _rootObjectField;
    private ObjectField _prefabListField;
    private IntegerField _cellSizeField;
    private VisualElement _itemSelectContainer;
    private DropdownField _itemDropdownField;
    private VisualElement _previewImage;
    
    private static GameObject _rootObject;
    private static StagePrefabListSO _prefabList;
    private static int _cellSize = 5;

    private bool _isReadyToPlacement = false;
    private GameObject _selectedPrefab;
    

    [MenuItem("Tools/StageEditorWindow")]
    public static void ShowWindow()
    {
        StageEditorWindow wnd = GetWindow<StageEditorWindow>();
        wnd.titleContent = new GUIContent("StageEditorWindow");
    }

    public void CreateGUI()
    {
        // Each editor window contains a root VisualElement object
        VisualElement root = rootVisualElement;
        
        editorView.CloneTree(root);
        
        _rootObjectField = root.Q<ObjectField>("RootObjectField");
        _itemSelectContainer = root.Q<VisualElement>("ItemSelectContainer");
        _itemDropdownField = root.Q<DropdownField>("ItemDropdownField");
        _prefabListField = root.Q<ObjectField>("PrefabListObjectField");
        _previewImage = root.Q<VisualElement>("PreviewImage");
        _cellSizeField = root.Q<IntegerField>("CellSizeIntField");
        
        _rootObjectField.RegisterValueChangedCallback(HandleRootObjectChange);
        _prefabListField.RegisterValueChangedCallback(HandlePrefabListChange);
        _itemDropdownField.RegisterValueChangedCallback(HandleItemSelect);
        _cellSizeField.RegisterValueChangedCallback(evt => { _cellSize = evt.newValue; });

        if(_rootObject != null)
            _rootObjectField.SetValueWithoutNotify(_rootObject);
        if(_prefabList != null)
            _prefabListField.SetValueWithoutNotify(_prefabList);
        
        CheckSelectContainerActive();
    }

    private void OnEnable()
    {
        SceneView.duringSceneGui += HandleSceneGui;
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= HandleSceneGui;
    }

    private void HandleSceneGui(SceneView sceneView)
    {
        if (!_isReadyToPlacement)
            return;
        
        Event evt = Event.current;
        
        Ray ray = HandleUtility.GUIPointToWorldRay(evt.mousePosition);
        Plane groundPlane = new Plane(Vector3.up, Vector3.zero);

        if (groundPlane.Raycast(ray, out float distance))
        {
            Vector3 worldPosition = ray.GetPoint(distance);

            Vector3 snappedPosition = new Vector3(
                Mathf.Floor(worldPosition.x / _cellSize) * _cellSize + _cellSize * 0.5f,
                0,
                Mathf.Floor(worldPosition.z / _cellSize) * _cellSize + _cellSize * 0.5f);

            if (_selectedPrefab != null)
            {
                DrawPrefabPreview(snappedPosition);
                if (evt.type == EventType.MouseDown && evt.button == 0)
                {
                    PlacePrefab(snappedPosition);
                    evt.Use(); //이 이벤트를 다른곳에서 쓰지 않게 삭제해버린다.
                }
            }
            //씬뷰상의 기본 컨트롤 비활성화해서 포커싱을 유지하게 해주는거.
            HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
            
            sceneView.Repaint();
        }
    }

    private void PlacePrefab(Vector3 snappedPosition)
    {
        if(_selectedPrefab == null || !_isReadyToPlacement)
            return;
        
        Vector3 pivotOffset = new Vector3(_cellSize * 0.5f, 0, -_cellSize * 0.5f);
        Vector3 placementPosition = snappedPosition + pivotOffset;
        
        GameObject newInstance = PrefabUtility.InstantiatePrefab(_selectedPrefab, _rootObject.transform)
            as GameObject;
        
        newInstance.transform.position = placementPosition;
        
        Undo.RegisterCreatedObjectUndo(newInstance, $"Placed Prefab {newInstance.name}");
    }

    private void DrawPrefabPreview(Vector3 snappedPosition)
    {
        Handles.color = Color.green;
        Handles.DrawWireCube(snappedPosition, new Vector3(_cellSize, 0.1f, _cellSize));
    }

    private void HandleItemSelect(ChangeEvent<string> evt)
    {
        if (string.IsNullOrEmpty(evt.newValue))
        {
            _previewImage.style.backgroundImage = null;
            _isReadyToPlacement = false;
            _selectedPrefab = null;
            return;
        }

        _selectedPrefab = _prefabList.prefabs[_itemDropdownField.index]; //현재 선택된 인덱스의 프리팹을 가져와
        Texture2D preview = AssetPreview.GetAssetPreview(_selectedPrefab);
        if (preview != null)
        {
            _previewImage.style.backgroundImage = preview;
        }
        else
        {
            if (AssetPreview.IsLoadingAssetPreview(_selectedPrefab.GetInstanceID()))
            {
                _previewImage.schedule.Execute(() =>
                {
                    Texture2D loadedPreview = AssetPreview.GetAssetPreview(_selectedPrefab);
                    if(loadedPreview != null)
                        _previewImage.style.backgroundImage = loadedPreview; 
                }).Until(() => !AssetPreview.IsLoadingAssetPreview(_selectedPrefab.GetInstanceID()));
            }
        }
        _isReadyToPlacement = true;
    }

    private void CheckSelectContainerActive()
    {
        bool isReadyToView = _rootObject != null && _prefabList != null;
        
        _itemSelectContainer.style.display = isReadyToView ? DisplayStyle.Flex : DisplayStyle.None;
        if (isReadyToView)
        {
            _itemDropdownField.choices.Clear();
            _itemDropdownField.choices.AddRange(
                _prefabList.prefabs.Select(prefab => prefab.name));
        }
        else
        {
            _isReadyToPlacement = false;
        }
    }

    private void HandlePrefabListChange(ChangeEvent<Object> evt)
    {
        _prefabList = evt.newValue as StagePrefabListSO;
        CheckSelectContainerActive();
    }

    private void HandleRootObjectChange(ChangeEvent<Object> evt)
    {
        //Debug.Log($"이전 값 : {evt.previousValue} 에서 {evt.newValue} 로 변환");
        GameObject newRootObject = evt.newValue as GameObject;
        if (PrefabUtility.IsPartOfPrefabAsset(newRootObject))
        {
            _rootObject = null;
            _rootObjectField.SetValueWithoutNotify(null);
            EditorUtility.DisplayDialog("Error", 
                "루트 오브젝트는 하이라키에 있는 오브젝트여야 합니다.", "OK");
            return;
        }
        
        _rootObject = newRootObject;
        CheckSelectContainerActive();
    }
}
