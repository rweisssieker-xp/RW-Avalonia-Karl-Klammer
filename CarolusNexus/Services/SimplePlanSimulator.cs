using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CarolusNexus.Models;

namespace CarolusNexus.Services;

public static class SimplePlanSimulator
{
    public static async Task<string> RunAsync(
        IReadOnlyList<RecipeStep> steps,
        bool dryRun,
        NexusSettings? safety,
        CancellationToken ct = default)
    {
        var sb = new StringBuilder();
        var prefix = dryRun ? "[DRY-RUN]" : "[RUN]";
        var executeReal = !dryRun
                          && safety != null
                          && string.Equals(safety.Safety.Profile, "power-user",
                              System.StringComparison.OrdinalIgnoreCase)
                          && System.OperatingSystem.IsWindows();

        for (var i = 0; i < steps.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var s = steps[i];
            var line =
                $"{prefix} step {i + 1}/{steps.Count}: {s.ActionType} · {s.ActionArgument} (wait {s.WaitMs}ms)";
            sb.AppendLine(line);
            NexusShell.Log(line);

            string stepResult;
            if (dryRun)
            {
                stepResult = "[DRY-RUN]";
            }
            else if (executeReal && safety != null)
            {
                if (!PlanGuard.IsAllowed(safety, s.ActionArgument))
                {
                    sb.AppendLine("  → [BLOCKED] Safety-Policy");
                    NexusShell.Log("  → [BLOCKED] Safety-Policy");
                    stepResult = "[BLOCKED] Safety-Policy";
                }
                else
                {
                    string msg;
                    if (NexusContext.RunWin32StepOnUiThreadAsync != null)
                    {
                        msg = await NexusContext.RunWin32StepOnUiThreadAsync(() =>
                                Win32AutomationExecutor.Execute(s, safety))
                            .ConfigureAwait(false);
                    }
                    else
                        msg = Win32AutomationExecutor.Execute(s, safety);

                    sb.AppendLine("  → " + msg);
                    NexusShell.Log("  → " + msg);
                    stepResult = msg;
                }
            }
            else
            {
                stepResult = !System.OperatingSystem.IsWindows()
                    ? "[SIM] not Windows"
                    : !string.Equals(safety?.Safety.Profile, "power-user", System.StringComparison.OrdinalIgnoreCase)
                        ? "[SIM] profile ≠ power-user — no Win32"
                        : "[SIM]";
            }

            RitualStepAudit.Append(i + 1, steps.Count, s, dryRun, stepResult);

            if (s.WaitMs > 0)
                await Task.Delay(s.WaitMs, ct).ConfigureAwait(false);
        }

        if (steps.Count == 0)
            sb.AppendLine("(no steps)");
        if (!dryRun && safety != null &&
            !string.Equals(safety.Safety.Profile, "power-user", System.StringComparison.OrdinalIgnoreCase))
            sb.AppendLine("(Note: real execution only with safety profile “power-user”.)");

        var logText = sb.ToString().TrimEnd();
        ActionHistoryService.AppendPlanRun(steps, dryRun, logText.Length > 4000 ? logText[..4000] + "…" : logText);

        return logText;
    }

    public static List<RecipeStep> ParsePlanPreviewLines(string planText)
    {
        var steps = new List<RecipeStep>();
        if (string.IsNullOrWhiteSpace(planText))
            return steps;

        foreach (var line in planText.Split('\n'))
        {
            var t = line.Trim();
            if (t.Length == 0)
                continue;
            var dot = t.IndexOf(". ");
            if (dot > 0 && int.TryParse(t[..dot], out _))
                t = t[(dot + 2)..].Trim();
            if (t.StartsWith('(') && (t.Contains("keine Action", System.StringComparison.Ordinal) || t.Contains("no action", System.StringComparison.OrdinalIgnoreCase)))
                continue;
            steps.Add(new RecipeStep { ActionType = "token", ActionArgument = t, WaitMs = 0 });
        }

        return steps;
    }
}
