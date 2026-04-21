using System;
using CarolusNexus.Models;
using CarolusNexus.Services;

namespace CarolusNexus_WinUI;

internal sealed class GhostOperatorService
{
    public GhostOperatorSuggestion? TrySuggest(
        NexusSettings settings,
        string? liveContextLine,
        GhostOperatorState state)
    {
        var insight = OperatorInsightService.BuildSnapshot(settings);
        var next = NextBestActionService.Build(settings, liveContextLine);
        var confidence = Score(insight, next, liveContextLine);
        if (confidence < 0.55 || ShouldSuppress(next, state))
            return null;

        return new GhostOperatorSuggestion
        {
            Situation = insight.LikelyTask,
            ActionLabel = next.PrimaryLabel,
            SecondaryLabel = next.Intent == "live.ax_context" ? "Open Live Context" : "Open in Ask",
            Why = $"{next.Message}\n{next.Context}\nSafe next: {insight.SafeNextAction}",
            Intent = next.Intent,
            Risk = next.RequiresApproval ? "approval required" : next.Severity,
            RequiresApproval = next.RequiresApproval,
            Confidence = confidence
        };
    }

    private static double Score(OperatorInsightSnapshot insight, NextBestAction next, string? liveContextLine)
    {
        var score = 0.42;
        if (!string.IsNullOrWhiteSpace(liveContextLine))
            score += 0.12;
        if (!string.Equals(insight.AdapterFamily, "generic", StringComparison.OrdinalIgnoreCase))
            score += 0.14;
        if (next.Severity is "success" or "warning")
            score += 0.12;
        if (next.RequiresApproval)
            score += 0.08;
        if (!string.IsNullOrWhiteSpace(insight.RecommendedFlow) && !insight.RecommendedFlow.Contains("none", StringComparison.OrdinalIgnoreCase))
            score += 0.08;
        return Math.Min(score, 0.96);
    }

    private static bool ShouldSuppress(NextBestAction next, GhostOperatorState state)
    {
        var now = DateTimeOffset.Now;
        if (state.LastShownAt != default && now - state.LastShownAt < TimeSpan.FromSeconds(24))
            return true;
        if (state.LastIgnoredAt != default && now - state.LastIgnoredAt < TimeSpan.FromSeconds(60 + state.IgnoreCount * 30))
            return true;
        return string.Equals(next.Intent, state.LastIntent, StringComparison.OrdinalIgnoreCase)
               && state.LastShownAt != default
               && now - state.LastShownAt < TimeSpan.FromMinutes(3);
    }
}
