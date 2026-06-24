using System;
using System.Collections.Generic;
using System.IO;
using GGMLib.ObjectPool.Runtime;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace GGMLib.ObjectPool.Editor
{
    public class ObjectPoolEditor : EditorWindow
    {
        [SerializeField] private VisualTreeAsset visualTreeAsset = default;
        [SerializeField] private PoolManagerSO poolManagerAsset = default;
        [SerializeField] private VisualTreeAsset itemAsset = default;

        private string _rootFolder;
        private Button _createButton;
        private ScrollView _itemView;

        private List<PoolItemViewUI> _itemUIList;
        private PoolItemViewUI _selectedItemUI;

        private UnityEditor.Editor _cachedEditor;
        private VisualElement _inspector;
        
        [MenuItem("Tools/PoolManager")]
        public static void ShowExample()
        {
            ObjectPoolEditor wnd = GetWindow<ObjectPoolEditor>();
            wnd.titleContent = new GUIContent("ObjectPoolEditor");
        }

        #region Utility section

        private string GetCurrentDirectory()
        {
            string scriptPath = AssetDatabase.GetAssetPath(MonoScript.FromScriptableObject(this));
            return Path.GetDirectoryName(scriptPath);
        }

        private void InitializeRootFolder()
        {
            string dirName = GetCurrentDirectory();
            DirectoryInfo parentDir = Directory.GetParent(dirName); //부모 디렉토리 가져와서
            Debug.Assert(parentDir != null, $"부모 디렉토리가 없습니다. {dirName}");
            
            string dataPath = Application.dataPath;
            _rootFolder = parentDir.FullName.Replace('\\', '/');
            if (_rootFolder.StartsWith(dataPath))
            {
                _rootFolder = $"Assets{_rootFolder.Substring(dataPath.Length)}";
            }
            Debug.Log(_rootFolder);
        }

        #endregion

        public void CreateGUI()
        {
            InitializeRootFolder();
            VisualElement root = rootVisualElement;

            if (visualTreeAsset == null)
            {
                string dirName = GetCurrentDirectory();
                visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>($"{dirName}/ObjectPoolEditor.uxml");
                visualTreeAsset.CloneTree(root);

                InitializeItems(root);
                GeneratePoolingItemUI();
            }
        }


        private void InitializeItems(VisualElement root)
        {
            _createButton = root.Q<Button>("CreateBtn");
            _createButton.clicked += HandleCreateItem;
            _itemView = root.Q<ScrollView>("ItemView");

            _itemView.Clear();
            _itemUIList = new List<PoolItemViewUI>();
            _inspector = root.Q<VisualElement>("InspectorView");
        }

        private void GeneratePoolingItemUI()
        {
            _itemView.Clear();
            _itemUIList.Clear();
            _inspector.Clear();

            if (poolManagerAsset == null)
            {
                string filePath = $"{_rootFolder}/PoolManager.asset";
                poolManagerAsset = AssetDatabase.LoadAssetAtPath<PoolManagerSO>(filePath);
                if (poolManagerAsset == null) //해당 경로에도 없다면
                {
                    Debug.LogWarning("플매니저 에셋이 없어서 새로 만들어집니다.");
                    poolManagerAsset = ScriptableObject.CreateInstance<PoolManagerSO>();
                    AssetDatabase.CreateAsset(poolManagerAsset, filePath);
                }
            }

            if (itemAsset == null)
            {
                string dirName = GetCurrentDirectory();
                itemAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>($"{dirName}/PoolItemView UI.uxml");
            }

            foreach (PoolItemSO item in poolManagerAsset.itemList)
            {
                TemplateContainer container = itemAsset.Instantiate();
                PoolItemViewUI itemViewUI = new PoolItemViewUI(container,item);
                _itemUIList.Add(itemViewUI); //UI에는 아이템으로 넣어준다.
                _itemView.Add(container); // 스크롤뷰에는 컨테이너로 넣어주고

                itemViewUI.Name = item.itemName;
                itemViewUI.IsEmpty = item.prefab == null;
                itemViewUI.IsActive = false;

                itemViewUI.OnSelectEvent += HandleSelectionEvent;
                itemViewUI.OnDeleteEvent += HandleDeleteEvent;
            }
            
            
        }

        #region Event Handling

        private void HandleSelectionEvent(PoolItemViewUI targetUI)
        {
            if (_selectedItemUI != null)
            {
                _selectedItemUI.IsActive = false;
            }
            _selectedItemUI = targetUI;
            _selectedItemUI.IsActive = true;
            
            _inspector.Clear();
            UnityEditor.Editor.CreateCachedEditor(_selectedItemUI.ItemSO, null, ref _cachedEditor);
            VisualElement inspectorElement = _cachedEditor.CreateInspectorGUI(); 
            //에디터에서 Inspector에 표시할 UIToolkit을 뽑는다. IMGUI => UIToolkit

            SerializedObject so = new SerializedObject(_selectedItemUI.ItemSO);
            inspectorElement.Bind(so);
            
            _inspector.Add(inspectorElement);
            inspectorElement.TrackSerializedObjectValue(so, so =>
            {
                _selectedItemUI.Name = so.FindProperty("itemName").stringValue;
                _selectedItemUI.IsEmpty = so.FindProperty("prefab").objectReferenceValue == null;
            });
        }
        private void HandleDeleteEvent(PoolItemViewUI targetUI)
        {
            poolManagerAsset.itemList.Remove(targetUI.ItemSO);
            AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(targetUI.ItemSO));
            EditorUtility.SetDirty(poolManagerAsset);
            AssetDatabase.SaveAssets();

            if (targetUI == _selectedItemUI)
            {
                _selectedItemUI = null;
                //인스펙터 클리어 코드 넣어야 해. 근데 아직 없어.
            }
            
            GeneratePoolingItemUI(); //다시 왼쪽 UI새로고침.
        }
        private void HandleCreateItem()
        {
            Guid itemGuid = Guid.NewGuid();
            PoolItemSO item = ScriptableObject.CreateInstance<PoolItemSO>();
            item.itemName = itemGuid.ToString(); //중복되지 않게 랜덤 guid 박음

            if (Directory.Exists($"{_rootFolder}/Items") == false)
            {
                Directory.CreateDirectory($"{_rootFolder}/Items");
            }
            
            AssetDatabase.CreateAsset(item, $"{_rootFolder}/Items/{item.itemName}.asset");
            poolManagerAsset.itemList.Add(item); //새로 만든 아이템 넣음.
            
            EditorUtility.SetDirty(poolManagerAsset); //더러워졌다는거 알려주고
            AssetDatabase.SaveAssets();
            
            GeneratePoolingItemUI(); //새롭게 생성.
        }
        #endregion
       


       
    }
}
