using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CarolusNexus.Models;

namespace CarolusNexus.Services;

public static class FlowTestStudioService
{
    public static string BuildReport(AutomationRecipe recipe, NexusSettings settings)
    {
        var qa = RitualQualityGate.Validate(recipe, settings);
        var sb = new StringBuilder();
        sb.AppendLine("Flow test report");
        sb.AppendLine("Generated: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        sb.AppendLine("Flow: " + (string.IsNullOrWhiteSpace(recipe.Name) ? "(unnamed)" : recipe.Name));
        sb.AppendLine("Risk: " + recipe.RiskLevel);
        sb.AppendLine("Approval: " + recipe.ApprovalMode);
        sb.AppendLine("Adapter: " + (string.IsNullOrWhiteSpace(recipe.AdapterAffinity) ? "(none)" : recipe.AdapterAffinity));
        sb.AppendLine("Steps: " + recipe.Steps.Count);
        sb.AppendLine();
        sb.AppendLine("[Quality gate]");
        sb.AppendLine(qa.Ok ? "OK" : "BLOCKED");
        foreach (var issue in qa.Issues)
            sb.AppendLine("- " + issue);
        sb.AppendLine();
        sb.AppendLine("[Step checks]");
        for (var i = 0; i < recipe.Steps.Count; i++)
        {
            var s = recipe.Steps[i];
            var arg = s.ActionArgument ?? "";
            var flags = new List<string>();
            if (string.IsNullOrWhiteSpace(arg))
                flags.Add("empty");
            if (arg.Contains("ax.", StringComparison.OrdinalIgnoreCase))
                flags.Add(settings.AxIntegrationEnabled ? "ax-enabled" : "ax-disabled");
            if (!PlanGuard.IsAllowed(settings, arg))
                flags.Add("blocked-by-plan-guard");
            if (!string.IsNullOrWhiteSpace(s.GuardProcessContains) || !string.IsNullOrWhiteSpace(s.GuardWindowTitleContains))
                flags.Add("has-foreground-guard");
            if (s.RetryCount > 0)
                flags.Add($"retry={s.RetryCount}");
            if (s.Checkpoint)
                flags.Add("checkpoint");
            sb.AppendLine($"{i + 1}. {s.ActionType}: {Short(arg, 160)} [{(flags.Count == 0 ? "ok" : string.Join(", ", flags))}]");
        }
        sb.AppendLine();
        sb.AppendLine("[Resume]");
        var state = FlowResumeStore.Get(recipe.Id);
        sb.AppendLine(state == null
            ? "No resume state."
            : $"{state.Status} · next step {state.NextStepIndex + 1}/{Math.Max(1, state.TotalSteps)} · {state.LastResult}");
        return sb.ToString().TrimEnd();
    }

    public static string SaveReport(AutomationRecipe recipe, NexusSettings settings)
    {
        Directory.CreateDirectory(AppPaths.DataDir);
        var safe = string.Join("_", (recipe.Name ?? "flow").Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
        if (safe.Length > 60)
            safe = safe[..60];
        var path = Path.Combine(AppPaths.DataDir, $"flow-test-{safe}-{DateTime.Now:yyyyMMdd-HHmmss}.txt");
        File.WriteAllText(path, BuildReport(recipe, settings));
        return path;
    }

    private static string Short(string value, int max) => value.Length <= max ? value : value[..max] + "...";
}
