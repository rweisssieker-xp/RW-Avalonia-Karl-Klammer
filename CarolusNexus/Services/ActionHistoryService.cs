using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using CarolusNexus.Models;

namespace CarolusNexus.Services;

public sealed class ActionHistoryEntry
{
    [JsonPropertyName("at")]
    public DateTime UtcAt { get; set; }

    [JsonPropertyName("kind")]
    public string Kind { get; set; } = "plan_run";

    [JsonPropertyName("dryRun")]
    public bool DryRun { get; set; }

    [JsonPropertyName("summary")]
    public string Summary { get; set; } = "";

    [JsonPropertyName("steps")]
    public List<RecipeStep> Steps { get; set; } = new();

    [JsonIgnore]
    public string ListLabel =>
        $"{UtcAt.ToLocalTime():yyyy-MM-dd HH:mm} · {Kind} · {(DryRun ? "dry" : "run")} · {Steps.Count} steps";

    public override string ToString() => ListLabel;
}

public sealed class ActionHistoryDocument
{
    [JsonPropertyName("version")]
    public int Version { get; set; } = 1;

    [JsonPropertyName("entries")]
    public List<ActionHistoryEntry> Entries { get; set; } = new();
}

public static class ActionHistoryService
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static ActionHistoryDocument Load()
    {
        if (!File.Exists(AppPaths.ActionHistory))
            return new ActionHistoryDocument();
        try
        {
            var json = File.ReadAllText(AppPaths.ActionHistory);
            return JsonSerializer.Deserialize<ActionHistoryDocument>(json, JsonOpts) ?? new ActionHistoryDocument();
        }
        catch
        {
            return new ActionHistoryDocument();
        }
    }

    public static void AppendPlanRun(IReadOnlyList<RecipeStep> steps, bool dryRun, string summary)
    {
        if (steps.Count == 0)
            return;
        try
        {
            Directory.CreateDirectory(AppPaths.DataDir);
            var doc = Load();
            doc.Entries.Add(new ActionHistoryEntry
            {
                UtcAt = DateTime.UtcNow,
                Kind = "plan_run",
                DryRun = dryRun,
                Summary = summary.Trim(),
                Steps = steps.Select(s => new RecipeStep
                {
                    ActionType = s.ActionType,
                    ActionArgument = s.ActionArgument,
                    WaitMs = s.WaitMs
                }).ToList()
            });
            while (doc.Entries.Count > 200)
                doc.Entries.RemoveAt(0);

            File.WriteAllText(AppPaths.ActionHistory, JsonSerializer.Serialize(doc, JsonOpts));
        }
        catch
        {
            // ignore
        }
    }

    /// <summary>Neuester Plan-Lauf mit mindestens einem Schritt (für „promote from history“).</summary>
    public static ActionHistoryEntry? GetLatestPlanRunWithSteps()
    {
        return Load().Entries
            .Where(e => string.Equals(e.Kind, "plan_run", StringComparison.OrdinalIgnoreCase) && e.Steps.Count > 0)
            .OrderByDescending(e => e.UtcAt)
            .FirstOrDefault();
    }
}
