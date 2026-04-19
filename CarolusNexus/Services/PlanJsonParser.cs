using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using CarolusNexus.Models;

namespace CarolusNexus.Services;

/// <summary>Extrahiert strukturierte Schritte aus Modellantworten (Markdown-JSON-Blöcke oder reines JSON).</summary>
public static class PlanJsonParser
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static readonly Regex JsonFence = new(@"```(?:json)?\s*([\s\S]*?)```", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static bool TryParseRecipeStepsFromText(string? text, out List<RecipeStep> steps)
    {
        steps = new List<RecipeStep>();
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var candidates = new List<string>();
        foreach (Match m in JsonFence.Matches(text))
        {
            if (m.Groups.Count > 1)
                candidates.Add(m.Groups[1].Value.Trim());
        }

        candidates.Add(text.Trim());

        foreach (var raw in candidates)
        {
            if (TryDeserializeSteps(raw, out var list) && list.Count > 0)
            {
                steps = list;
                return true;
            }
        }

        return false;
    }

    private static bool TryDeserializeSteps(string json, out List<RecipeStep> steps)
    {
        steps = new List<RecipeStep>();
        json = json.Trim();
        if (json.Length == 0)
            return false;

        try
        {
            if (json.StartsWith('['))
            {
                var arr = JsonSerializer.Deserialize<List<RecipeStep>>(json, JsonOpts);
                if (arr != null && arr.Count > 0)
                {
                    steps = arr.Where(s => !string.IsNullOrWhiteSpace(s.ActionArgument) || !string.IsNullOrWhiteSpace(s.ActionType)).ToList();
                    return steps.Count > 0;
                }
            }

            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("steps", out var stepsEl) || stepsEl.ValueKind != JsonValueKind.Array)
                return false;
            var list = JsonSerializer.Deserialize<List<RecipeStep>>(stepsEl.GetRawText(), JsonOpts);
            if (list == null || list.Count == 0)
                return false;
            steps = list.Where(s => !string.IsNullOrWhiteSpace(s.ActionArgument) || !string.IsNullOrWhiteSpace(s.ActionType)).ToList();
            return steps.Count > 0;
        }
        catch
        {
            return false;
        }
    }
}
