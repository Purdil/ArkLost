using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class KamenBattleSequenceControllerRepair
{
    private const string ControllerPath = "Assets/GameModules/Enemies/BossController.controller";
    private static readonly Regex BattleState = new Regex(
        @"^att_battle_(?<skill>\d+)_(?<step>\d+)(?:_(?<suffix>S|M))?(?<variant>_\d+)?$",
        RegexOptions.Compiled);

    [MenuItem("Tools/Kamen/Repair Battle Sequence Transitions")]
    public static void Repair()
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
        {
            throw new InvalidOperationException("BossController.controller was not found at " + ControllerPath);
        }

        var machineReports = new List<string>();
        var transitionCount = 0;

        foreach (var layer in controller.layers)
        {
            RepairStateMachine(layer.stateMachine, machineReports, ref transitionCount);
        }

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            $"KamenBattleSequenceControllerRepair complete. Added {transitionCount} sequential transitions.\n" +
            string.Join("\n", machineReports));
    }

    private static void RepairStateMachine(AnimatorStateMachine machine, List<string> reports, ref int transitionCount)
    {
        var groups = new Dictionary<string, List<AnimatorState>>();
        foreach (var child in machine.states)
        {
            var state = child.state;
            var match = BattleState.Match(state.name);
            if (!match.Success)
                continue;

            var skill = int.Parse(match.Groups["skill"].Value);
            var variant = match.Groups["variant"].Success ? match.Groups["variant"].Value : string.Empty;
            var suffix = match.Groups["suffix"].Success ? match.Groups["suffix"].Value : "S";
            var key = $"{skill:00}|{suffix}|{variant}";
            if (!groups.TryGetValue(key, out var list))
            {
                list = new List<AnimatorState>();
                groups.Add(key, list);
            }

            state.tag = suffix == "M" ? "NoSword" : "Sword";
            list.Add(state);
        }

        foreach (var group in groups.OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            var ordered = group.Value
                .OrderBy(s => int.Parse(BattleState.Match(s.name).Groups["step"].Value))
                .ToList();

            if (ordered.Count <= 1)
                continue;

            var expectedSteps = Enumerable.Range(
                int.Parse(BattleState.Match(ordered[0].name).Groups["step"].Value),
                int.Parse(BattleState.Match(ordered[^1].name).Groups["step"].Value)
                - int.Parse(BattleState.Match(ordered[0].name).Groups["step"].Value) + 1).ToArray();
            var actualSteps = ordered.Select(s => int.Parse(BattleState.Match(s.name).Groups["step"].Value)).ToArray();
            var missing = expectedSteps.Except(actualSteps).ToArray();

            for (var i = 0; i < ordered.Count - 1; i++)
            {
                var from = ordered[i];
                var to = ordered[i + 1];
                if (HasTransitionTo(from, to))
                    continue;

                var transition = from.AddTransition(to);
                transition.hasExitTime = true;
                transition.exitTime = 0.95f;
                transition.duration = 0.04f;
                transition.hasFixedDuration = false;
                transition.canTransitionToSelf = false;
                transition.interruptionSource = TransitionInterruptionSource.None;
                transition.orderedInterruption = true;
                transitionCount++;
            }

            reports.Add(
                $"{machine.name}/{group.Key}: {string.Join(" -> ", ordered.Select(s => s.name))}" +
                (missing.Length == 0 ? string.Empty : $" | source gap visible in controller: {string.Join(",", missing.Select(v => v.ToString("00")))}"));
        }

        foreach (var childMachine in machine.stateMachines)
        {
            RepairStateMachine(childMachine.stateMachine, reports, ref transitionCount);
        }
    }

    private static bool HasTransitionTo(AnimatorState from, AnimatorState to)
    {
        return from.transitions.Any(t => t.destinationState == to);
    }
}
