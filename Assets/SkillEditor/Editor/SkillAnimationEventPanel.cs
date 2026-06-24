using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace SkillEditor.Editor
{
    public sealed class SkillAnimationEventPanel
    {
        private readonly Func<float> previewTimeGetter;
        private readonly Action<float> timeRequested;
        private readonly Foldout root;
        private readonly Label warningLabel;
        private readonly Label dirtyIndicator;
        private readonly Button addButton;
        private readonly Button deleteButton;
        private readonly Button saveButton;
        private readonly Button jumpButton;
        private readonly Button setTimeButton;
        private readonly ListView eventListView;
        private readonly FloatField timeField;
        private readonly TextField functionNameField;
        private readonly TextField stringParameterField;
        private readonly FloatField floatParameterField;
        private readonly IntegerField intParameterField;
        private readonly ObjectField objectReferenceField;
        private readonly Label eventKeyLabel;
        private readonly List<AnimationEvent> events = new List<AnimationEvent>();

        private AnimationClip clip;
        private AnimationEvent selectedEvent;
        private bool dirty;
        private bool canEdit;
        private bool syncingFields;

        public event Action DirtyChanged;
        public event Action EventsChanged;

        public SkillAnimationEventPanel(Func<float> previewTimeGetter, Action<float> timeRequested)
        {
            this.previewTimeGetter = previewTimeGetter;
            this.timeRequested = timeRequested;

            root = new Foldout
            {
                text = "Animation Events",
                value = true
            };
            root.AddToClassList("skill-panel");

            warningLabel = new Label();
            warningLabel.AddToClassList("skill-warning-label");
            root.Add(warningLabel);

            VisualElement buttonRow = new VisualElement();
            buttonRow.AddToClassList("skill-row");
            root.Add(buttonRow);

            addButton = new Button(AddEvent) { text = "Add Event" };
            deleteButton = new Button(DeleteSelectedEvent) { text = "Delete" };
            saveButton = new Button(SaveEvents) { text = "Save" };
            dirtyIndicator = new Label();
            dirtyIndicator.AddToClassList("skill-dirty-indicator");

            buttonRow.Add(addButton);
            buttonRow.Add(deleteButton);
            buttonRow.Add(saveButton);
            buttonRow.Add(dirtyIndicator);

            eventListView = new ListView
            {
                fixedItemHeight = 28f,
                selectionType = SelectionType.Single,
                showAlternatingRowBackgrounds = AlternatingRowBackground.ContentOnly
            };
            eventListView.AddToClassList("skill-event-list");
            eventListView.makeItem = () => new Label();
            eventListView.bindItem = BindEventItem;
            eventListView.selectionChanged += HandleSelectionChanged;
            eventListView.RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.clickCount >= 2)
                    JumpToSelectedEvent();
            });
            root.Add(eventListView);

            VisualElement jumpRow = new VisualElement();
            jumpRow.AddToClassList("skill-row");
            root.Add(jumpRow);

            jumpButton = new Button(JumpToSelectedEvent) { text = "Jump" };
            setTimeButton = new Button(SetSelectedEventTimeToCurrent) { text = "Set Time To Current" };
            jumpRow.Add(jumpButton);
            jumpRow.Add(setTimeButton);

            eventKeyLabel = new Label();
            eventKeyLabel.AddToClassList("skill-event-key-label");
            root.Add(eventKeyLabel);

            timeField = new FloatField("time");
            functionNameField = new TextField("functionName");
            stringParameterField = new TextField("stringParameter");
            floatParameterField = new FloatField("floatParameter");
            intParameterField = new IntegerField("intParameter");
            objectReferenceField = new ObjectField("objectReferenceParameter")
            {
                objectType = typeof(Object),
                allowSceneObjects = false
            };

            RegisterDetailCallbacks();
            root.Add(timeField);
            root.Add(functionNameField);
            root.Add(stringParameterField);
            root.Add(floatParameterField);
            root.Add(intParameterField);
            root.Add(objectReferenceField);

            LoadClip(null);
        }

        public VisualElement Root => root;
        public bool HasDirtyChanges => dirty;

        public void LoadClip(AnimationClip newClip)
        {
            clip = newClip;
            selectedEvent = null;
            dirty = false;
            events.Clear();

            if (clip != null)
            {
                try
                {
                    AnimationEvent[] loadedEvents = AnimationUtility.GetAnimationEvents(clip);
                    foreach (AnimationEvent loadedEvent in loadedEvents)
                    {
                        events.Add(CloneEvent(loadedEvent));
                    }
                }
                catch (Exception exception)
                {
                    Debug.LogError($"Failed to read Animation Events from {clip.name}: {exception.Message}");
                }
            }

            SortEvents();
            UpdateEditability();
            Rebuild();
            NotifyDirtyChanged();
            EventsChanged?.Invoke();
        }

        public void SaveEvents()
        {
            if (clip == null || !canEdit)
                return;

            try
            {
                SortEvents();
                Undo.RecordObject(clip, "Save Animation Events");
                AnimationUtility.SetAnimationEvents(clip, events.ToArray());
                EditorUtility.SetDirty(clip);
                AssetDatabase.SaveAssets();
                dirty = false;
                Rebuild();
                NotifyDirtyChanged();
            }
            catch (Exception exception)
            {
                Debug.LogError($"Failed to save Animation Events to {clip.name}: {exception.Message}");
            }
        }

        private void AddEvent()
        {
            if (!canEdit)
                return;

            RecordClipUndo("Add Animation Event");
            AnimationEvent animationEvent = new AnimationEvent
            {
                time = Mathf.Clamp(previewTimeGetter(), 0f, clip != null ? clip.length : 0f),
                functionName = "AgentTrigger",
                stringParameter = CreateDefaultEventKey()
            };

            events.Add(animationEvent);
            selectedEvent = animationEvent;
            SortEvents();
            MarkDirty();
            Rebuild();
            timeRequested?.Invoke(animationEvent.time);
            EventsChanged?.Invoke();
        }

        private void DeleteSelectedEvent()
        {
            if (!canEdit || selectedEvent == null)
                return;

            RecordClipUndo("Delete Animation Event");
            events.Remove(selectedEvent);
            selectedEvent = null;
            MarkDirty();
            Rebuild();
            EventsChanged?.Invoke();
        }

        private void JumpToSelectedEvent()
        {
            if (selectedEvent != null)
                timeRequested?.Invoke(selectedEvent.time);
        }

        private void SetSelectedEventTimeToCurrent()
        {
            if (!canEdit || selectedEvent == null)
                return;

            UpdateSelectedEvent(animationEvent => animationEvent.time = Mathf.Clamp(previewTimeGetter(), 0f, clip.length));
            timeRequested?.Invoke(selectedEvent.time);
        }

        private void BindEventItem(VisualElement element, int index)
        {
            Label label = (Label)element;
            if (index < 0 || index >= events.Count)
            {
                label.text = string.Empty;
                return;
            }

            AnimationEvent animationEvent = events[index];
            string displayName = animationEvent.functionName == "AgentTrigger"
                ? $"AgentTrigger(\"{animationEvent.stringParameter}\")"
                : animationEvent.functionName;

            label.text = $"{animationEvent.time:0.000}s | {displayName} | s:{animationEvent.stringParameter} f:{animationEvent.floatParameter:0.###} i:{animationEvent.intParameter}";
        }

        private void HandleSelectionChanged(IEnumerable<object> selectedItems)
        {
            foreach (object item in selectedItems)
            {
                selectedEvent = item as AnimationEvent;
                if (selectedEvent != null)
                    timeRequested?.Invoke(selectedEvent.time);

                SyncDetailFields();
                return;
            }
        }

        private void RegisterDetailCallbacks()
        {
            timeField.RegisterValueChangedCallback(evt => UpdateSelectedEvent(animationEvent => animationEvent.time = Mathf.Clamp(evt.newValue, 0f, clip != null ? clip.length : 0f)));
            functionNameField.RegisterValueChangedCallback(evt => UpdateSelectedEvent(animationEvent => animationEvent.functionName = evt.newValue ?? string.Empty));
            stringParameterField.RegisterValueChangedCallback(evt => UpdateSelectedEvent(animationEvent => animationEvent.stringParameter = evt.newValue ?? string.Empty));
            floatParameterField.RegisterValueChangedCallback(evt => UpdateSelectedEvent(animationEvent => animationEvent.floatParameter = evt.newValue));
            intParameterField.RegisterValueChangedCallback(evt => UpdateSelectedEvent(animationEvent => animationEvent.intParameter = evt.newValue));
            objectReferenceField.RegisterValueChangedCallback(evt => UpdateSelectedEvent(animationEvent => animationEvent.objectReferenceParameter = evt.newValue));
        }

        private void UpdateSelectedEvent(Action<AnimationEvent> edit)
        {
            if (syncingFields || !canEdit || selectedEvent == null)
                return;

            RecordClipUndo("Edit Animation Event");
            edit(selectedEvent);
            SortEvents();
            MarkDirty();
            Rebuild();
            EventsChanged?.Invoke();
        }

        private void Rebuild()
        {
            eventListView.itemsSource = events;
            eventListView.Rebuild();

            if (selectedEvent != null)
            {
                int index = events.IndexOf(selectedEvent);
                if (index >= 0)
                    eventListView.SetSelectionWithoutNotify(new[] { index });
            }

            SyncDetailFields();
            UpdateControls();
        }

        private void SyncDetailFields()
        {
            syncingFields = true;
            bool hasEvent = selectedEvent != null;

            timeField.SetEnabled(canEdit && hasEvent);
            functionNameField.SetEnabled(canEdit && hasEvent);
            stringParameterField.SetEnabled(canEdit && hasEvent);
            floatParameterField.SetEnabled(canEdit && hasEvent);
            intParameterField.SetEnabled(canEdit && hasEvent);
            objectReferenceField.SetEnabled(canEdit && hasEvent);
            jumpButton.SetEnabled(hasEvent);
            setTimeButton.SetEnabled(canEdit && hasEvent);
            deleteButton.SetEnabled(canEdit && hasEvent);

            if (!hasEvent)
            {
                timeField.SetValueWithoutNotify(0f);
                functionNameField.SetValueWithoutNotify(string.Empty);
                stringParameterField.SetValueWithoutNotify(string.Empty);
                floatParameterField.SetValueWithoutNotify(0f);
                intParameterField.SetValueWithoutNotify(0);
                objectReferenceField.SetValueWithoutNotify(null);
                eventKeyLabel.text = "No Animation Event selected.";
                syncingFields = false;
                return;
            }

            timeField.SetValueWithoutNotify(selectedEvent.time);
            functionNameField.SetValueWithoutNotify(selectedEvent.functionName);
            stringParameterField.SetValueWithoutNotify(selectedEvent.stringParameter);
            floatParameterField.SetValueWithoutNotify(selectedEvent.floatParameter);
            intParameterField.SetValueWithoutNotify(selectedEvent.intParameter);
            objectReferenceField.SetValueWithoutNotify(selectedEvent.objectReferenceParameter);
            eventKeyLabel.text = selectedEvent.functionName == "AgentTrigger"
                ? $"eventKey: {selectedEvent.stringParameter}"
                : "eventKey is shown when functionName is AgentTrigger.";

            syncingFields = false;
        }

        private void UpdateControls()
        {
            addButton.SetEnabled(canEdit);
            saveButton.SetEnabled(canEdit && dirty);
            dirtyIndicator.text = dirty ? "*" : string.Empty;

            if (clip == null)
            {
                warningLabel.text = "No AnimationClip selected.";
                return;
            }

            if (!canEdit)
            {
                warningLabel.text = "Selected clip is not a writable .anim asset. Event editing is disabled.";
                return;
            }

            warningLabel.text = dirty ? "Unsaved Animation Event changes." : string.Empty;
        }

        private void UpdateEditability()
        {
            canEdit = false;

            if (clip == null)
                return;

            string assetPath = AssetDatabase.GetAssetPath(clip);
            if (string.IsNullOrEmpty(assetPath))
                return;

            canEdit = AssetDatabase.Contains(clip)
                      && string.Equals(Path.GetExtension(assetPath), ".anim", StringComparison.OrdinalIgnoreCase)
                      && !AssetDatabase.IsSubAsset(clip);
        }

        private void MarkDirty()
        {
            dirty = true;
            NotifyDirtyChanged();
        }

        private void NotifyDirtyChanged()
        {
            DirtyChanged?.Invoke();
        }

        private void RecordClipUndo(string label)
        {
            if (clip != null)
                Undo.RecordObject(clip, label);
        }

        private void SortEvents()
        {
            events.Sort((left, right) => left.time.CompareTo(right.time));
        }

        private string CreateDefaultEventKey()
        {
            int index = 1;
            while (true)
            {
                string candidate = $"Event_{index:00}";
                bool exists = events.Exists(animationEvent => animationEvent.stringParameter == candidate);
                if (!exists)
                    return candidate;

                index++;
            }
        }

        private AnimationEvent CloneEvent(AnimationEvent source)
        {
            return new AnimationEvent
            {
                time = source.time,
                functionName = source.functionName,
                stringParameter = source.stringParameter,
                floatParameter = source.floatParameter,
                intParameter = source.intParameter,
                objectReferenceParameter = source.objectReferenceParameter
            };
        }
    }
}
