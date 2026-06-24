using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.UIElements;

namespace SkillEditor.Editor
{
    public sealed class SkillTimelinePanel
    {
        private readonly SkillPreviewSceneContext context;
        private readonly Foldout root;
        private readonly Button playPauseButton;
        private readonly Button stopButton;
        private readonly Toggle loopToggle;
        private readonly Slider timeSlider;
        private readonly FloatField timeField;
        private readonly Label timeLabel;
        private readonly Label normalizedLabel;
        private readonly Label warningLabel;
        private AnimationClip clip;
        private double lastUpdateTime;
        private float currentTime;
        private bool playing;
        private bool animationModeStarted;
        private bool externalAnimationModeWarningLogged;

        public event Action Sampled;

        public SkillTimelinePanel(SkillPreviewSceneContext context)
        {
            this.context = context;

            root = new Foldout
            {
                text = "Timeline / Playback",
                value = true
            };
            root.AddToClassList("skill-panel");
            root.AddToClassList("skill-timeline-panel");

            VisualElement transportRow = new VisualElement();
            transportRow.AddToClassList("skill-row");
            root.Add(transportRow);

            playPauseButton = new Button(TogglePlayback) { text = "Play" };
            stopButton = new Button(Stop) { text = "Stop" };
            loopToggle = new Toggle("Loop");
            transportRow.Add(playPauseButton);
            transportRow.Add(stopButton);
            transportRow.Add(loopToggle);

            timeSlider = new Slider(0f, 1f);
            timeSlider.AddToClassList("skill-time-slider");
            timeSlider.RegisterValueChangedCallback(evt => SetTime(evt.newValue, true));
            root.Add(timeSlider);

            VisualElement labelRow = new VisualElement();
            labelRow.AddToClassList("skill-row");
            root.Add(labelRow);

            timeField = new FloatField("Seconds");
            timeField.AddToClassList("skill-time-field");
            timeField.RegisterValueChangedCallback(evt => SetTime(evt.newValue, true));
            timeLabel = new Label("Time: 0.000s");
            normalizedLabel = new Label("Normalized: 0.000");
            labelRow.Add(timeField);
            labelRow.Add(timeLabel);
            labelRow.Add(normalizedLabel);

            warningLabel = new Label();
            warningLabel.AddToClassList("skill-warning-label");
            root.Add(warningLabel);

            lastUpdateTime = EditorApplication.timeSinceStartup;
            UpdateControls();
        }

        public VisualElement Root => root;
        public float CurrentTime => currentTime;

        public void SetClip(AnimationClip nextClip)
        {
            clip = nextClip;
            currentTime = 0f;
            playing = false;
            externalAnimationModeWarningLogged = false;
            UpdateControls();
            SetTime(0f, true);
        }

        public void Tick()
        {
            double now = EditorApplication.timeSinceStartup;
            double delta = now - lastUpdateTime;
            lastUpdateTime = now;

            if (!playing || clip == null)
                return;

            float nextTime = currentTime + (float)delta;
            if (nextTime > clip.length)
            {
                if (loopToggle.value && clip.length > 0f)
                {
                    nextTime %= clip.length;
                }
                else
                {
                    nextTime = clip.length;
                    playing = false;
                }
            }

            SetTime(nextTime, true);
            UpdateControls();
        }

        public void Dispose()
        {
            if (animationModeStarted)
            {
                AnimationMode.StopAnimationMode();
                animationModeStarted = false;
            }
        }

        public void SetTime(float time, bool sample)
        {
            float length = clip != null ? clip.length : 0f;
            currentTime = Mathf.Clamp(time, 0f, Mathf.Max(0f, length));
            timeSlider.SetValueWithoutNotify(currentTime);
            UpdateTimeLabels();

            if (sample)
                SampleCurrentTime();
        }

        private void TogglePlayback()
        {
            if (clip == null)
                return;

            playing = !playing;
            lastUpdateTime = EditorApplication.timeSinceStartup;
            UpdateControls();
        }

        private void Stop()
        {
            playing = false;
            SetTime(0f, true);
            UpdateControls();
        }

        private void SampleCurrentTime()
        {
            GameObject sampleRoot = context.AnimationSampleRoot;
            if (clip == null || sampleRoot == null || context.PreviewRoot == null)
                return;

            if (!EnsureAnimationMode())
                return;

            try
            {
                AnimationMode.BeginSampling();
                AnimationMode.SampleAnimationClip(sampleRoot, clip, currentTime);
                SampleAnimatorWithPlayable(currentTime);
            }
            catch (Exception exception)
            {
                Debug.LogError($"Failed to sample AnimationClip {clip.name}: {exception.Message}");
            }
            finally
            {
                AnimationMode.EndSampling();
            }

            warningLabel.text = string.Empty;
            Sampled?.Invoke();
        }

        private bool EnsureAnimationMode()
        {
            if (animationModeStarted)
                return true;

            if (AnimationMode.InAnimationMode())
            {
                warningLabel.text = "AnimationMode is already active elsewhere. Preview sampling is paused.";
                if (!externalAnimationModeWarningLogged)
                {
                    Debug.LogWarning("SkillEditor cannot start AnimationMode because another AnimationMode session is already active.");
                    externalAnimationModeWarningLogged = true;
                }

                return false;
            }

            AnimationMode.StartAnimationMode();
            animationModeStarted = true;
            return true;
        }

        private void UpdateControls()
        {
            bool hasClip = clip != null;
            playPauseButton.SetEnabled(hasClip);
            stopButton.SetEnabled(hasClip);
            timeSlider.SetEnabled(hasClip);
            timeField.SetEnabled(hasClip);
            loopToggle.SetEnabled(hasClip);
            playPauseButton.text = playing ? "Pause" : "Play";
            timeSlider.lowValue = 0f;
            timeSlider.highValue = hasClip ? Mathf.Max(0.0001f, clip.length) : 1f;
            UpdateTimeLabels();
        }

        private void UpdateTimeLabels()
        {
            float length = clip != null ? clip.length : 0f;
            float normalized = length > 0f ? Mathf.Clamp01(currentTime / length) : 0f;
            timeField.SetValueWithoutNotify(currentTime);
            timeLabel.text = $"Time: {currentTime:0.000}s";
            normalizedLabel.text = $"Normalized: {normalized:0.000}";
        }

        private void SampleAnimatorWithPlayable(float time)
        {
            Animator animator = context.PreviewAnimator;
            if (animator == null || clip == null)
                return;

            PlayableGraph graph = PlayableGraph.Create("SkillEditor Animation Preview");
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);

            AnimationClipPlayable clipPlayable = AnimationClipPlayable.Create(graph, clip);
            clipPlayable.SetApplyFootIK(false);
            clipPlayable.SetTime(time);

            AnimationPlayableOutput output = AnimationPlayableOutput.Create(graph, "Preview", animator);
            output.SetSourcePlayable(clipPlayable);

            graph.Play();
            graph.Evaluate(0f);
            graph.Destroy();
        }
    }
}
