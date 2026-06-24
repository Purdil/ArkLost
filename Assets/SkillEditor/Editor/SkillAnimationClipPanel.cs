using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace SkillEditor.Editor
{
    public sealed class SkillAnimationClipPanel
    {
        private readonly SkillPreviewSceneContext context;
        private readonly Foldout root;
        private readonly TextField searchField;
        private readonly ListView clipListView;
        private readonly Label infoLabel;
        private readonly List<AnimationClip> clips = new List<AnimationClip>();
        private readonly List<AnimationClip> filteredClips = new List<AnimationClip>();
        private AnimationClip selectedClip;
        private string searchText = string.Empty;

        public event Action<AnimationClip> ClipSelected;

        public SkillAnimationClipPanel(SkillPreviewSceneContext context)
        {
            this.context = context;
            root = new Foldout
            {
                text = "Animation Clips",
                value = true
            };
            root.AddToClassList("skill-panel");

            searchField = new TextField();
            searchField.AddToClassList("skill-search-field");
            searchField.RegisterValueChangedCallback(evt =>
            {
                searchText = evt.newValue ?? string.Empty;
                ApplyFilter();
            });
            root.Add(searchField);

            clipListView = new ListView
            {
                fixedItemHeight = 24f,
                selectionType = SelectionType.Single,
                showAlternatingRowBackgrounds = AlternatingRowBackground.ContentOnly
            };
            clipListView.AddToClassList("skill-list");
            clipListView.makeItem = () => new Label();
            clipListView.bindItem = BindClipItem;
            clipListView.selectionChanged += HandleSelectionChanged;
            root.Add(clipListView);

            infoLabel = new Label("Clip: None");
            infoLabel.AddToClassList("skill-info-label");
            root.Add(infoLabel);
        }

        public VisualElement Root => root;

        public AnimationClip SelectedClip => selectedClip;

        public void Refresh()
        {
            clips.Clear();
            selectedClip = null;

            Animator animator = context.PreviewAnimator != null
                ? context.PreviewAnimator
                : context.PreviewRoot != null
                    ? context.PreviewRoot.GetComponentInChildren<Animator>(true)
                    : null;

            if (animator != null && animator.runtimeAnimatorController != null)
            {
                clips.AddRange(animator.runtimeAnimatorController.animationClips
                    .Where(clip => clip != null)
                    .GroupBy(clip => clip.GetInstanceID())
                    .Select(group => group.First())
                    .OrderBy(clip => clip.name));
            }

            ApplyFilter();

            if (filteredClips.Count > 0)
                SelectClip(filteredClips[0]);
            else
                ClipSelected?.Invoke(null);
        }

        private void ApplyFilter()
        {
            filteredClips.Clear();
            string lowerSearch = searchText.ToLowerInvariant();

            foreach (AnimationClip clip in clips)
            {
                if (string.IsNullOrWhiteSpace(lowerSearch) || clip.name.ToLowerInvariant().Contains(lowerSearch))
                    filteredClips.Add(clip);
            }

            clipListView.itemsSource = filteredClips;
            clipListView.Rebuild();
            UpdateInfoLabel();
        }

        private void BindClipItem(VisualElement element, int index)
        {
            Label label = (Label)element;
            if (index < 0 || index >= filteredClips.Count)
            {
                label.text = string.Empty;
                return;
            }

            AnimationClip clip = filteredClips[index];
            label.text = $"{clip.name}  ({clip.length:0.###}s)";
        }

        private void HandleSelectionChanged(IEnumerable<object> selectedItems)
        {
            foreach (object item in selectedItems)
            {
                SelectClip(item as AnimationClip);
                return;
            }
        }

        private void SelectClip(AnimationClip clip)
        {
            selectedClip = clip;
            int index = filteredClips.IndexOf(clip);
            if (index >= 0)
                clipListView.SetSelectionWithoutNotify(new[] { index });

            UpdateInfoLabel();
            ClipSelected?.Invoke(selectedClip);
        }

        private void UpdateInfoLabel()
        {
            infoLabel.text = selectedClip == null
                ? "Clip: None"
                : $"Clip: {selectedClip.name}\nLength: {selectedClip.length:0.###}s | Frame Rate: {selectedClip.frameRate:0.##}";
        }
    }
}
