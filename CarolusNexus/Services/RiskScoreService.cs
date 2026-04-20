using System;
using System.Collections.Generic;
using CarolusNexus.Models;

namespace CarolusNexus.Services;

/// <summary>Heuristisches Risiko für Pläne / Rituale (Anzeige + Gate).</summary>
public static class RiskScoreService
{
    public static string ClassifyPlan(IReadOnlyList<RecipeStep> steps, NexusSettings? _)
    {
        var score = 0;
        foreach (var s in steps)
        {
            var a = (s.ActionArgument ?? "").ToLowerInvariant();
            if (a.Length == 0)
                score += 1;
            if (a.Contains("password", StringComparison.Ordinal) || a.Contains("credential", StringComparison.Ordinal))
                score += 4;
            if (a.Contains("delete", StringComparison.Ordinal) || a.Contains("format", StringComparison.Ordinal))
                score += 3;
            if (a.Contains("send", StringComparison.Ordinal) || a.Contains("post", StringComparison.Ordinal))
                score += 2;
            if (a.Contains("shell", StringComparison.Ordinal) || a.Contains("powershell", StringComparison.Ordinal))
                score += 2;
            if (a.StartsWith("uia.", StringComparison.OrdinalIgnoreCase))
                score += 2;
            if (a.Contains("[action:", StringComparison.OrdinalIgnoreCase))
                score += 1;
        }

        if (steps.Count > 12)
            score += 2;

        return score >= 6 ? "high" : score >= 2 ? "medium" : "low";
    }

    public static string ClassifyRecipe(AutomationRecipe recipe, NexusSettings? settings) =>
        ClassifyPlan(recipe.Steps, settings);
}
