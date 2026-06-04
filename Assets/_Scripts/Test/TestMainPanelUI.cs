using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Test
{
    public class TestMainPanelUI : MonoBehaviour
    {
        [SerializeField] private VisualTreeAsset contentAsset;
        
        private UIDocument _uiDocument;
        private VisualElement _root;
        private VisualElement _popUpWindow;
        private VisualElement _popUpContent;

        private void Awake()
        {
            _uiDocument = GetComponent<UIDocument>();
        }

        private void OnEnable()
        {
            _root = _uiDocument.rootVisualElement;
            _popUpWindow = _root.Q<VisualElement>("PopUpWindow");
            Debug.Assert(_popUpWindow != null, "팝업 윈도우가 없습니다.");
            _popUpContent = _root.Q<VisualElement>("Content");
            Debug.Assert(_popUpContent != null, "팝업 컨텐츠가 없습니다.");
            
            VisualElement topContainer = _root.Q<VisualElement>("TopContainer");
            topContainer.RegisterCallback<ClickEvent>(HandleBtnClick);
            
            Button closeBtn = _root.Q<Button>("CloseBtn");
            closeBtn.RegisterCallback<ClickEvent>(HandleCloseButton);
        }

        private void HandleCloseButton(ClickEvent evt)
        {
            _popUpWindow.RemoveFromClassList("open");
        }

        private void HandleBtnClick(ClickEvent evt)
        {
            if (evt.target is DataButton {ButtonIndex: 1} dataBtn)
            {
                OpenPopUpWindow();
            }
            
        }

        private void OpenPopUpWindow()
        {
            _popUpContent.Clear(); //자식들이 전부 없어져.
            contentAsset.CloneTree(_popUpContent); //_popUpContent의 자식으로 복사해서 생성해줘.
            
            _popUpWindow.AddToClassList("open");
            
        }
    }
}