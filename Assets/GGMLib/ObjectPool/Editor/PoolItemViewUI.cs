using System;
using GGMLib.ObjectPool.Runtime;
using UnityEngine.UIElements;

namespace GGMLib.ObjectPool.Editor
{
    public class PoolItemViewUI
    {
        private Label _nameLabel;
        private Button _deleteBtn;
        private VisualElement _rootElement;
        private Label _warningLabel;

        public event Action<PoolItemViewUI> OnDeleteEvent;
        public event Action<PoolItemViewUI> OnSelectEvent;

        public string Name
        {
            get => _nameLabel.text;
            set => _nameLabel.text = value;
        }
        
        public PoolItemSO ItemSO { get; private set; }

        public bool IsActive
        {
            get => _rootElement.ClassListContains("active");
            set => _rootElement.EnableInClassList("active", value);
        }

        public bool IsEmpty
        {
            get => _warningLabel.ClassListContains("on");
            set => _warningLabel.EnableInClassList("on", value);
        }

        public PoolItemViewUI(VisualElement root, PoolItemSO itemSO)
        {
            ItemSO = itemSO;
            _rootElement = root.Q("PoolItem");
            _nameLabel = _rootElement.Q<Label>("ItemName");
            _deleteBtn = _rootElement.Q<Button>("DeleteBtn");
            _warningLabel = _rootElement.Q<Label>("WarningLabel");
            
            _deleteBtn.RegisterCallback<ClickEvent>(evt =>
            {
                OnDeleteEvent?.Invoke(this);
                evt.StopPropagation();
            });
            
            _rootElement.RegisterCallback<ClickEvent>(evt =>
            {
                OnSelectEvent?.Invoke(this);
                evt.StopPropagation();
            });
        }
    }
}