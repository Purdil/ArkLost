using System;
using System.Collections.Generic;
using System.Linq;
using _Scripts.CoreSystem.Effects;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace _Scripts.Editor.AnimationVfxEventEditor
{
    public class AnimationVfxEventEditorWindow : EditorWindow
    {
        private const string StyleSheetPath = "Assets/_Scripts/Editor/AnimationVfxEventEditor/AnimationVfxEventEditor.uss";
        private readonly AnimationVfxPreviewRenderer previewRenderer = new AnimationVfxPreviewRenderer();
        private readonly List<AnimationClip> clips = new List<AnimationClip>();
        private readonly List<AnimationEvent> animationEvents = new List<AnimationEvent>();
        private readonly List<Component> vfxComponents = new List<Component>();

        private ObjectField characterField;
        private ObjectField clipField;
        private ListView clipList;
        private ListView eventList;
        private ListView vfxList;
        private VisualElement eventTrack;
        private VisualElement playhead;
        private VisualElement eventInspector;
        private VisualElement vfxInspector;
        private IMGUIContainer previewContainer;
        private Slider timeSlider;
        private FloatField timeField;
        private Button playButton;
        private Toggle loopToggle;
        private Toggle showSelectedVfxToggle;
        private Label clipInfoLabel;
        private Label statusLabel;
        private Label selectionSummaryLabel;
        private Vector3Field vfxLocalPositionField;
        private Vector3Field vfxLocalRotationField;
        private Vector3Field vfxLocalScaleField;

        private GameObject character;
        private AnimationClip selectedClip;
        private AnimationEvent selectedEvent;
        private Component selectedVfx;
        private Vector3 lastSyncedVfxPosition;
        private Vector3 lastSyncedVfxRotation;
        private Vector3 lastSyncedVfxScale;
        private int previewCameraMode;
        private int previewCameraButton = -1;
        private Vector2 lastPreviewMousePosition;
        private double lastUpdateTime;
        private float currentTime;
        private bool isPlaying;

        [MenuItem("Tools/Animation VFX Event Editor")]
        public static void ShowWindow()
        {
            AnimationVfxEventEditorWindow window = GetWindow<AnimationVfxEventEditorWindow>();
            window.titleContent = new GUIContent("Animation VFX Event Editor");
            window.minSize = new Vector2(1160f, 720f);
            window.Focus();
        }

        public void CreateGUI()
        {
            rootVisualElement.Clear();
            LoadStyleSheet();
            BuildLayout();
            previewRenderer.SelectedVfxTransformChanged = HandlePreviewVfxTransformChanged;
            UseCurrentSelectionIfPossible();
            RefreshAll();
        }

        private void OnEnable()
        {
            EditorApplication.update += HandleEditorUpdate;
            Selection.selectionChanged += HandleUnitySelectionChanged;
            Undo.undoRedoPerformed += HandleUndoRedoPerformed;
            lastUpdateTime = EditorApplication.timeSinceStartup;
        }

        private void OnDisable()
        {
            EditorApplication.update -= HandleEditorUpdate;
            Selection.selectionChanged -= HandleUnitySelectionChanged;
            Undo.undoRedoPerformed -= HandleUndoRedoPerformed;
            previewRenderer.Dispose();
        }

        private void LoadStyleSheet()
        {
            StyleSheet styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(StyleSheetPath);
            if (styleSheet != null)
                rootVisualElement.styleSheets.Add(styleSheet);
        }

        private void BuildLayout()
        {
            VisualElement root = rootVisualElement;
            root.AddToClassList("avfx-root");
            root.style.flexGrow = 1f;
            root.style.flexDirection = FlexDirection.Column;

            BuildHeader(root);
            BuildTopBar(root);

            VisualElement body = new VisualElement();
            body.AddToClassList("avfx-body");
            body.style.flexGrow = 1f;
            root.Add(body);

            BuildLeftPanel(body);
            BuildCenterPanel(body);
            BuildRightPanel(body);
        }

        private void BuildHeader(VisualElement root)
        {
            VisualElement header = new VisualElement();
            header.AddToClassList("avfx-header");
            root.Add(header);

            Label title = new Label("Animation VFX Event Editor");
            title.AddToClassList("avfx-title");
            header.Add(title);

            statusLabel = new Label("Select a scene character, animator child, or child VFX. The editor will load the working target automatically.");
            statusLabel.AddToClassList("avfx-status");
            header.Add(statusLabel);
        }

        private void BuildTopBar(VisualElement root)
        {
            VisualElement topBar = new VisualElement();
            topBar.AddToClassList("avfx-topbar");
            root.Add(topBar);

            characterField = CreateObjectField("Scene Character", typeof(GameObject), true);
            characterField.RegisterValueChangedCallback(changeEvent => SelectCharacterFromField(changeEvent.newValue as GameObject));
            topBar.Add(characterField);

            clipField = CreateObjectField("Animation Clip", typeof(AnimationClip), false);
            clipField.RegisterValueChangedCallback(changeEvent => SelectClip(changeEvent.newValue as AnimationClip));
            topBar.Add(clipField);

            Button useSelectionButton = new Button(UseCurrentSelectionIfPossible) { text = "Use Selection" };
            useSelectionButton.AddToClassList("avfx-button-primary");
            topBar.Add(useSelectionButton);

            Button refreshButton = new Button(RefreshAll) { text = "Refresh" };
            refreshButton.AddToClassList("avfx-button");
            topBar.Add(refreshButton);

            selectionSummaryLabel = new Label("No scene character selected");
            selectionSummaryLabel.AddToClassList("avfx-selection-summary");
            topBar.Add(selectionSummaryLabel);
        }

        private void BuildLeftPanel(VisualElement body)
        {
            VisualElement panel = new VisualElement();
            panel.AddToClassList("avfx-left-panel");
            body.Add(panel);

            panel.Add(CreateSectionLabel("Animation Clips"));
            clipList = CreateListView(130f);
            clipList.makeItem = () => new Label();
            clipList.bindItem = BindClipItem;
            clipList.selectionChanged += HandleClipSelectionChanged;
            panel.Add(clipList);

            panel.Add(CreateSectionLabel("Animation Events"));
            VisualElement eventButtons = new VisualElement();
            eventButtons.AddToClassList("avfx-row");
            eventButtons.AddToClassList("avfx-action-row");
            panel.Add(eventButtons);

            Button addEventButton = new Button(AddGenericEvent) { text = "+ Event" };
            addEventButton.AddToClassList("avfx-button");
            eventButtons.Add(addEventButton);

            Button addVfxEventButton = new Button(AddVfxInvokeEvent) { text = "+ VFX Invoke" };
            addVfxEventButton.AddToClassList("avfx-button");
            eventButtons.Add(addVfxEventButton);

            eventList = CreateListView(190f);
            eventList.makeItem = () => new Label();
            eventList.bindItem = BindEventItem;
            eventList.selectionChanged += HandleEventSelectionChanged;
            panel.Add(eventList);

            panel.Add(CreateSectionLabel("Child VFX Components"));
            vfxList = CreateListView(150f);
            vfxList.makeItem = () => new Label();
            vfxList.bindItem = BindVfxItem;
            vfxList.selectionChanged += HandleVfxSelectionChanged;
            panel.Add(vfxList);
        }

        private void BuildCenterPanel(VisualElement body)
        {
            VisualElement panel = new VisualElement();
            panel.AddToClassList("avfx-center-panel");
            body.Add(panel);

            previewContainer = new IMGUIContainer(DrawPreview);
            previewContainer.focusable = true;
            previewContainer.AddToClassList("avfx-preview");
            previewContainer.RegisterCallback<MouseDownEvent>(_ => previewContainer.Focus(), TrickleDown.TrickleDown);
            previewContainer.RegisterCallback<MouseDownEvent>(HandlePreviewMouseDown, TrickleDown.TrickleDown);
            previewContainer.RegisterCallback<MouseMoveEvent>(HandlePreviewMouseMove, TrickleDown.TrickleDown);
            previewContainer.RegisterCallback<MouseUpEvent>(HandlePreviewMouseUp, TrickleDown.TrickleDown);
            previewContainer.RegisterCallback<KeyDownEvent>(HandlePreviewKeyDown, TrickleDown.TrickleDown);
            previewContainer.RegisterCallback<WheelEvent>(HandlePreviewWheel, TrickleDown.TrickleDown);
            panel.Add(previewContainer);

            VisualElement transport = new VisualElement();
            transport.AddToClassList("avfx-transport");
            panel.Add(transport);

            playButton = new Button(TogglePlayback) { text = "Play" };
            playButton.AddToClassList("avfx-transport-button");
            transport.Add(playButton);

            Button stopButton = new Button(StopPlayback) { text = "Stop" };
            stopButton.AddToClassList("avfx-transport-button");
            transport.Add(stopButton);

            loopToggle = new Toggle("Loop");
            loopToggle.AddToClassList("avfx-toggle");
            transport.Add(loopToggle);

            showSelectedVfxToggle = new Toggle("Preview VFX");
            showSelectedVfxToggle.SetValueWithoutNotify(true);
            showSelectedVfxToggle.AddToClassList("avfx-toggle");
            showSelectedVfxToggle.RegisterValueChangedCallback(_ => previewContainer.MarkDirtyRepaint());
            transport.Add(showSelectedVfxToggle);

            timeSlider = new Slider(0f, 1f);
            timeSlider.AddToClassList("avfx-time-slider");
            timeSlider.RegisterValueChangedCallback(changeEvent => SetCurrentTime(changeEvent.newValue, true));
            transport.Add(timeSlider);

            timeField = new FloatField();
            timeField.AddToClassList("avfx-time-field");
            timeField.RegisterValueChangedCallback(changeEvent => SetCurrentTime(changeEvent.newValue, true));
            transport.Add(timeField);

            BuildTimeline(panel);
        }

        private void BuildTimeline(VisualElement panel)
        {
            VisualElement timeline = new VisualElement();
            timeline.AddToClassList("avfx-timeline");
            panel.Add(timeline);

            Label label = new Label("Events");
            label.AddToClassList("avfx-track-label");
            timeline.Add(label);

            VisualElement trackHost = new VisualElement();
            trackHost.AddToClassList("avfx-track-host");
            timeline.Add(trackHost);

            eventTrack = new VisualElement();
            eventTrack.AddToClassList("avfx-track");
            eventTrack.RegisterCallback<PointerDownEvent>(HandleTrackPointerDown);
            trackHost.Add(eventTrack);

            playhead = new VisualElement();
            playhead.AddToClassList("avfx-playhead");
            trackHost.Add(playhead);
        }

        private void BuildRightPanel(VisualElement body)
        {
            VisualElement panel = new VisualElement();
            panel.AddToClassList("avfx-right-panel");
            body.Add(panel);

            clipInfoLabel = new Label("Clip: None");
            clipInfoLabel.AddToClassList("avfx-clip-info");
            panel.Add(clipInfoLabel);

            panel.Add(CreateSectionLabel("Event Inspector"));
            eventInspector = new VisualElement();
            eventInspector.AddToClassList("avfx-inspector");
            panel.Add(eventInspector);

            panel.Add(CreateSectionLabel("VFX Transform"));
            vfxInspector = new VisualElement();
            vfxInspector.AddToClassList("avfx-inspector");
            panel.Add(vfxInspector);
        }

        private ObjectField CreateObjectField(string label, Type objectType, bool allowSceneObjects)
        {
            ObjectField field = new ObjectField(label)
            {
                objectType = objectType,
                allowSceneObjects = allowSceneObjects
            };

            field.AddToClassList("avfx-object-field");
            return field;
        }

        private ListView CreateListView(float minimumHeight)
        {
            ListView listView = new ListView
            {
                fixedItemHeight = 28f,
                selectionType = SelectionType.Single,
                showAlternatingRowBackgrounds = AlternatingRowBackground.ContentOnly,
                style =
                {
                    flexGrow = 0f,
                    flexShrink = 0f,
                    height = minimumHeight,
                    minHeight = minimumHeight
                }
            };

            listView.AddToClassList("avfx-list");
            return listView;
        }

        private Label CreateSectionLabel(string text)
        {
            Label label = new Label(text);
            label.AddToClassList("avfx-section-label");
            return label;
        }

        private void HandleUnitySelectionChanged()
        {
            UseCurrentSelectionIfPossible();
        }

        private void UseCurrentSelectionIfPossible()
        {
            GameObject selected = Selection.activeGameObject;
            if (selected == null)
            {
                UpdateStatus("No hierarchy object is selected.");
                return;
            }

            GameObject resolvedCharacter = ResolveCharacterRoot(selected);
            if (resolvedCharacter == null)
            {
                UpdateStatus($"Selection '{selected.name}' does not contain an Animator.");
                return;
            }

            SelectCharacter(resolvedCharacter, selected);
        }

        private void SelectCharacterFromField(GameObject selectedCharacter)
        {
            if (selectedCharacter == null)
            {
                character = null;
                selectedClip = null;
                selectedEvent = null;
                selectedVfx = null;
                RefreshAll();
                return;
            }

            SelectCharacter(selectedCharacter, selectedCharacter);
        }

        private void SelectCharacter(GameObject selectedCharacter, GameObject preferredSelection)
        {
            GameObject resolvedCharacter = ResolveCharacterRoot(selectedCharacter);
            if (resolvedCharacter == null)
            {
                UpdateStatus("Pick a scene object with an Animator in itself or its children.");
                return;
            }

            character = resolvedCharacter;
            characterField.SetValueWithoutNotify(character);

            RefreshClips();
            SelectDefaultClipIfNeeded();
            LoadEventsFromClip();
            ConfigureTimeControls();
            RebuildEventList();
            RefreshTimeline();
            RefreshVfxComponents();
            SelectDefaultVfx(preferredSelection);
            RebuildInspectors();

            previewRenderer.SetCharacter(character);
            previewRenderer.SetClip(selectedClip);
            previewRenderer.SetSelectedVfx(selectedVfx != null ? selectedVfx.transform : null);
            previewContainer?.MarkDirtyRepaint();
            UpdateStatus($"Loaded '{character.name}' ({clips.Count} clips, {vfxComponents.Count} VFX).");
        }

        private GameObject ResolveCharacterRoot(GameObject selected)
        {
            if (selected == null || EditorUtility.IsPersistent(selected))
                return null;

            Transform current = selected.transform;
            while (current != null)
            {
                if (current.GetComponentInChildren<Animator>(true) != null)
                    return current.gameObject;

                current = current.parent;
            }

            Animator parentAnimator = selected.GetComponentInParent<Animator>(true);
            return parentAnimator != null ? parentAnimator.gameObject : null;
        }

        private Component ResolveVfxComponent(GameObject selected)
        {
            if (selected == null || character == null)
                return null;

            Transform current = selected.transform;
            while (current != null && current.IsChildOf(character.transform))
            {
                MonoBehaviour[] behaviours = current.GetComponents<MonoBehaviour>();
                foreach (MonoBehaviour behaviour in behaviours)
                {
                    if (behaviour is IPlayableVFX)
                        return behaviour;
                }

                current = current.parent;
            }

            MonoBehaviour[] childBehaviours = selected.GetComponentsInChildren<MonoBehaviour>(true);
            foreach (MonoBehaviour behaviour in childBehaviours)
            {
                if (behaviour is IPlayableVFX)
                    return behaviour;
            }

            return null;
        }

        private void SelectClip(AnimationClip clip)
        {
            selectedClip = clip;
            selectedEvent = null;
            currentTime = 0f;

            clipField.SetValueWithoutNotify(selectedClip);
            LoadEventsFromClip();
            ConfigureTimeControls();
            RebuildEventList();
            RefreshTimeline();
            RebuildInspectors();
            previewRenderer.SetClip(selectedClip);
            previewContainer?.MarkDirtyRepaint();
        }

        private void RefreshAll()
        {
            RefreshClips();
            SelectDefaultClipIfNeeded();
            LoadEventsFromClip();
            RefreshVfxComponents();
            SelectDefaultVfx(Selection.activeGameObject);
            ConfigureTimeControls();
            RebuildEventList();
            RefreshTimeline();
            RebuildInspectors();
            UpdateSummary();

            previewRenderer.SetCharacter(character);
            previewRenderer.SetClip(selectedClip);
            previewRenderer.SetSelectedVfx(selectedVfx != null ? selectedVfx.transform : null);
            previewContainer?.MarkDirtyRepaint();
        }

        private void RefreshClips()
        {
            clips.Clear();

            if (character != null)
            {
                Animator animator = character.GetComponentInChildren<Animator>(true);
                if (animator != null && animator.runtimeAnimatorController != null)
                    clips.AddRange(animator.runtimeAnimatorController.animationClips);
            }

            List<AnimationClip> sortedClips = clips
                .Where(clip => clip != null)
                .GroupBy(clip => clip.GetInstanceID())
                .Select(group => group.First())
                .OrderBy(clip => clip.name)
                .ToList();

            clips.Clear();
            clips.AddRange(sortedClips);

            if (clipList == null)
                return;

            clipList.itemsSource = clips;
            clipList.Rebuild();
        }

        private void SelectDefaultClipIfNeeded()
        {
            if (selectedClip == null || !clips.Contains(selectedClip))
                selectedClip = clips.Count > 0 ? clips[0] : null;

            clipField?.SetValueWithoutNotify(selectedClip);

            if (clipList == null)
                return;

            int index = selectedClip != null ? clips.IndexOf(selectedClip) : -1;
            if (index >= 0)
                clipList.SetSelection(index);
            else
                clipList.ClearSelection();
        }

        private void LoadEventsFromClip()
        {
            animationEvents.Clear();
            selectedEvent = null;

            if (selectedClip != null)
                animationEvents.AddRange(AnimationUtility.GetAnimationEvents(selectedClip));

            SortEvents();
        }

        private void RebuildEventList()
        {
            if (eventList == null)
                return;

            eventList.itemsSource = animationEvents;
            eventList.Rebuild();

            int selectedIndex = selectedEvent != null ? animationEvents.IndexOf(selectedEvent) : -1;
            if (selectedIndex >= 0)
                eventList.SetSelection(selectedIndex);
            else
                eventList.ClearSelection();
        }

        private void RefreshVfxComponents()
        {
            vfxComponents.Clear();

            if (character != null)
            {
                MonoBehaviour[] components = character.GetComponentsInChildren<MonoBehaviour>(true);
                foreach (MonoBehaviour component in components)
                {
                    if (component is IPlayableVFX)
                        vfxComponents.Add(component);
                }
            }

            if (vfxList == null)
                return;

            vfxList.itemsSource = vfxComponents;
            vfxList.Rebuild();
        }

        private void SelectDefaultVfx(GameObject preferredSelection)
        {
            Component preferred = ResolveVfxComponent(preferredSelection);
            if (preferred != null && vfxComponents.Contains(preferred))
                selectedVfx = preferred;
            else if (selectedVfx == null || !vfxComponents.Contains(selectedVfx))
                selectedVfx = vfxComponents.Count > 0 ? vfxComponents[0] : null;

            if (vfxList == null)
                return;

            int index = selectedVfx != null ? vfxComponents.IndexOf(selectedVfx) : -1;
            if (index >= 0)
                vfxList.SetSelection(index);
            else
                vfxList.ClearSelection();
        }

        private void ConfigureTimeControls()
        {
            float clipLength = selectedClip != null ? Mathf.Max(0.0001f, selectedClip.length) : 1f;

            if (timeSlider != null)
            {
                timeSlider.lowValue = 0f;
                timeSlider.highValue = clipLength;
                timeSlider.SetValueWithoutNotify(currentTime);
            }

            if (timeField != null)
                timeField.SetValueWithoutNotify(currentTime);

            if (clipInfoLabel != null)
            {
                string clipName = selectedClip != null ? selectedClip.name : "None";
                float length = selectedClip != null ? selectedClip.length : 0f;
                clipInfoLabel.text = $"Clip: {clipName}  |  {length:0.000}s";
            }
        }

        private void BindClipItem(VisualElement element, int index)
        {
            Label label = (Label)element;
            label.text = clips[index] != null ? clips[index].name : "(Missing Clip)";
        }

        private void BindEventItem(VisualElement element, int index)
        {
            Label label = (Label)element;
            AnimationEvent animationEvent = animationEvents[index];
            string functionName = string.IsNullOrEmpty(animationEvent.functionName) ? "(No Function)" : animationEvent.functionName;
            label.text = $"{animationEvent.time:0.000}s  {functionName}";
        }

        private void BindVfxItem(VisualElement element, int index)
        {
            Label label = (Label)element;
            Component component = vfxComponents[index];
            IPlayableVFX playableVfx = component as IPlayableVFX;
            string assetName = playableVfx != null && playableVfx.VfxName != null ? playableVfx.VfxName.name : "No VfxName";
            label.text = $"{component.gameObject.name}  ({assetName})";
        }

        private void HandleClipSelectionChanged(IEnumerable<object> selectedItems)
        {
            AnimationClip clip = selectedItems.OfType<AnimationClip>().FirstOrDefault();
            if (clip != null && clip != selectedClip)
                SelectClip(clip);
        }

        private void HandleEventSelectionChanged(IEnumerable<object> selectedItems)
        {
            selectedEvent = selectedItems.OfType<AnimationEvent>().FirstOrDefault();
            RebuildInspectors();
            RefreshTimeline();
        }

        private void HandleVfxSelectionChanged(IEnumerable<object> selectedItems)
        {
            selectedVfx = selectedItems.OfType<Component>().FirstOrDefault();

            if (selectedVfx != null && Selection.activeGameObject != selectedVfx.gameObject)
            {
                Selection.activeObject = selectedVfx.gameObject;
                EditorGUIUtility.PingObject(selectedVfx.gameObject);
            }

            previewRenderer.SetSelectedVfx(selectedVfx != null ? selectedVfx.transform : null);
            RebuildInspectors();
            previewContainer?.MarkDirtyRepaint();
        }

        private void AddGenericEvent()
        {
            if (selectedClip == null)
            {
                EditorUtility.DisplayDialog("Animation VFX Event Editor", "Select an Animation Clip first.", "OK");
                return;
            }

            AnimationEvent animationEvent = new AnimationEvent
            {
                time = Mathf.Clamp(currentTime, 0f, selectedClip.length),
                functionName = "AnimationEndTrigger"
            };

            animationEvents.Add(animationEvent);
            selectedEvent = animationEvent;
            SaveEvents();
            RebuildEventList();
            RefreshTimeline();
            RebuildInspectors();
        }

        private void AddVfxInvokeEvent()
        {
            if (selectedClip == null)
            {
                EditorUtility.DisplayDialog("Animation VFX Event Editor", "Select an Animation Clip first.", "OK");
                return;
            }

            if (selectedVfx == null)
            {
                EditorUtility.DisplayDialog("Animation VFX Event Editor", "Select a Child VFX first.", "OK");
                return;
            }

            IPlayableVFX playableVfx = selectedVfx as IPlayableVFX;
            if (playableVfx == null || playableVfx.VfxName == null)
            {
                EditorUtility.DisplayDialog("Animation VFX Event Editor", "The selected VFX does not have a VfxName asset.", "OK");
                return;
            }

            AnimationEvent animationEvent = new AnimationEvent
            {
                time = Mathf.Clamp(currentTime, 0f, selectedClip.length),
                functionName = "ExecuteVFX",
                objectReferenceParameter = playableVfx.VfxName
            };

            animationEvents.Add(animationEvent);
            selectedEvent = animationEvent;
            SaveEvents();
            RebuildEventList();
            RefreshTimeline();
            RebuildInspectors();
        }

        private void DeleteSelectedEvent()
        {
            if (selectedEvent == null)
                return;

            animationEvents.Remove(selectedEvent);
            selectedEvent = null;
            SaveEvents();
            RebuildEventList();
            RefreshTimeline();
            RebuildInspectors();
        }

        private void RebuildInspectors()
        {
            RebuildEventInspector();
            RebuildVfxInspector();
            UpdateSummary();
        }

        private void RebuildEventInspector()
        {
            eventInspector.Clear();

            if (selectedEvent == null)
            {
                eventInspector.Add(new Label("No event selected."));
                return;
            }

            FloatField time = new FloatField("Time");
            time.SetValueWithoutNotify(selectedEvent.time);
            time.RegisterValueChangedCallback(changeEvent => SetEventTime(selectedEvent, changeEvent.newValue, true));
            eventInspector.Add(time);

            DropdownField functionPreset = new DropdownField("Preset");
            functionPreset.choices = GetFunctionPresets();
            functionPreset.SetValueWithoutNotify(selectedEvent.functionName);
            functionPreset.RegisterValueChangedCallback(changeEvent =>
            {
                selectedEvent.functionName = changeEvent.newValue;
                SaveEvents();
                RebuildEventList();
                RefreshTimeline();
            });
            eventInspector.Add(functionPreset);

            TextField function = new TextField("Function");
            function.SetValueWithoutNotify(selectedEvent.functionName);
            function.RegisterValueChangedCallback(changeEvent =>
            {
                selectedEvent.functionName = changeEvent.newValue;
                SaveEvents();
                RebuildEventList();
                RefreshTimeline();
            });
            eventInspector.Add(function);

            TextField stringParameter = new TextField("String");
            stringParameter.SetValueWithoutNotify(selectedEvent.stringParameter);
            stringParameter.RegisterValueChangedCallback(changeEvent =>
            {
                selectedEvent.stringParameter = changeEvent.newValue;
                SaveEvents();
            });
            eventInspector.Add(stringParameter);

            FloatField floatParameter = new FloatField("Float");
            floatParameter.SetValueWithoutNotify(selectedEvent.floatParameter);
            floatParameter.RegisterValueChangedCallback(changeEvent =>
            {
                selectedEvent.floatParameter = changeEvent.newValue;
                SaveEvents();
            });
            eventInspector.Add(floatParameter);

            IntegerField intParameter = new IntegerField("Int");
            intParameter.SetValueWithoutNotify(selectedEvent.intParameter);
            intParameter.RegisterValueChangedCallback(changeEvent =>
            {
                selectedEvent.intParameter = changeEvent.newValue;
                SaveEvents();
            });
            eventInspector.Add(intParameter);

            ObjectField objectParameter = CreateObjectField("Object", typeof(Object), false);
            objectParameter.SetValueWithoutNotify(selectedEvent.objectReferenceParameter);
            objectParameter.RegisterValueChangedCallback(changeEvent =>
            {
                selectedEvent.objectReferenceParameter = changeEvent.newValue;
                SaveEvents();
                previewContainer?.MarkDirtyRepaint();
            });
            eventInspector.Add(objectParameter);

            Button deleteButton = new Button(DeleteSelectedEvent) { text = "Delete Event" };
            deleteButton.AddToClassList("avfx-danger-button");
            eventInspector.Add(deleteButton);
        }

        private void RebuildVfxInspector()
        {
            vfxInspector.Clear();
            vfxLocalPositionField = null;
            vfxLocalRotationField = null;
            vfxLocalScaleField = null;

            if (selectedVfx == null)
            {
                vfxInspector.Add(new Label("No child VFX selected."));
                return;
            }

            IPlayableVFX playableVfx = selectedVfx as IPlayableVFX;
            ObjectField nameAsset = CreateObjectField("VfxName", typeof(Object), false);
            nameAsset.SetValueWithoutNotify(playableVfx != null ? playableVfx.VfxName : null);
            nameAsset.SetEnabled(false);
            vfxInspector.Add(nameAsset);

            Label parentLabel = new Label($"Parent: {GetTransformParentName(selectedVfx.transform)}");
            parentLabel.AddToClassList("avfx-parent-label");
            vfxInspector.Add(parentLabel);

            vfxLocalPositionField = new Vector3Field("Position");
            vfxLocalPositionField.RegisterValueChangedCallback(changeEvent => RecordVfxTransform(selectedVfx.transform, changeEvent.newValue, GetInspectorLocalEulerAngles(selectedVfx.transform), GetInspectorLocalScale(selectedVfx.transform)));
            vfxInspector.Add(vfxLocalPositionField);

            vfxLocalRotationField = new Vector3Field("Rotation");
            vfxLocalRotationField.RegisterValueChangedCallback(changeEvent => RecordVfxTransform(selectedVfx.transform, GetInspectorLocalPosition(selectedVfx.transform), changeEvent.newValue, GetInspectorLocalScale(selectedVfx.transform)));
            vfxInspector.Add(vfxLocalRotationField);

            vfxLocalScaleField = new Vector3Field("Scale");
            vfxLocalScaleField.RegisterValueChangedCallback(changeEvent => RecordVfxTransform(selectedVfx.transform, GetInspectorLocalPosition(selectedVfx.transform), GetInspectorLocalEulerAngles(selectedVfx.transform), changeEvent.newValue));
            vfxInspector.Add(vfxLocalScaleField);

            SyncVfxTransformFields(true);

            Button selectButton = new Button(() => Selection.activeObject = selectedVfx.gameObject) { text = "Select In Hierarchy" };
            selectButton.AddToClassList("avfx-button");
            vfxInspector.Add(selectButton);
        }

        private List<string> GetFunctionPresets()
        {
            return new List<string>
            {
                "AnimationEndTrigger",
                "ExecuteVFX",
                "DamageCastTrigger",
                "LinkTimeTrigger",
                "CanManualMovementTrigger"
            };
        }

        private void RecordVfxTransform(Transform target, Vector3 localPosition, Vector3 localEulerAngles, Vector3 localScale)
        {
            if (target == null)
                return;

            Undo.RecordObject(target, "Edit VFX Transform");
            target.localPosition = localPosition;
            target.localEulerAngles = localEulerAngles;
            target.localScale = localScale;
            WriteInspectorLocalEulerAnglesHint(target, localEulerAngles);
            EditorUtility.SetDirty(target);
            PrefabUtility.RecordPrefabInstancePropertyModifications(target);
            previewRenderer.ForceRebuild();
            previewRenderer.SetSelectedVfx(target);
            SyncVfxTransformFields(true);
            previewContainer?.MarkDirtyRepaint();
        }

        private void HandlePreviewVfxTransformChanged(Transform changedTransform)
        {
            if (changedTransform == null || selectedVfx == null || changedTransform != selectedVfx.transform)
                return;

            SyncVfxTransformFields(true);
            previewContainer?.MarkDirtyRepaint();
        }

        private void HandleUndoRedoPerformed()
        {
            SyncVfxTransformFields(true);
            previewRenderer.ForceRebuild();
            previewContainer?.MarkDirtyRepaint();
        }

        private void SyncVfxTransformFields(bool force)
        {
            if (selectedVfx == null)
                return;

            Transform target = selectedVfx.transform;
            Vector3 position = GetInspectorLocalPosition(target);
            Vector3 rotation = GetInspectorLocalEulerAngles(target);
            Vector3 scale = GetInspectorLocalScale(target);

            bool changed = force
                           || position != lastSyncedVfxPosition
                           || rotation != lastSyncedVfxRotation
                           || scale != lastSyncedVfxScale;

            if (!changed)
                return;

            vfxLocalPositionField?.SetValueWithoutNotify(position);
            vfxLocalRotationField?.SetValueWithoutNotify(rotation);
            vfxLocalScaleField?.SetValueWithoutNotify(scale);

            lastSyncedVfxPosition = position;
            lastSyncedVfxRotation = rotation;
            lastSyncedVfxScale = scale;

            previewContainer?.MarkDirtyRepaint();
        }

        private string GetTransformParentName(Transform target)
        {
            if (target == null || target.parent == null)
                return "(None)";

            return target.parent.name;
        }

        private Vector3 GetInspectorLocalPosition(Transform target)
        {
            SerializedObject serializedTransform = new SerializedObject(target);
            SerializedProperty property = serializedTransform.FindProperty("m_LocalPosition");
            return property != null ? property.vector3Value : target.localPosition;
        }

        private Vector3 GetInspectorLocalEulerAngles(Transform target)
        {
            SerializedObject serializedTransform = new SerializedObject(target);
            SerializedProperty property = serializedTransform.FindProperty("m_LocalEulerAnglesHint");
            return property != null ? property.vector3Value : target.localEulerAngles;
        }

        private Vector3 GetInspectorLocalScale(Transform target)
        {
            SerializedObject serializedTransform = new SerializedObject(target);
            SerializedProperty property = serializedTransform.FindProperty("m_LocalScale");
            return property != null ? property.vector3Value : target.localScale;
        }

        private void WriteInspectorLocalEulerAnglesHint(Transform target, Vector3 localEulerAngles)
        {
            SerializedObject serializedTransform = new SerializedObject(target);
            SerializedProperty property = serializedTransform.FindProperty("m_LocalEulerAnglesHint");
            if (property == null)
                return;

            property.vector3Value = localEulerAngles;
            serializedTransform.ApplyModifiedPropertiesWithoutUndo();
        }

        private void SaveEvents()
        {
            if (selectedClip == null)
                return;

            SortEvents();

            try
            {
                Undo.RegisterCompleteObjectUndo(selectedClip, "Edit Animation Events");
                AnimationUtility.SetAnimationEvents(selectedClip, animationEvents.ToArray());
                EditorUtility.SetDirty(selectedClip);
                AssetDatabase.SaveAssets();
            }
            catch (Exception exception)
            {
                EditorUtility.DisplayDialog("Animation VFX Event Editor", $"Failed to save Animation Events.\n{exception.Message}", "OK");
            }
        }

        private void SortEvents()
        {
            animationEvents.Sort((left, right) => left.time.CompareTo(right.time));
        }

        private void SetEventTime(AnimationEvent animationEvent, float time, bool save)
        {
            if (selectedClip == null || animationEvent == null)
                return;

            animationEvent.time = Mathf.Clamp(time, 0f, selectedClip.length);
            currentTime = animationEvent.time;

            if (save)
                SaveEvents();

            RebuildEventList();
            RefreshTimeline();
            RebuildInspectors();
            SetCurrentTime(currentTime, true);
        }

        private void SetCurrentTime(float time, bool repaint)
        {
            float clipLength = selectedClip != null ? selectedClip.length : 1f;
            currentTime = Mathf.Clamp(time, 0f, Mathf.Max(0f, clipLength));

            timeSlider?.SetValueWithoutNotify(currentTime);
            timeField?.SetValueWithoutNotify(currentTime);
            RefreshPlayhead();

            if (repaint)
                previewContainer?.MarkDirtyRepaint();
        }

        private void SetPlaying(bool playing)
        {
            isPlaying = playing;
            lastUpdateTime = EditorApplication.timeSinceStartup;

            if (playButton != null)
                playButton.text = isPlaying ? "Pause" : "Play";
        }

        private void TogglePlayback()
        {
            SetPlaying(!isPlaying);
        }

        private void StopPlayback()
        {
            SetPlaying(false);
            SetCurrentTime(0f, true);
        }

        private void HandleEditorUpdate()
        {
            double now = EditorApplication.timeSinceStartup;
            double delta = now - lastUpdateTime;
            lastUpdateTime = now;

            SyncVfxTransformFields(false);

            if (!isPlaying || selectedClip == null)
                return;

            float nextTime = currentTime + (float)delta;
            if (nextTime > selectedClip.length)
            {
                if (loopToggle != null && loopToggle.value)
                    nextTime %= Mathf.Max(0.0001f, selectedClip.length);
                else
                {
                    nextTime = selectedClip.length;
                    SetPlaying(false);
                }
            }

            SetCurrentTime(nextTime, true);
        }

        private void RefreshTimeline()
        {
            if (eventTrack == null)
                return;

            eventTrack.Clear();

            if (selectedClip == null)
            {
                RefreshPlayhead();
                return;
            }

            foreach (AnimationEvent animationEvent in animationEvents)
            {
                eventTrack.Add(CreateEventMarker(animationEvent));
            }

            RefreshPlayhead();
        }

        private VisualElement CreateEventMarker(AnimationEvent animationEvent)
        {
            VisualElement marker = new VisualElement();
            marker.AddToClassList("avfx-marker");

            if (animationEvent == selectedEvent)
                marker.AddToClassList("avfx-marker-selected");

            if (animationEvent.functionName == "ExecuteVFX")
                marker.AddToClassList("avfx-marker-vfx");
            else
                marker.AddToClassList("avfx-marker-event");

            PositionMarker(marker, animationEvent.time);
            marker.tooltip = $"{animationEvent.functionName} @ {animationEvent.time:0.000}s";

            marker.RegisterCallback<PointerDownEvent>(pointerEvent =>
            {
                selectedEvent = animationEvent;
                currentTime = animationEvent.time;
                eventList.SetSelection(animationEvents.IndexOf(animationEvent));
                RebuildInspectors();
                marker.CapturePointer(pointerEvent.pointerId);
                pointerEvent.StopPropagation();
            });

            marker.RegisterCallback<PointerMoveEvent>(pointerEvent =>
            {
                if (!marker.HasPointerCapture(pointerEvent.pointerId))
                    return;

                float time = GetTimeFromPointer(pointerEvent);
                animationEvent.time = time;
                currentTime = time;
                PositionMarker(marker, time);
                SetCurrentTime(time, true);
                pointerEvent.StopPropagation();
            });

            marker.RegisterCallback<PointerUpEvent>(pointerEvent =>
            {
                if (!marker.HasPointerCapture(pointerEvent.pointerId))
                    return;

                marker.ReleasePointer(pointerEvent.pointerId);
                SaveEvents();
                RebuildEventList();
                RefreshTimeline();
                RebuildInspectors();
                pointerEvent.StopPropagation();
            });

            return marker;
        }

        private void PositionMarker(VisualElement marker, float time)
        {
            float normalized = GetNormalizedTime(time);
            marker.style.left = Length.Percent(normalized * 100f);
        }

        private void RefreshPlayhead()
        {
            if (playhead == null)
                return;

            playhead.style.left = Length.Percent(GetNormalizedTime(currentTime) * 100f);
        }

        private float GetNormalizedTime(float time)
        {
            if (selectedClip == null || selectedClip.length <= 0f)
                return 0f;

            return Mathf.Clamp01(time / selectedClip.length);
        }

        private float GetTimeFromPointer(PointerMoveEvent pointerEvent)
        {
            Vector2 localPosition = eventTrack.WorldToLocal(pointerEvent.position);
            float width = Mathf.Max(1f, eventTrack.resolvedStyle.width);
            float normalized = Mathf.Clamp01(localPosition.x / width);
            float clipLength = selectedClip != null ? selectedClip.length : 1f;
            return normalized * clipLength;
        }

        private void HandleTrackPointerDown(PointerDownEvent pointerEvent)
        {
            if (pointerEvent.target != eventTrack || selectedClip == null)
                return;

            Vector2 localPosition = eventTrack.WorldToLocal(pointerEvent.position);
            float width = Mathf.Max(1f, eventTrack.resolvedStyle.width);
            SetCurrentTime(Mathf.Clamp01(localPosition.x / width) * selectedClip.length, true);
        }

        private void DrawPreview()
        {
            float width = Mathf.Max(1f, previewContainer.contentRect.width);
            float height = Mathf.Max(1f, previewContainer.contentRect.height);
            Rect rect = new Rect(0f, 0f, width, height);
            bool showSelectedVfx = showSelectedVfxToggle == null || showSelectedVfxToggle.value;
            previewRenderer.Draw(rect, currentTime, animationEvents.ToArray(), showSelectedVfx);
        }

        private void HandlePreviewWheel(WheelEvent wheelEvent)
        {
            previewRenderer.ZoomBy(wheelEvent.delta.y);
            previewContainer?.MarkDirtyRepaint();
            wheelEvent.StopPropagation();
        }

        private void HandlePreviewMouseDown(MouseDownEvent mouseDownEvent)
        {
            int mode = GetPreviewCameraMode(mouseDownEvent.button, mouseDownEvent.altKey);
            if (mode == 0)
                return;

            previewCameraMode = mode;
            previewCameraButton = mouseDownEvent.button;
            lastPreviewMousePosition = mouseDownEvent.mousePosition;
            previewContainer.CaptureMouse();
            previewContainer.Focus();
            mouseDownEvent.StopImmediatePropagation();
        }

        private void HandlePreviewMouseMove(MouseMoveEvent mouseMoveEvent)
        {
            if (previewCameraMode == 0 || !previewContainer.HasMouseCapture())
                return;

            Vector2 currentPosition = mouseMoveEvent.mousePosition;
            Vector2 delta = currentPosition - lastPreviewMousePosition;
            lastPreviewMousePosition = currentPosition;

            if (delta.sqrMagnitude <= 0f)
                return;

            if (previewCameraMode == 1)
                previewRenderer.OrbitCamera(delta);
            else if (previewCameraMode == 2)
                previewRenderer.PanCamera(delta);
            else if (previewCameraMode == 3)
                previewRenderer.LookCamera(delta);
            else if (previewCameraMode == 4)
                previewRenderer.ZoomBy((delta.x + delta.y) * 0.35f);

            previewContainer.MarkDirtyRepaint();
            mouseMoveEvent.StopImmediatePropagation();
        }

        private void HandlePreviewMouseUp(MouseUpEvent mouseUpEvent)
        {
            if (previewCameraMode == 0 || mouseUpEvent.button != previewCameraButton)
                return;

            previewCameraMode = 0;
            previewCameraButton = -1;

            if (previewContainer.HasMouseCapture())
                previewContainer.ReleaseMouse();

            mouseUpEvent.StopImmediatePropagation();
        }

        private int GetPreviewCameraMode(int button, bool altPressed)
        {
            if (altPressed && button == 0)
                return 1;

            if (button == 2)
                return 2;

            if (!altPressed && button == 1)
                return 3;

            if (altPressed && button == 1)
                return 4;

            return 0;
        }

        private void HandlePreviewKeyDown(KeyDownEvent keyDownEvent)
        {
            if (keyDownEvent.keyCode != KeyCode.F)
                return;

            previewRenderer.FrameSelected();
            previewContainer?.MarkDirtyRepaint();
            keyDownEvent.StopPropagation();
        }

        private void UpdateStatus(string message)
        {
            if (statusLabel != null)
                statusLabel.text = message;

            UpdateSummary();
        }

        private void UpdateSummary()
        {
            if (selectionSummaryLabel == null)
                return;

            if (character == null)
            {
                selectionSummaryLabel.text = "No scene character selected";
                return;
            }

            string clipName = selectedClip != null ? selectedClip.name : "No clip";
            string vfxName = selectedVfx != null ? selectedVfx.gameObject.name : "No VFX";
            selectionSummaryLabel.text = $"{character.name} | {clipName} | {vfxName}";
        }
    }
}
