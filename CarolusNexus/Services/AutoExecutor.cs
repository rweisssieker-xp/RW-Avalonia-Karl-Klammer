using System;
using System.Linq;
using System.IO;
using System.Text.Json;
using CarolusNexus.Models;

namespace CarolusNexus.Services;

public static class AutoExecutor
{
    public static async Task<RadicalExecutionReport> RunAsync(
        RadicalPlan plan,
        NexusSettings settings,
        bool dryRun,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        Directory.CreateDirectory(AppPaths.RadicalRunsDir);
        var runId = Guid.NewGuid().ToString("N");
        var runPath = Path.Combine(AppPaths.RadicalRunsDir, $"radical-run-{runId}.json");

        var runText = await SimplePlanSimulator.RunAsync(plan.Steps, dryRun, settings, null, ct).ConfigureAwait(false);
        var lines = runText.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var stepSummaries = lines.Select(l => l.Trim()).Where(l => !string.IsNullOrWhiteSpace(l)).ToList();

        var failedSteps = 0;
        var executedSteps = 0;
        foreach (var line in lines)
        {
            if (line.Contains("[ERR]", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("[BLOCKED]", StringComparison.OrdinalIgnoreCase))
                failedSteps++;
            else
                executedSteps++;
        }

        var completed = !lines.Any(l => l.Contains("[BLOCKED]", StringComparison.OrdinalIgnoreCase) ||
                                        l.Contains("[ERR]", StringComparison.OrdinalIgnoreCase));

        var summary = runText.Length > 2500 ? runText[..2500] + "…" : runText;

        var report = new RadicalExecutionReport
        {
            RunId = runId,
            PlanId = plan.PlanId,
            GoalText = plan.GoalText,
            DryRun = dryRun,
            SafetyProfile = settings.Safety.Profile,
            Completed = completed,
            ExecutedSteps = executedSteps,
            FailedSteps = failedSteps,
            Summary = summary,
            ReportFilePath = runPath,
            StepSummaries = stepSummaries,
            RiskLevel = plan.RiskLevel
        };

        var json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(runPath, json, ct).ConfigureAwait(false);
        return report;
    }
}
