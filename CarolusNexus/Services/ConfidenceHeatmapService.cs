using System;
using System.Collections.Generic;
using System.Text;
using CarolusNexus.Models;

namespace CarolusNexus.Services;

public static class ConfidenceHeatmapService
{
    public static string BuildHeatmap(IReadOnlyList<RecipeStep> steps, NexusSettings settings)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Confidence heatmap");
        if (steps.Count == 0)
        {
            sb.AppendLine("(no steps)");
            return sb.ToString().TrimEnd();
        }

        for (var i = 0; i < steps.Count; i++)
        {
            var r = AutomationTokenReadiness.Classify(steps[i].ActionArgument, settings, steps[i].Channel);
            var band = r.Mode switch
            {
                "real" => "GREEN",
                "guarded" => "AMBER",
                _ => "RED"
            };
            sb.AppendLine($"{i + 1}. [{band}] {r.Capability} · {steps[i].ActionArgument}");
            sb.AppendLine($"   reason: {r.Reason}");
        }

        return sb.ToString().TrimEnd();
    }
}
