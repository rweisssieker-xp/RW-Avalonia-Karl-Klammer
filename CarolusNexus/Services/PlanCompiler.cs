using System;
using System.Text;
using System.Text.Json;
using CarolusNexus.Models;

namespace CarolusNexus.Services;

public static class PlanCompiler
{
    public static async Task<RadicalPlan> CompileAsync(
        RadicalGoal goal,
        NexusSettings settings,
        string markdown,
        bool usedAi,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var plan = new RadicalPlan
        {
            PlanId = goal.GoalId,
            GoalId = goal.GoalId,
            GoalText = goal.GoalText,
            Markdown = markdown,
            RiskLevel = DetermineRisk(markdown, usedAi),
            RequiresApproval = DetermineRisk(markdown, usedAi).Equals("high", StringComparison.OrdinalIgnoreCase),
            Steps = await RadicalPlanService.TryExtractStepsAsync(settings, markdown, ct).ConfigureAwait(false)
        };

        return plan;
    }

    private static string DetermineRisk(string markdown, bool usedAi)
    {
        if (!usedAi)
            return "low";

        var lower = (markdown ?? "").ToLowerInvariant();
        if (lower.Contains("delete") || lower.Contains("format") || lower.Contains("drop ") || lower.Contains("wipe"))
            return "high";
        if (lower.Contains("login") || lower.Contains("credentials") || lower.Contains("power-user"))
            return "medium";
        return "low";
    }
}

