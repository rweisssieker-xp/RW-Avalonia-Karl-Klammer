using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CarolusNexus.Models;

namespace CarolusNexus.Services;

/// <summary>Optionaler Closed-Loop: Screenshot/UIA → Modell → Aktion, mit hartem Step-Limit (Phase D, Demo).</summary>
public static class ComputerUseLoopService
{
    public sealed record StepResult(int Index, string Observation, string ModelReply, string ActionSummary);
    public sealed record ObserveOnlySnapshot(
        DateTimeOffset CapturedAt,
        string ForegroundTitle,
        string ForegroundProcess,
        string AdapterFamily,
        string Risk,
        string SuggestedAction,
        string Why,
        string UiaSnapshot);

    public static ObserveOnlySnapshot ObserveOnly(NexusSettings? settings)
    {
        settings ??= new NexusSettings();
        if (!OperatingSystem.IsWindows())
        {
            return new ObserveOnlySnapshot(
                DateTimeOffset.Now,
                "Windows only",
                "unknown",
                "generic",
                "observe-only",
                "No action",
                "Computer-use observe mode requires Windows foreground-window APIs.",
                "");
        }

        var detail = ForegroundWindowInfo.TryReadDetail();
        var title = detail?.Title ?? "(no foreground window)";
        var process = detail?.ProcessName ?? "unknown";
        var adapter = detail == null
            ? "generic"
            : OperatorAdapterRegistry.ResolveFamily(detail.Value.ProcessName, detail.Value.Title);
        var uia = UiAutomationSnapshot.TryBuildForForeground(maxDepth: 3, maxNodes: 48, maxChars: 5000);
        var insight = OperatorInsightService.BuildSnapshot(settings);
        var risk = string.Equals(settings.Safety.Profile, "power-user", StringComparison.OrdinalIgnoreCase)
            ? "guarded"
            : "observe-only";
        var suggested = string.IsNullOrWhiteSpace(insight.SafeNextAction) ? "No safe action" : insight.SafeNextAction;
        var why =
            $"{insight.LikelyTask}\n" +
            $"Recommended flow: {insight.RecommendedFlow}\n" +
            $"Risky action: {insight.RiskyAction}\n" +
            "Mode: observe-only. No click, typing, send, post, booking or file write is executed.";

        NexusShell.Log($"[computer-use] observe-only: {process} · {adapter} · risk={risk}");
        return new ObserveOnlySnapshot(DateTimeOffset.Now, title, process, adapter, risk, suggested, why, uia);
    }

    public static string FormatObserveOnly(ObserveOnlySnapshot snapshot)
    {
        var sb = new StringBuilder();
        sb.AppendLine("[Computer-use observe-only]");
        sb.AppendLine($"captured: {snapshot.CapturedAt:O}");
        sb.AppendLine($"foreground: {snapshot.ForegroundProcess} · {snapshot.ForegroundTitle}");
        sb.AppendLine($"adapter: {snapshot.AdapterFamily}");
        sb.AppendLine($"risk: {snapshot.Risk}");
        sb.AppendLine($"suggested action: {snapshot.SuggestedAction}");
        sb.AppendLine();
        sb.AppendLine("why:");
        sb.AppendLine(snapshot.Why);
        sb.AppendLine();
        sb.AppendLine("uia:");
        sb.AppendLine(string.IsNullOrWhiteSpace(snapshot.UiaSnapshot) ? "(no UIA snapshot)" : snapshot.UiaSnapshot);
        return sb.ToString().TrimEnd();
    }

    /// <summary>Harmless fallback steps — kept only when no foreground observation can produce a better guarded plan.</summary>
    public static IReadOnlyList<RecipeStep> SampleTierCPlanSteps() =>
    [
        new()
        {
            ActionType = "computer_use",
            ActionArgument = "tier-c sample step 1 (observation placeholder)",
            WaitMs = 20
        },
        new()
        {
            ActionType = "computer_use",
            ActionArgument = "tier-c sample step 2",
            WaitMs = 20
        },
        new()
        {
            ActionType = "computer_use",
            ActionArgument = "tier-c sample step 3 — audit",
            WaitMs = 20
        }
    ];

    public static IReadOnlyList<RecipeStep> BuildObservedPlanSteps(NexusSettings? settings)
    {
        settings ??= new NexusSettings();
        var snapshot = ObserveOnly(settings);
        var steps = new List<RecipeStep>
        {
            new()
            {
                ActionType = "token",
                ActionArgument = snapshot.AdapterFamily == "ax2012" ? "ax.read_context" : "[ACTION:hotkey|Ctrl+L]",
                WaitMs = 50,
                Checkpoint = true
            }
        };

        if (snapshot.AdapterFamily == "browser")
        {
            steps.Add(new RecipeStep
            {
                ActionType = "token",
                ActionArgument = "[ACTION:hotkey|Ctrl+L]",
                WaitMs = 50,
                Checkpoint = true
            });
        }

        if (steps.Count == 0)
            return SampleTierCPlanSteps();
        return steps;
    }

    /// <summary>
    /// Runs up to <paramref name="maxSteps"/> steps through <see cref="SimplePlanSimulator"/> (AgentRunState + <see cref="RitualStepAudit"/> + action history).
    /// </summary>
    public static Task<string> RunThroughSimulatorAsync(
        IReadOnlyList<RecipeStep>? steps,
        int maxSteps,
        bool dryRun,
        NexusSettings? settings,
        CancellationToken ct = default)
    {
        var list = steps is { Count: > 0 } ? steps : BuildObservedPlanSteps(settings);
        var cap = Math.Clamp(maxSteps, 1, 64);
        var slice = list.Take(Math.Min(cap, list.Count)).ToList();
        NexusShell.Log($"[computer-use] RunThroughSimulator: {slice.Count} step(s), dryRun={dryRun}");
        return SimplePlanSimulator.RunAsync(slice, dryRun, settings, recipe: null, ct);
    }

    /// <summary>
    /// Demo-Orchestrierung: ruft <paramref name="perStep"/> bis zu <paramref name="maxSteps"/> mal auf.
    /// Echte Integration: proStep liefert Screenshot/UIA und wendet Modell-Aktion an.
    /// </summary>
    public static async Task<string> RunDemoAsync(
        int maxSteps,
        Func<int, CancellationToken, Task<(string observation, string actionDone)>> perStep,
        CancellationToken ct = default)
    {
        if (maxSteps < 1)
            maxSteps = 1;
        var sb = new StringBuilder();
        sb.AppendLine("[Computer-use demo loop]");
        for (var i = 0; i < maxSteps; i++)
        {
            ct.ThrowIfCancellationRequested();
            var (obs, act) = await perStep(i, ct).ConfigureAwait(false);
            sb.AppendLine($"step {i + 1}/{maxSteps}: obs={obs}; action={act}");
            NexusShell.Log($"computer-use demo step {i + 1}: {act}");
        }

        sb.AppendLine("done (perStep callback). For plan runtime + audit use RunThroughSimulatorAsync.");
        return sb.ToString().Trim();
    }
}
