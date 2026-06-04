using GGMLib.ObjectPool.Runtime;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace GGMLib.ObjectPool.Editor
{
    [CustomEditor(typeof(PoolItemSO))]
    public class PoolItemSOView : UnityEditor.Editor
    {
        [SerializeField] private VisualTreeAsset viewAsset = default;

        private TextField _nameField;
        private Button _changeBtn;
        private ObjectField _prefabField;
        
        public override VisualElement CreateInspectorGUI()
        {
            VisualElement root = new  VisualElement();
            viewAsset.CloneTree(root);
            //InspectorElement.FillDefaultInspector(root,serializedObject,this);
            
            _nameField = root.Q<TextField>("PoolingName");
            _changeBtn = root.Q<Button>("ChangeBtn");
            _prefabField = root.Q<ObjectField>("PrefabField");
            
            _changeBtn.clicked += HandleChangeButtonClick;
            _nameField.RegisterCallback<KeyDownEvent>(HandleKeyDownEvent);
            _prefabField.RegisterValueChangedCallback(HandlePrefabChange);
            
            return root;
        }

        private void HandlePrefabChange(ChangeEvent<Object> evt)
        {
            if (evt.newValue == null) return; //프리팹이 빠진거면 할게 없다.
            
            GameObject go = evt.newValue as GameObject;
            PoolItemSO poolItem = target as PoolItemSO; //에디터 타겟을 가져온다.

            if (!go.TryGetComponent(out IPoolable poolable))
            {
                poolItem.prefab = null;
                EditorUtility.SetDirty(poolItem);
                AssetDatabase.SaveAssetIfDirty(poolItem);
                EditorUtility.DisplayDialog("Error", "Pool Prefab Not Found", "OK");
                return;
            }

            poolable.Item = poolItem; //SO를 프리팹에 할당한다.
            EditorUtility.SetDirty(go); // 게임오브젝트를 저장하기 위해 dirty
            AssetDatabase.SaveAssetIfDirty(go);

        }

        private void HandleChangeButtonClick()
        {
            string newName = _nameField.value;
            if (string.IsNullOrEmpty(newName))
            {
                EditorUtility.DisplayDialog("Error", "Please enter a name", "OK");
                return;
            }
            
            string assetPath = AssetDatabase.GetAssetPath(target);
            string changeMsg = AssetDatabase.RenameAsset(assetPath, newName);
            if (string.IsNullOrEmpty(changeMsg)) //성공적으로 변겅
                target.name = newName;
            else
            {
                EditorUtility.DisplayDialog("Error", changeMsg, "OK");
            }

        }

        private void HandleKeyDownEvent(KeyDownEvent evt)
        {
            if(evt.keyCode == KeyCode.Return)
                HandleChangeButtonClick();
        }
    }
}