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

    /// <summary>Harmless sample steps — in dry-run they only produce <see cref="SimplePlanSimulator"/> log + <see cref="RitualStepAudit"/> lines.</summary>
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
        var list = steps is { Count: > 0 } ? steps : SampleTierCPlanSteps();
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
