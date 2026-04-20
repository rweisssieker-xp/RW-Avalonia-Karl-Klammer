using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CarolusNexus.Services;

/// <summary>Optionaler Closed-Loop: Screenshot/UIA → Modell → Aktion, mit hartem Step-Limit (Phase D, Demo).</summary>
public static class ComputerUseLoopService
{
    public sealed record StepResult(int Index, string Observation, string ModelReply, string ActionSummary);

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

        sb.AppendLine("done (demo — wire perStep to SimplePlanSimulator / AutomationToolRouter / audit).");
        return sb.ToString().Trim();
    }
}
