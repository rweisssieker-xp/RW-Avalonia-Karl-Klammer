using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CarolusNexus.Services;

public class RitualJobEntry
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("recipeId")]
    public string RecipeId { get; set; } = "";

    [JsonPropertyName("recipeName")]
    public string RecipeName { get; set; } = "";

    [JsonPropertyName("utcQueued")]
    public DateTime UtcQueued { get; set; }
}

public sealed class RitualJobHistoryEntry : RitualJobEntry
{
    [JsonPropertyName("utcFinished")]
    public DateTime? UtcFinished { get; set; }

    [JsonPropertyName("outcome")]
    public string Outcome { get; set; } = "";

    [JsonPropertyName("detail")]
    public string? Detail { get; set; }
}

public sealed class RitualJobQueueDocument
{
    [JsonPropertyName("pending")]
    public List<RitualJobEntry> Pending { get; set; } = new();

    [JsonPropertyName("history")]
    public List<RitualJobHistoryEntry> History { get; set; } = new();
}

public static class RitualJobQueueStore
{
    private const int MaxHistory = 100;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static RitualJobQueueDocument LoadOrEmpty()
    {
        if (!File.Exists(AppPaths.RitualJobQueue))
            return new RitualJobQueueDocument();

        try
        {
            var json = File.ReadAllText(AppPaths.RitualJobQueue);
            return JsonSerializer.Deserialize<RitualJobQueueDocument>(json, JsonOpts) ?? new RitualJobQueueDocument();
        }
        catch
        {
            return new RitualJobQueueDocument();
        }
    }

    private static void Save(RitualJobQueueDocument doc)
    {
        Directory.CreateDirectory(AppPaths.DataDir);
        while (doc.History.Count > MaxHistory)
            doc.History.RemoveAt(doc.History.Count - 1);

        var json = JsonSerializer.Serialize(doc, JsonOpts);
        File.WriteAllText(AppPaths.RitualJobQueue, json);
    }

    public static int GetPendingCount() => LoadOrEmpty().Pending.Count;

    /// <summary>Kurztext für Dashboard / Diagnose (ohne vollständige Datei zu lesen zweimal).</summary>
    public static string FormatDashboardSummary(int maxPendingLines = 5, int maxHistoryLines = 6)
    {
        var doc = LoadOrEmpty();
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Ausstehend: {doc.Pending.Count}");
        foreach (var p in doc.Pending.Take(maxPendingLines))
        {
            var t = p.UtcQueued.ToLocalTime();
            sb.AppendLine($" · {t:yyyy-MM-dd HH:mm} — {p.RecipeName}");
        }

        if (doc.Pending.Count > maxPendingLines)
            sb.AppendLine($" … +{doc.Pending.Count - maxPendingLines} weitere");

        sb.AppendLine("Letzte Jobs:");
        if (doc.History.Count == 0)
            sb.AppendLine(" (noch keine)");
        else
        {
            foreach (var h in doc.History.Take(maxHistoryLines))
            {
                var tf = h.UtcFinished?.ToLocalTime() ?? h.UtcQueued.ToLocalTime();
                var detail = string.IsNullOrEmpty(h.Detail) ? "" : $" — {h.Detail}";
                sb.AppendLine($" · {tf:MM-dd HH:mm} {h.Outcome}: {h.RecipeName}{detail}");
            }
        }

        return sb.ToString().TrimEnd();
    }

    public static void Enqueue(string recipeId, string recipeName)
    {
        if (string.IsNullOrWhiteSpace(recipeId))
            throw new ArgumentException("recipeId fehlt.", nameof(recipeId));

        var doc = LoadOrEmpty();
        doc.Pending.Add(new RitualJobEntry
        {
            Id = Guid.NewGuid().ToString("n"),
            RecipeId = recipeId,
            RecipeName = recipeName ?? "",
            UtcQueued = DateTime.UtcNow
        });
        Save(doc);
    }

    public static bool TryDequeuePending(out RitualJobEntry? job)
    {
        var doc = LoadOrEmpty();
        job = doc.Pending.FirstOrDefault();
        if (job == null)
            return false;

        doc.Pending.RemoveAt(0);
        Save(doc);
        return true;
    }

    public static void RecordHistory(RitualJobEntry job, string outcome, string? detail)
    {
        var doc = LoadOrEmpty();
        doc.History.Insert(0, new RitualJobHistoryEntry
        {
            Id = job.Id,
            RecipeId = job.RecipeId,
            RecipeName = job.RecipeName,
            UtcQueued = job.UtcQueued,
            UtcFinished = DateTime.UtcNow,
            Outcome = outcome,
            Detail = detail
        });
        Save(doc);
    }
}
