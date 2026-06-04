using System.Drawing.Printing;
using System.IO;
using System.Linq;
using CoreSystem;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Agents.FSM.Editor
{
    [CustomEditor(typeof(StateListSO))]
    public class StateListSOEditor : UnityEditor.Editor
    {
        [SerializeField] private VisualTreeAsset editorView = default;

        private Button _folderBtn;
        private Button _generateBtn;
        private Label _folderPathLabel;

        private string _folderPath;
        private StateListSO _targetData;
        
        public override VisualElement CreateInspectorGUI()
        {
            VisualElement root = new VisualElement();
            //기본에디터를 채워주는 내용.
            InspectorElement.FillDefaultInspector(root, serializedObject, this);
            editorView.CloneTree(root);
            
            _folderBtn = root.Q<Button>("FolderBtn");
            _generateBtn = root.Q<Button>("GenerateBtn");
            _folderPathLabel = root.Q<Label>("SelectedFolderLabel");
            _folderPathLabel.text = "No folder selected";

            _targetData = target as StateListSO;
            _folderBtn.clicked += HandleFolderSelectBtn;
            _generateBtn.clicked += HandleGenenrateBtn;

            if (_targetData != null && !string.IsNullOrEmpty(_targetData.generatePath))
            {
                _folderPath = _targetData.generatePath;
                _folderPathLabel.text = FileUtil.GetProjectRelativePath(_targetData.generatePath);
            }

            return root;
        }

        private void HandleGenenrateBtn()
        {
            if (string.IsNullOrEmpty(_folderPath) || !Directory.Exists(_folderPath))
            {
                EditorUtility.DisplayDialog("Folder Not Found", "경로 설정이 올바르지 않습니다.", "OK");
                return;
            }

            int index = 0;
            string enumString = string.Join(",", _targetData.states.Select(so =>
            {
                so.assetIndex = index;
                EditorUtility.SetDirty(so);
                return $"{so.stateName} = {index++}";
            }));

            //이름 공간은 경로를 기반으로 자동으로 추측되어야 한다.(라이더처럼)
            string nameSpace = FileUtil.GetProjectRelativePath(_folderPath).Substring("Assets/".Length);
            if (nameSpace.StartsWith("Scripts/"))
            {
                nameSpace = nameSpace.Substring("Scripts/".Length);
            }

            nameSpace = nameSpace.Replace("/", ".");
            
            string code = string.Format(CodeFormat.EnumFormat, nameSpace, _targetData.enumName, enumString);
            File.WriteAllText($"{_folderPath}/{_targetData.enumName}.cs", code);
            
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(); //이걸 해줘야 컴파일이 새로 들어간다.
        }

        private void HandleFolderSelectBtn()
        {
            _folderPath = EditorUtility.OpenFolderPanel("폴더를 선택", _folderPath, "");

            if (!string.IsNullOrEmpty(_folderPath))
            {
                _folderPathLabel.text = FileUtil.GetProjectRelativePath(_folderPath);
                _targetData.generatePath = _folderPath;
                EditorUtility.SetDirty(_targetData);
                AssetDatabase.SaveAssets(); //더러워진 거 전부 저장.
            }
        }
    }
}