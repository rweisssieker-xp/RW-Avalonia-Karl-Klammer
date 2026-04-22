using System.Collections.Generic;
using CarolusNexus.Models;

namespace CarolusNexus.Services;

public sealed class RadicalGoal
{
    public string GoalId { get; init; } = "";
    public string GoalText { get; init; } = "";
    public string RequestedBy { get; init; } = "";
}

public sealed class RadicalPlan
{
    public string PlanId { get; init; } = "";
    public string GoalId { get; init; } = "";
    public string GoalText { get; init; } = "";
    public string RiskLevel { get; init; } = "medium";
    public string Markdown { get; init; } = "";
    public string? PlanFilePath { get; init; }
    public string? RunDigestPath { get; init; }
    public List<RecipeStep> Steps { get; init; } = [];
    public bool RequiresApproval { get; init; }
}

public sealed class RadicalExecutionReport
{
    public string RunId { get; init; } = "";
    public string PlanId { get; init; } = "";
    public string GoalText { get; init; } = "";
    public bool DryRun { get; init; }
    public bool Completed { get; init; }
    public int ExecutedSteps { get; init; }
    public int FailedSteps { get; init; }
    public string Summary { get; init; } = "";
    public string? ReportFilePath { get; init; }
    public List<string> StepSummaries { get; init; } = [];
    public string SafetyProfile { get; init; } = "";
    public string RiskLevel { get; init; } = "";
}
