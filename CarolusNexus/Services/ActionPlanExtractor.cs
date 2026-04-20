using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using CarolusNexus.Models;

namespace CarolusNexus.Services;

/// <summary>Extrahiert Plan-Schritte aus Modellantworten (Token-Katalog / ax.*).</summary>
public static class ActionPlanExtractor
{
    private static readonly Regex[] Patterns =
    [
        new(@"\[ACTION:[^\]]+\]", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"\[ACTIONS:[^\]]+\]", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"\bax\.[a-z_][a-z0-9_]*(?:\s*:\s*[^\s\]\)]+)?", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"\b(?:browser|explorer|mail|editor|outlook|teams|word|excel|powerpoint|onenote)\.[a-z_][a-z0-9_.]*(?:\s*:\s*[^\s\]\)]+)?",
            RegexOptions.IgnoreCase | RegexOptions.Compiled),
    ];

    public static IReadOnlyList<string> Extract(string response)
    {
        if (string.IsNullOrWhiteSpace(response))
            return Array.Empty<string>();

        var hits = new List<(int Index, string Text)>();
        foreach (var rx in Patterns)
        {
            foreach (Match m in rx.Matches(response))
            {
                var t = m.Value.Trim();
                if (t.Length > 0)
                    hits.Add((m.Index, t));
            }
        }

        hits.Sort((a, b) => a.Index.CompareTo(b.Index));
        var ordered = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (_, text) in hits)
        {
            if (seen.Add(text))
                ordered.Add(text);
        }

        return ordered;
    }

    public static string FormatPreview(IReadOnlyList<string> steps)
    {
        if (steps.Count == 0)
            return "(no action tokens in the reply — model did not name [ACTION:…] / ax.*).";

        var sb = new StringBuilder();
        for (var i = 0; i < steps.Count; i++)
            sb.AppendLine($"{i + 1}. {steps[i]}");
        return sb.ToString().TrimEnd();
    }

    public static List<RecipeStep> ToRecipeSteps(IReadOnlyList<string> raw)
    {
        return raw.Select(s => new RecipeStep
        {
            ActionType = "token",
            ActionArgument = s,
            WaitMs = 0
        }).ToList();
    }
}
