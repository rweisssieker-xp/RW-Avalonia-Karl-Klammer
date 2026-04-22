using System;
using System.IO;
using System.Text.Json;
using CarolusNexus.Models;

namespace CarolusNexus.Services;

public static class GoalOrchestrator
{
    public static async Task<RadicalPlan> GeneratePlanAsync(
        NexusSettings settings,
        string goalText,
        CancellationToken ct)
    {
        var cleanedGoal = string.IsNullOrWhiteSpace(goalText) ? "Radical AI transformation cycle" : goalText.Trim();
        var goal = new RadicalGoal
        {
            GoalId = Guid.NewGuid().ToString("N"),
            GoalText = cleanedGoal,
            RequestedBy = Environment.UserName
        };

        Directory.CreateDirectory(AppPaths.RadicalPlansDir);
        var planPrompt = $"Ziel: {cleanedGoal}";
        var result = await RadicalPlanService.BuildPlanAsync(settings, planPrompt, ct).ConfigureAwait(false);
        var plan = await PlanCompiler.CompileAsync(goal, settings, result.PlanText, result.UsedAi, ct).ConfigureAwait(false);

        var output = JsonSerializer.Serialize(plan, new JsonSerializerOptions { WriteIndented = true });
        var planFile = Path.Combine(AppPaths.RadicalPlansDir, $"radical-plan-{goal.GoalId}.json");
        await File.WriteAllTextAsync(planFile, output, ct).ConfigureAwait(false);
        plan = new RadicalPlan
        {
            PlanId = plan.PlanId,
            GoalId = plan.GoalId,
            GoalText = plan.GoalText,
            RiskLevel = plan.RiskLevel,
            Markdown = plan.Markdown,
            PlanFilePath = planFile,
            RequiresApproval = plan.RequiresApproval,
            Steps = plan.Steps
        };

        return plan;
    }
}

