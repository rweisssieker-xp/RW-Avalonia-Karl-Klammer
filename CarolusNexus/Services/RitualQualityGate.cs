using System;
using System.Collections.Generic;
using CarolusNexus.Models;

namespace CarolusNexus.Services;

/// <summary>Blocker vor Save: Denylist, leere Args, offensichtliche Risiken.</summary>
public static class RitualQualityGate
{
    public sealed record Result(bool Ok, IReadOnlyList<string> Issues);

    public static Result Validate(AutomationRecipe recipe, NexusSettings settings)
    {
        var issues = new List<string>();
        if (string.IsNullOrWhiteSpace(recipe.Name))
            issues.Add("Ritual name is empty.");

        for (var i = 0; i < recipe.Steps.Count; i++)
        {
            var s = recipe.Steps[i];
            if (string.IsNullOrWhiteSpace(s.ActionArgument))
                issues.Add($"Step {i + 1}: empty actionArgument.");
            else if (!PlanGuard.IsAllowed(settings, s.ActionArgument))
                issues.Add($"Step {i + 1}: blocked by safety denylist — {s.ActionArgument}");
        }

        if (string.Equals(recipe.RiskLevel, "high", StringComparison.OrdinalIgnoreCase) &&
            recipe.Steps.Count > 25)
            issues.Add("High risk with many steps — trim or split the ritual.");

        return new Result(issues.Count == 0, issues);
    }
}
