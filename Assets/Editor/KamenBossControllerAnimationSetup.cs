using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class KamenBossControllerAnimationSetup
{
    private const string FbxPath = "Assets/KamenAsset/Models/v1/Kamen_v1.fbx";
    private const string GameScenePath = "Assets/_Scenes/GameScene.unity";
    private const string ControllerPath = "Assets/GameModules/Enemies/BossController.controller";
    private const string ReportPath = "Assets/KamenAsset/SourceMetadata/Kamen_v1_BossControllerAnimationSetupReport.md";
    private const string RootMotionReportPath = "Assets/KamenAsset/SourceMetadata/Kamen_v1_RootMotionSettingsReport.txt";
    private const string ClipListPath = "Assets/KamenAsset/SourceMetadata/Kamen_v1_EmbeddedClipNames.txt";
    private const string NonContinuousPath = "Assets/KamenAsset/SourceMetadata/Kamen_v1_NonContinuousGroups.tsv";
    private const string DuplicateClipRoot = "Assets/KamenAsset/KamenAnimClip/v1";
    private const string GeneratedDuplicateClipRoot = "Assets/KamenAsset/KamenAnimClip/v1/DuplicatedFromFbx";
    private const string DuplicateResolutionPath = "Assets/KamenAsset/SourceMetadata/Kamen_v1_DuplicateClipResolution.tsv";
    private const string ExcludedStatusEventPath = "Assets/KamenAsset/SourceMetadata/Kamen_v1_ExcludedEvtStatusClips.txt";
    private const string RootMotionNodeName = "bip001";
    private const float ContinuityThreshold = 0.05f;
    private const float ClipCompareEpsilon = 0.0001f;

    [MenuItem("Tools/Kamen/Setup BossController Animations")]
    public static void Run()
    {
        AssetDatabase.ImportAsset(FbxPath);

        var allClips = AssetDatabase.LoadAllAssetRepresentationsAtPath(FbxPath)
            .OfType<AnimationClip>()
            .Where(clip => clip != null && !clip.name.StartsWith("__preview__", StringComparison.Ordinal))
            .OrderBy(clip => clip.name, StringComparer.Ordinal)
            .ToList();

        var excludedClips = allClips
            .Where(ShouldExcludeClip)
            .OrderBy(clip => clip.name, StringComparer.Ordinal)
            .ToList();
        var clips = allClips
            .Where(clip => !ShouldExcludeClip(clip))
            .OrderBy(clip => clip.name, StringComparer.Ordinal)
            .ToList();

        if (clips.Count == 0)
        {
            throw new InvalidOperationException("No embedded animation clips were found in " + FbxPath);
        }

        var clipInfo = new Dictionary<AnimationClip, Dictionary<string, string>>();
        var groups = new Dictionary<string, List<AnimationClip>>(StringComparer.Ordinal);
        foreach (var clip in clips)
        {
            var info = ClassifyClip(clip.name);
            clipInfo[clip] = info;

            var groupKey = info["primary"] + "|" + info["group"];
            if (!groups.TryGetValue(groupKey, out var list))
            {
                list = new List<AnimationClip>();
                groups.Add(groupKey, list);
            }

            list.Add(clip);
        }

        var groupContinuity = new Dictionary<string, bool>(StringComparer.Ordinal);
        var pairReports = new List<string>();
        foreach (var group in groups.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            var ordered = OrderGroup(group.Value, clipInfo);
            var continuous = true;

            if (ordered.Count <= 1)
            {
                pairReports.Add(group.Key + "\t(single)\tPASS\t0\t0");
            }
            else
            {
                for (var i = 0; i < ordered.Count - 1; i++)
                {
                    var current = ordered[i];
                    var next = ordered[i + 1];
                    var diff = AverageEndpointDifference(current, next, out var sampleCount);
                    var passes = diff <= ContinuityThreshold;
                    if (!passes)
                    {
                        continuous = false;
                    }

                    pairReports.Add(
                        group.Key + "\t" +
                        CleanClipName(current.name) + " -> " + CleanClipName(next.name) + "\t" +
                        (passes ? "PASS" : "FAIL") + "\t" +
                        diff.ToString("0.######", CultureInfo.InvariantCulture) + "\t" +
                        sampleCount.ToString(CultureInfo.InvariantCulture));
                }
            }

            groupContinuity[group.Key] = continuous;
        }

        var duplicateReports = new List<string>();
        var motionClips = ResolveMotionClips(clips, clipInfo, duplicateReports);

        SetupController(groups, clipInfo, groupContinuity, motionClips);
        WriteReports(clips, excludedClips, groups, clipInfo, groupContinuity, pairReports, duplicateReports);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            "KamenBossControllerAnimationSetup complete. " +
            "AllEmbeddedClips=" + allClips.Count +
            ", UsedClips=" + clips.Count +
            ", ExcludedEvtStatusClips=" + excludedClips.Count +
            ", Groups=" + groups.Count +
            ", Continuous=" + groupContinuity.Count(pair => pair.Value) +
            ", NonContinuous=" + groupContinuity.Count(pair => !pair.Value) +
            ", DuplicateMatches=" + duplicateReports.Count(line => line.IndexOf("\texisting-match\t", StringComparison.Ordinal) >= 0) +
            ", DuplicateCreated=" + duplicateReports.Count(line => line.IndexOf("\tcreated\t", StringComparison.Ordinal) >= 0) +
            ", Threshold=" + ContinuityThreshold.ToString(CultureInfo.InvariantCulture));
    }

    [MenuItem("Tools/Kamen/Apply Root Motion Settings")]
    public static void ApplyRootMotionSettings()
    {
        var importer = AssetImporter.GetAtPath(FbxPath) as ModelImporter;
        if (importer == null)
        {
            throw new InvalidOperationException("Kamen_v1.fbx ModelImporter was not found.");
        }

        var humanDescription = importer.humanDescription;
        var previousTranslationDoF = humanDescription.hasTranslationDoF;
        var previousMotionNodeName = importer.motionNodeName;
        var previousRootMotionBoneName = ReadModelMetaValue("rootMotionBoneName");
        importer.motionNodeName = RootMotionNodeName;
        humanDescription.hasTranslationDoF = true;
        importer.humanDescription = humanDescription;
        importer.SaveAndReimport();

        var scene = EditorSceneManager.OpenScene(GameScenePath);
        var boss = GameObject.Find("BossEnemy");
        var visual = boss != null ? boss.transform.Find("Visual_v1") : null;
        if (visual == null)
        {
            throw new InvalidOperationException("BossEnemy/Visual_v1 was not found in " + GameScenePath);
        }

        var animator = visual.GetComponent<Animator>();
        if (animator == null)
        {
            throw new InvalidOperationException("BossEnemy/Visual_v1 does not have an Animator.");
        }

        animator.applyRootMotion = true;
        EditorUtility.SetDirty(animator);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        var clips = AssetDatabase.LoadAllAssetRepresentationsAtPath(FbxPath)
            .OfType<AnimationClip>()
            .Where(clip => clip != null && !clip.name.StartsWith("__preview__", StringComparison.Ordinal))
            .OrderBy(clip => clip.name, StringComparer.Ordinal)
            .ToList();

        var report = new StringBuilder();
        report.AppendLine("Kamen v1 Root Motion Settings Report");
        report.AppendLine("FBX: " + FbxPath);
        report.AppendLine("Scene: " + GameScenePath);
        report.AppendLine("Previous hasTranslationDoF: " + previousTranslationDoF);
        report.AppendLine("Previous motionNodeName: " + previousMotionNodeName);
        report.AppendLine("Previous rootMotionBoneName: " + previousRootMotionBoneName);
        report.AppendLine("Current hasTranslationDoF: " + importer.humanDescription.hasTranslationDoF);
        report.AppendLine("Current motionNodeName: " + importer.motionNodeName);
        report.AppendLine("Current rootMotionBoneName: " + ReadModelMetaValue("rootMotionBoneName"));
        report.AppendLine("Visual_v1 Animator.applyRootMotion: " + animator.applyRootMotion);
        report.AppendLine("Clip count: " + clips.Count.ToString(CultureInfo.InvariantCulture));
        report.AppendLine("Clips with root curves: " + clips.Count(clip => clip.hasRootCurves).ToString(CultureInfo.InvariantCulture));
        report.AppendLine("Clips with motion curves: " + clips.Count(clip => clip.hasMotionCurves).ToString(CultureInfo.InvariantCulture));
        report.AppendLine("Clips with generic root transform: " + clips.Count(clip => clip.hasGenericRootTransform).ToString(CultureInfo.InvariantCulture));
        File.WriteAllText(RootMotionReportPath, report.ToString(), Encoding.UTF8);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(report.ToString());
    }

    private static string ReadModelMetaValue(string key)
    {
        var metaPath = FbxPath + ".meta";
        if (!File.Exists(metaPath))
        {
            return string.Empty;
        }

        var prefix = key + ":";
        foreach (var line in File.ReadAllLines(metaPath))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith(prefix, StringComparison.Ordinal))
            {
                return trimmed.Substring(prefix.Length).Trim();
            }
        }

        return string.Empty;
    }

    private static Dictionary<string, string> ClassifyClip(string rawName)
    {
        var name = CleanClipName(rawName);
        var source = "unknown";
        var tail = name;
        var marker = name.IndexOf("_ani__", StringComparison.Ordinal);
        if (marker >= 0)
        {
            source = name.Substring(0, marker);
            tail = name.Substring(marker + "_ani__".Length);
        }

        var tokens = tail.Split(new[] { '_' }, StringSplitOptions.RemoveEmptyEntries);
        var primary = tokens.Length > 0 ? NormalizePrimary(tokens[0]) : "other";
        var group = source + "_" + tail;
        var sequence = 1;

        if (TryBuildPhaseGroup(source, tokens, out var phaseGroup, out var phaseSequence))
        {
            if (tokens.Length >= 2 && tokens[0] == "att")
            {
                primary = NormalizePrimary(tokens[1]);
            }

            group = phaseGroup;
            sequence = phaseSequence;
        }
        else if (tokens.Length >= 4 && tokens[0] == "att")
        {
            primary = NormalizePrimary(tokens[1]);
            var suffix = tokens.Length >= 5 ? "_" + string.Join("_", tokens.Skip(4)) : string.Empty;
            group = source + "_" + tokens[1] + "_" + tokens[2] + suffix;
            sequence = ParseIntOrDefault(tokens[3], 1);
        }
        else if (tokens.Length >= 5 && tokens[0] == "idle" && IsIdlePhase(tokens[2]))
        {
            primary = "idle";
            group = source + "_" + tokens[0] + "_" + tokens[1] + "_" + tokens[3] + "_" + tokens[4];
            sequence = IdlePhaseOrder(tokens[2]);
        }
        else
        {
            var suffixIndex = tokens.Length > 0 && IsVariantSuffix(tokens[tokens.Length - 1]) ? tokens.Length - 1 : -1;
            var lastNumericIndex = -1;
            for (var i = (suffixIndex >= 0 ? suffixIndex - 1 : tokens.Length - 1); i >= 0; i--)
            {
                if (int.TryParse(tokens[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out sequence))
                {
                    lastNumericIndex = i;
                    break;
                }
            }

            if (lastNumericIndex >= 0)
            {
                var groupTokens = tokens.Take(lastNumericIndex + 1).ToList();
                if (suffixIndex >= 0)
                {
                    groupTokens.Add(tokens[suffixIndex]);
                }

                group = source + "_" + string.Join("_", groupTokens);
            }
        }

        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["name"] = name,
            ["fullName"] = "Kamen_v1|" + name,
            ["source"] = source,
            ["tail"] = tail,
            ["primary"] = primary,
            ["group"] = group,
            ["sequence"] = sequence.ToString(CultureInfo.InvariantCulture)
        };
    }

    private static bool TryBuildPhaseGroup(
        string source,
        string[] tokens,
        out string group,
        out int sequence)
    {
        group = string.Empty;
        sequence = 1;

        var suffixIndex = tokens.Length > 0 && IsVariantSuffix(tokens[tokens.Length - 1]) ? tokens.Length - 1 : -1;
        var searchEnd = suffixIndex >= 0 ? suffixIndex - 1 : tokens.Length - 1;
        for (var phaseIndex = 0; phaseIndex <= searchEnd; phaseIndex++)
        {
            if (!IsPhaseToken(tokens, phaseIndex))
            {
                continue;
            }

            var numericIndex = -1;
            var numericSequence = 1;
            for (var i = phaseIndex + 1; i <= searchEnd; i++)
            {
                if (int.TryParse(tokens[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out numericSequence))
                {
                    numericIndex = i;
                    break;
                }
            }

            if (numericIndex < 0)
            {
                continue;
            }

            var groupTokens = new List<string>();
            var startIndex = tokens.Length >= 2 && tokens[0] == "att" ? 1 : 0;
            for (var i = startIndex; i <= searchEnd; i++)
            {
                if (i != phaseIndex)
                {
                    groupTokens.Add(tokens[i]);
                }
            }

            if (suffixIndex >= 0)
            {
                groupTokens.Add(tokens[suffixIndex]);
            }

            group = source + "_" + string.Join("_", groupTokens);
            sequence = PhaseOrder(tokens[phaseIndex]) * 1000 + numericSequence;
            return true;
        }

        return false;
    }

    private static string NormalizePrimary(string token)
    {
        if (string.IsNullOrEmpty(token))
        {
            return "other";
        }

        if (token.StartsWith("status", StringComparison.OrdinalIgnoreCase))
        {
            return "status";
        }

        return token.ToLowerInvariant();
    }

    private static bool IsIdlePhase(string token)
    {
        return token == "start" || token == "loop" || token == "end" || token == "normal";
    }

    private static bool IsPhaseToken(string[] tokens, int index)
    {
        var token = tokens[index];
        return token == "start" ||
               token == "loop" ||
               token == "end" ||
               (tokens.Length > 0 && tokens[0] == "idle" && token == "normal");
    }

    private static int IdlePhaseOrder(string token)
    {
        if (token == "start")
        {
            return PhaseOrder(token);
        }

        if (token == "normal")
        {
            return 2;
        }

        if (token == "loop")
        {
            return PhaseOrder(token);
        }

        if (token == "end")
        {
            return PhaseOrder(token);
        }

        return 1;
    }

    private static int PhaseOrder(string token)
    {
        if (token == "start")
        {
            return 1;
        }

        if (token == "normal")
        {
            return 2;
        }

        if (token == "loop")
        {
            return 3;
        }

        if (token == "end")
        {
            return 4;
        }

        return 1;
    }

    private static bool IsVariantSuffix(string token)
    {
        return token == "S" || token == "M";
    }

    private static int ParseIntOrDefault(string value, int fallback)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result)
            ? result
            : fallback;
    }

    private static List<AnimationClip> OrderGroup(
        IEnumerable<AnimationClip> group,
        Dictionary<AnimationClip, Dictionary<string, string>> clipInfo)
    {
        return group
            .OrderBy(clip => ParseIntOrDefault(clipInfo[clip]["sequence"], 1))
            .ThenBy(clip => clipInfo[clip]["name"], StringComparer.Ordinal)
            .ToList();
    }

    private static float AverageEndpointDifference(AnimationClip current, AnimationClip next, out int sampleCount)
    {
        var currentSample = SampleTransformCurves(current, current.length);
        var nextSample = SampleTransformCurves(next, 0f);
        var total = 0f;
        sampleCount = 0;

        foreach (var pair in currentSample)
        {
            if (!nextSample.TryGetValue(pair.Key, out var nextValue))
            {
                continue;
            }

            total += Mathf.Abs(pair.Value - nextValue);
            sampleCount++;
        }

        if (sampleCount == 0)
        {
            return float.PositiveInfinity;
        }

        return total / sampleCount;
    }

    private static Dictionary<string, float> SampleTransformCurves(AnimationClip clip, float time)
    {
        var result = new Dictionary<string, float>(StringComparer.Ordinal);
        foreach (var binding in AnimationUtility.GetCurveBindings(clip))
        {
            if (!IsTransformKey(binding.propertyName))
            {
                continue;
            }

            var curve = AnimationUtility.GetEditorCurve(clip, binding);
            if (curve == null)
            {
                continue;
            }

            result[binding.path + "|" + binding.propertyName] = curve.Evaluate(time);
        }

        return result;
    }

    private static bool IsTransformKey(string propertyName)
    {
        return propertyName.StartsWith("m_LocalPosition.", StringComparison.Ordinal) ||
               propertyName.StartsWith("m_LocalRotation.", StringComparison.Ordinal) ||
               propertyName.StartsWith("localEulerAnglesRaw.", StringComparison.Ordinal);
    }

    private static void SetupController(
        Dictionary<string, List<AnimationClip>> groups,
        Dictionary<AnimationClip, Dictionary<string, string>> clipInfo,
        Dictionary<string, bool> groupContinuity,
        Dictionary<AnimationClip, AnimationClip> motionClips)
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
        {
            throw new InvalidOperationException("BossController.controller was not found at " + ControllerPath);
        }

        while (controller.layers.Length > 1)
        {
            controller.RemoveLayer(controller.layers.Length - 1);
        }

        var orderedCategories = groups.Keys
            .Select(key => key.Split('|')[0])
            .Distinct()
            .OrderBy(CategorySortOrder)
            .ThenBy(key => key, StringComparer.Ordinal)
            .ToList();

        if (orderedCategories.Count == 0)
        {
            throw new InvalidOperationException("No animation categories were found.");
        }

        var layers = controller.layers;
        layers[0].name = "Base Layer";
        layers[0].defaultWeight = 1f;
        ClearStateMachine(layers[0].stateMachine);
        layers[0].stateMachine.name = "Base Layer";
        controller.layers = layers;

        var baseStateMachine = controller.layers[0].stateMachine;
        for (var i = 0; i < orderedCategories.Count; i++)
        {
            var category = orderedCategories[i];
            var categoryStateMachine = baseStateMachine.AddStateMachine(
                ToLayerName(category),
                new Vector3(260f * (i % 4), 160f * (i / 4), 0f));
            BuildLayer(categoryStateMachine, category, groups, clipInfo, groupContinuity, motionClips);
        }

        EditorUtility.SetDirty(controller);
    }

    private static int CategorySortOrder(string category)
    {
        if (category == "battle")
        {
            return 0;
        }

        if (category == "run")
        {
            return 1;
        }

        if (category == "idle")
        {
            return 2;
        }

        if (category == "status")
        {
            return 3;
        }

        return 10;
    }

    private static string ToLayerName(string category)
    {
        return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(category.Replace("_", " "));
    }

    private static void ClearStateMachine(AnimatorStateMachine machine)
    {
        foreach (var state in machine.states.ToArray())
        {
            machine.RemoveState(state.state);
        }

        foreach (var childMachine in machine.stateMachines.ToArray())
        {
            ClearStateMachine(childMachine.stateMachine);
            machine.RemoveStateMachine(childMachine.stateMachine);
        }

        machine.anyStateTransitions = Array.Empty<AnimatorStateTransition>();
        machine.entryTransitions = Array.Empty<AnimatorTransition>();
        machine.defaultState = null;
    }

    private static void BuildLayer(
        AnimatorStateMachine root,
        string category,
        Dictionary<string, List<AnimationClip>> groups,
        Dictionary<AnimationClip, Dictionary<string, string>> clipInfo,
        Dictionary<string, bool> groupContinuity,
        Dictionary<AnimationClip, AnimationClip> motionClips)
    {
        root.name = ToLayerName(category);
        var continuousRoot = root.AddStateMachine("ContinuousGroups", new Vector3(250f, 100f, 0f));
        var nonContinuousRoot = root.AddStateMachine("NonContinuousGroups", new Vector3(250f, 260f, 0f));
        var continuousIndex = 0;
        var nonContinuousIndex = 0;

        foreach (var pair in groups
                     .Where(pair => pair.Key.StartsWith(category + "|", StringComparison.Ordinal))
                     .OrderBy(pair => NaturalGroupName(pair.Key), StringComparer.Ordinal))
        {
            var continuous = groupContinuity[pair.Key];
            var parent = continuous ? continuousRoot : nonContinuousRoot;
            var index = continuous ? continuousIndex++ : nonContinuousIndex++;
            var groupMachine = parent.AddStateMachine(
                SanitizeStateName(NaturalGroupName(pair.Key)),
                new Vector3(260f * (index % 4), 120f * (index / 4), 0f));
            var ordered = OrderGroup(pair.Value, clipInfo);
            var states = new List<AnimatorState>();

            for (var i = 0; i < ordered.Count; i++)
            {
                var clip = ordered[i];
                var state = groupMachine.AddState(
                    SanitizeStateName(clipInfo[clip]["name"]),
                    new Vector3(260f * (i % 4), 80f * (i / 4), 0f));
                state.motion = motionClips.TryGetValue(clip, out var motionClip) ? motionClip : clip;
                state.writeDefaultValues = true;
                state.tag = continuous ? "Continuous" : "NonContinuous";
                states.Add(state);
            }

            if (states.Count > 0)
            {
                groupMachine.defaultState = states[0];
            }

            if (!continuous)
            {
                continue;
            }

            for (var i = 0; i < states.Count - 1; i++)
            {
                var transition = states[i].AddTransition(states[i + 1]);
                transition.hasExitTime = true;
                transition.exitTime = 1f;
                transition.duration = 0f;
                transition.hasFixedDuration = false;
                transition.canTransitionToSelf = false;
                transition.interruptionSource = TransitionInterruptionSource.None;
                transition.orderedInterruption = true;
            }
        }
    }

    private static string NaturalGroupName(string groupKey)
    {
        var separator = groupKey.IndexOf('|');
        return separator >= 0 ? groupKey.Substring(separator + 1) : groupKey;
    }

    private static string SanitizeStateName(string value)
    {
        var invalidCharacters = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            builder.Append(character == '|' || invalidCharacters.Contains(character) ? '_' : character);
        }

        return builder.ToString();
    }

    private static string CleanClipName(string rawName)
    {
        var separator = rawName.LastIndexOf('|');
        return separator >= 0 ? rawName.Substring(separator + 1) : rawName;
    }

    private static bool ShouldExcludeStatusEventClip(AnimationClip clip)
    {
        var name = CleanClipName(clip.name);
        return name.IndexOf("evt", StringComparison.OrdinalIgnoreCase) >= 0 &&
               name.IndexOf("status", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool ShouldExcludeMountedClip(AnimationClip clip)
    {
        var name = CleanClipName(clip.name);
        return name.StartsWith("mn_cdkcn_02", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ShouldExcludeClip(AnimationClip clip)
    {
        return ShouldExcludeStatusEventClip(clip) || ShouldExcludeMountedClip(clip);
    }

    private static Dictionary<AnimationClip, AnimationClip> ResolveMotionClips(
        List<AnimationClip> sourceClips,
        Dictionary<AnimationClip, Dictionary<string, string>> clipInfo,
        List<string> duplicateReports)
    {
        var result = new Dictionary<AnimationClip, AnimationClip>();
        var duplicateClips = LoadDuplicateClipsByNormalizedName();

        duplicateReports.Add("source_clip\tresult\tmotion_path\tnote");
        foreach (var sourceClip in sourceClips)
        {
            var sourceName = clipInfo[sourceClip]["name"];
            var matched = false;
            var candidateCount = 0;

            if (duplicateClips.TryGetValue(sourceName, out var candidates))
            {
                candidateCount = candidates.Count;
                foreach (var candidate in candidates.OrderBy(AssetDatabase.GetAssetPath, StringComparer.Ordinal))
                {
                    if (!AnimationClipsMatch(sourceClip, candidate))
                    {
                        continue;
                    }

                    result[sourceClip] = candidate;
                    duplicateReports.Add(
                        sourceName + "\texisting-match\t" +
                        AssetDatabase.GetAssetPath(candidate) + "\tcandidate=" + candidate.name);
                    matched = true;
                    break;
                }
            }

            if (matched)
            {
                continue;
            }

            var duplicate = CreateDuplicateClip(sourceClip, clipInfo[sourceClip], candidateCount, out var status, out var path);
            result[sourceClip] = duplicate;
            duplicateReports.Add(sourceName + "\t" + status + "\t" + path + "\tcandidates=" + candidateCount.ToString(CultureInfo.InvariantCulture));
        }

        return result;
    }

    private static Dictionary<string, List<AnimationClip>> LoadDuplicateClipsByNormalizedName()
    {
        var result = new Dictionary<string, List<AnimationClip>>(StringComparer.Ordinal);
        var guids = AssetDatabase.FindAssets("t:AnimationClip", new[] { DuplicateClipRoot });
        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.EndsWith(".anim", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null)
            {
                continue;
            }

            var key = NormalizeDuplicateClipName(clip.name);
            if (!result.TryGetValue(key, out var clips))
            {
                clips = new List<AnimationClip>();
                result.Add(key, clips);
            }

            clips.Add(clip);
        }

        return result;
    }

    private static string NormalizeDuplicateClipName(string rawName)
    {
        var name = CleanClipName(rawName);
        if (name.EndsWith("_S", StringComparison.Ordinal) || name.EndsWith("_M", StringComparison.Ordinal))
        {
            return name.Substring(0, name.Length - 2);
        }

        return name;
    }

    private static AnimationClip CreateDuplicateClip(
        AnimationClip sourceClip,
        Dictionary<string, string> info,
        int candidateCount,
        out string status,
        out string path)
    {
        path = GetGeneratedDuplicatePath(info);
        var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (existing != null && AnimationClipsMatch(sourceClip, existing))
        {
            status = "existing-generated-match";
            return existing;
        }

        EnsureAssetDirectory(path);

        if (existing != null || candidateCount > 0)
        {
            var uniquePath = AssetDatabase.GenerateUniqueAssetPath(path);
            if (!string.IsNullOrEmpty(uniquePath))
            {
                path = uniquePath;
            }
        }

        EnsureAssetDirectory(path);

        var duplicate = new AnimationClip();
        EditorUtility.CopySerialized(sourceClip, duplicate);
        duplicate.name = info["name"];
        AssetDatabase.CreateAsset(duplicate, path);
        AssetDatabase.ImportAsset(path);

        status = "created";
        return AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
    }

    private static void EnsureAssetDirectory(string assetPath)
    {
        var directorySeparator = assetPath.LastIndexOf('/');
        if (directorySeparator <= 0)
        {
            return;
        }

        var directory = assetPath.Substring(0, directorySeparator);
        Directory.CreateDirectory(directory);
    }

    private static string GetGeneratedDuplicatePath(Dictionary<string, string> info)
    {
        return GeneratedDuplicateClipRoot + "/" +
               SanitizeStateName(ToLayerName(info["primary"])) + "/" +
               SanitizeStateName(info["group"]) + "/" +
               SanitizeStateName(info["name"]) + ".anim";
    }

    private static bool AnimationClipsMatch(AnimationClip sourceClip, AnimationClip candidateClip)
    {
        if (sourceClip == null || candidateClip == null)
        {
            return false;
        }

        if (!Approximately(sourceClip.length, candidateClip.length) ||
            !Approximately(sourceClip.frameRate, candidateClip.frameRate))
        {
            return false;
        }

        if (sourceClip.hasRootCurves != candidateClip.hasRootCurves ||
            sourceClip.hasMotionCurves != candidateClip.hasMotionCurves ||
            sourceClip.hasGenericRootTransform != candidateClip.hasGenericRootTransform)
        {
            return false;
        }

        if (!EditorCurvesMatch(sourceClip, candidateClip))
        {
            return false;
        }

        if (!ObjectReferenceCurvesMatch(sourceClip, candidateClip))
        {
            return false;
        }

        return EventsMatch(sourceClip, candidateClip);
    }

    private static bool EditorCurvesMatch(AnimationClip sourceClip, AnimationClip candidateClip)
    {
        var sourceBindings = AnimationUtility.GetCurveBindings(sourceClip)
            .OrderBy(BindingKey, StringComparer.Ordinal)
            .ToArray();
        var candidateBindings = AnimationUtility.GetCurveBindings(candidateClip)
            .OrderBy(BindingKey, StringComparer.Ordinal)
            .ToArray();

        if (sourceBindings.Length != candidateBindings.Length)
        {
            return false;
        }

        for (var i = 0; i < sourceBindings.Length; i++)
        {
            if (BindingKey(sourceBindings[i]) != BindingKey(candidateBindings[i]))
            {
                return false;
            }

            var sourceCurve = AnimationUtility.GetEditorCurve(sourceClip, sourceBindings[i]);
            var candidateCurve = AnimationUtility.GetEditorCurve(candidateClip, candidateBindings[i]);
            if (!CurvesMatch(sourceCurve, candidateCurve))
            {
                return false;
            }
        }

        return true;
    }

    private static bool ObjectReferenceCurvesMatch(AnimationClip sourceClip, AnimationClip candidateClip)
    {
        var sourceBindings = AnimationUtility.GetObjectReferenceCurveBindings(sourceClip)
            .OrderBy(BindingKey, StringComparer.Ordinal)
            .ToArray();
        var candidateBindings = AnimationUtility.GetObjectReferenceCurveBindings(candidateClip)
            .OrderBy(BindingKey, StringComparer.Ordinal)
            .ToArray();

        if (sourceBindings.Length != candidateBindings.Length)
        {
            return false;
        }

        for (var i = 0; i < sourceBindings.Length; i++)
        {
            if (BindingKey(sourceBindings[i]) != BindingKey(candidateBindings[i]))
            {
                return false;
            }

            var sourceCurve = AnimationUtility.GetObjectReferenceCurve(sourceClip, sourceBindings[i]) ?? Array.Empty<ObjectReferenceKeyframe>();
            var candidateCurve = AnimationUtility.GetObjectReferenceCurve(candidateClip, candidateBindings[i]) ?? Array.Empty<ObjectReferenceKeyframe>();
            if (sourceCurve.Length != candidateCurve.Length)
            {
                return false;
            }

            for (var keyIndex = 0; keyIndex < sourceCurve.Length; keyIndex++)
            {
                if (!Approximately(sourceCurve[keyIndex].time, candidateCurve[keyIndex].time) ||
                    sourceCurve[keyIndex].value != candidateCurve[keyIndex].value)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static bool CurvesMatch(AnimationCurve sourceCurve, AnimationCurve candidateCurve)
    {
        if (sourceCurve == null || candidateCurve == null)
        {
            return sourceCurve == candidateCurve;
        }

        if (sourceCurve.preWrapMode != candidateCurve.preWrapMode ||
            sourceCurve.postWrapMode != candidateCurve.postWrapMode ||
            sourceCurve.keys.Length != candidateCurve.keys.Length)
        {
            return false;
        }

        for (var i = 0; i < sourceCurve.keys.Length; i++)
        {
            var sourceKey = sourceCurve.keys[i];
            var candidateKey = candidateCurve.keys[i];
            if (!Approximately(sourceKey.time, candidateKey.time) ||
                !Approximately(sourceKey.value, candidateKey.value) ||
                !Approximately(sourceKey.inTangent, candidateKey.inTangent) ||
                !Approximately(sourceKey.outTangent, candidateKey.outTangent) ||
                !Approximately(sourceKey.inWeight, candidateKey.inWeight) ||
                !Approximately(sourceKey.outWeight, candidateKey.outWeight) ||
                sourceKey.weightedMode != candidateKey.weightedMode)
            {
                return false;
            }
        }

        return true;
    }

    private static bool EventsMatch(AnimationClip sourceClip, AnimationClip candidateClip)
    {
        var sourceEvents = AnimationUtility.GetAnimationEvents(sourceClip);
        var candidateEvents = AnimationUtility.GetAnimationEvents(candidateClip);
        if (sourceEvents.Length != candidateEvents.Length)
        {
            return false;
        }

        for (var i = 0; i < sourceEvents.Length; i++)
        {
            var sourceEvent = sourceEvents[i];
            var candidateEvent = candidateEvents[i];
            if (!Approximately(sourceEvent.time, candidateEvent.time) ||
                sourceEvent.functionName != candidateEvent.functionName ||
                sourceEvent.stringParameter != candidateEvent.stringParameter ||
                !Approximately(sourceEvent.floatParameter, candidateEvent.floatParameter) ||
                sourceEvent.intParameter != candidateEvent.intParameter ||
                sourceEvent.objectReferenceParameter != candidateEvent.objectReferenceParameter)
            {
                return false;
            }
        }

        return true;
    }

    private static string BindingKey(EditorCurveBinding binding)
    {
        return binding.path + "|" +
               (binding.type == null ? string.Empty : binding.type.FullName) + "|" +
               binding.propertyName;
    }

    private static bool Approximately(float left, float right)
    {
        return Mathf.Abs(left - right) <= ClipCompareEpsilon;
    }

    private static void WriteReports(
        List<AnimationClip> clips,
        List<AnimationClip> excludedClips,
        Dictionary<string, List<AnimationClip>> groups,
        Dictionary<AnimationClip, Dictionary<string, string>> clipInfo,
        Dictionary<string, bool> groupContinuity,
        List<string> pairReports,
        List<string> duplicateReports)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));

        File.WriteAllLines(
            ClipListPath,
            clips.Select(clip => clipInfo[clip]["fullName"]).OrderBy(name => name, StringComparer.Ordinal));

        File.WriteAllLines(
            ExcludedStatusEventPath,
            excludedClips.Select(clip => "Kamen_v1|" + CleanClipName(clip.name)).OrderBy(name => name, StringComparer.Ordinal));

        File.WriteAllLines(DuplicateResolutionPath, duplicateReports);

        File.WriteAllLines(
            NonContinuousPath,
            new[] { "category\tgroup\tclip_count\tclips" }.Concat(
                groups
                    .Where(pair => !groupContinuity[pair.Key])
                    .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                    .Select(pair =>
                    {
                        var category = pair.Key.Split('|')[0];
                        var groupName = NaturalGroupName(pair.Key);
                        var ordered = OrderGroup(pair.Value, clipInfo);
                        return category + "\t" +
                               groupName + "\t" +
                               ordered.Count.ToString(CultureInfo.InvariantCulture) + "\t" +
                               string.Join(", ", ordered.Select(clip => clipInfo[clip]["name"]));
                    })));

        var report = new StringBuilder();
        report.AppendLine("# Kamen v1 BossController Animation Setup Report");
        report.AppendLine();
        report.AppendLine("- FBX: `" + FbxPath + "`");
        report.AppendLine("- Animator Controller: `" + ControllerPath + "`");
        report.AppendLine("- Used embedded clip count: " + clips.Count.ToString(CultureInfo.InvariantCulture));
        report.AppendLine("- Excluded evt/status clip count: " + excludedClips.Count.ToString(CultureInfo.InvariantCulture));
        report.AppendLine("- Duplicate existing matches: " + duplicateReports.Count(line => line.IndexOf("\texisting-match\t", StringComparison.Ordinal) >= 0).ToString(CultureInfo.InvariantCulture));
        report.AppendLine("- Duplicate newly created: " + duplicateReports.Count(line => line.IndexOf("\tcreated\t", StringComparison.Ordinal) >= 0).ToString(CultureInfo.InvariantCulture));
        report.AppendLine("- Group count: " + groups.Count.ToString(CultureInfo.InvariantCulture));
        report.AppendLine("- Continuity threshold: " + ContinuityThreshold.ToString(CultureInfo.InvariantCulture));
        report.AppendLine("- Continuous groups: " + groupContinuity.Count(pair => pair.Value).ToString(CultureInfo.InvariantCulture));
        report.AppendLine("- Non-continuous groups: " + groupContinuity.Count(pair => !pair.Value).ToString(CultureInfo.InvariantCulture));
        report.AppendLine();
        report.AppendLine("## Categories");
        foreach (var category in groups.Keys.Select(key => key.Split('|')[0]).Distinct().OrderBy(CategorySortOrder).ThenBy(key => key, StringComparer.Ordinal))
        {
            var categoryGroups = groups.Where(pair => pair.Key.StartsWith(category + "|", StringComparison.Ordinal)).ToList();
            report.AppendLine("- " + category + ": " +
                              categoryGroups.Count.ToString(CultureInfo.InvariantCulture) + " groups, " +
                              categoryGroups.Sum(pair => pair.Value.Count).ToString(CultureInfo.InvariantCulture) + " clips");
        }

        report.AppendLine();
        report.AppendLine("## Groups");
        foreach (var pair in groups.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            var ordered = OrderGroup(pair.Value, clipInfo);
            report.AppendLine("- " + pair.Key + ": " +
                              (groupContinuity[pair.Key] ? "Continuous" : "NonContinuous") +
                              ", clips=" + ordered.Count.ToString(CultureInfo.InvariantCulture) +
                              ", sequence=" + string.Join(" -> ", ordered.Select(clip => clipInfo[clip]["name"])));
        }

        report.AppendLine();
        report.AppendLine("## Pair Diffs");
        report.AppendLine("group\tpair\tresult\taverage_diff\tsample_count");
        foreach (var line in pairReports)
        {
            report.AppendLine(line);
        }

        report.AppendLine();
        report.AppendLine("## Excluded Evt Status Clips");
        foreach (var clip in excludedClips.OrderBy(clip => clip.name, StringComparer.Ordinal))
        {
            report.AppendLine("- Kamen_v1|" + CleanClipName(clip.name));
        }

        report.AppendLine();
        report.AppendLine("## Duplicate Clip Resolution");
        report.AppendLine("source_clip\tresult\tmotion_path\tnote");
        foreach (var line in duplicateReports.Skip(1))
        {
            report.AppendLine(line);
        }

        report.AppendLine();
        report.AppendLine("## Full Clip Names");
        foreach (var clip in clips.OrderBy(clip => clipInfo[clip]["fullName"], StringComparer.Ordinal))
        {
            report.AppendLine("- " + clipInfo[clip]["fullName"]);
        }

        File.WriteAllText(ReportPath, report.ToString());
    }
}
