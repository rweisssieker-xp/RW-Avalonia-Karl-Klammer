using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CarolusNexus.Services;

public static class FlowRoiTelemetryService
{
    public static string StorePath => Path.Combine(AppPaths.DataDir, "flow-roi-telemetry.json");

    public sealed class FlowRoiItem
    {
        [JsonPropertyName("flowKey")]
        public string FlowKey { get; set; } = "";

        [JsonPropertyName("estimatedMinutesSaved")]
        public int EstimatedMinutesSaved { get; set; } = 10;

        [JsonPropertyName("runsPerWeek")]
        public int RunsPerWeek { get; set; } = 2;

        [JsonPropertyName("status")]
        public string Status { get; set; } = "candidate";

        [JsonPropertyName("lastUpdatedUtc")]
        public DateTime LastUpdatedUtc { get; set; } = DateTime.UtcNow;
    }

    public sealed class FlowRoiDocument
    {
        [JsonPropertyName("version")]
        public int Version { get; set; } = 1;

        [JsonPropertyName("items")]
        public List<FlowRoiItem> Items { get; set; } = new();
    }

    public static FlowRoiDocument Load()
    {
        try
        {
            if (!File.Exists(StorePath))
                return new FlowRoiDocument();
            return JsonSerializer.Deserialize<FlowRoiDocument>(File.ReadAllText(StorePath)) ?? new FlowRoiDocument();
        }
        catch
        {
            return new FlowRoiDocument();
        }
    }

    public static string BuildReport()
    {
        var doc = EnsureSeedFromRecipes();
        var weekly = doc.Items.Sum(i => i.EstimatedMinutesSaved * i.RunsPerWeek);
        var yearly = Math.Round(weekly * 46d / 60d, 1);
        var sb = new StringBuilder();
        sb.AppendLine("Per-Flow ROI Telemetry");
        sb.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"Tracked flows: {doc.Items.Count}");
        sb.AppendLine($"Estimated weekly savings: {weekly} minutes");
        sb.AppendLine($"Estimated annual savings: {yearly} hours");
        sb.AppendLine();
        foreach (var item in doc.Items.OrderByDescending(i => i.EstimatedMinutesSaved * i.RunsPerWeek).Take(20))
            sb.AppendLine($"- {item.FlowKey}: {item.EstimatedMinutesSaved} min x {item.RunsPerWeek}/week | {item.Status}");
        return sb.ToString().TrimEnd();
    }

    private static FlowRoiDocument EnsureSeedFromRecipes()
    {
        var doc = Load();
        try
        {
            foreach (var recipe in RitualRecipeStore.LoadAll())
            {
                var key = GetString(recipe, "Name") ?? GetString(recipe, "Title") ?? GetString(recipe, "Id") ?? "flow";
                if (!doc.Items.Any(i => string.Equals(i.FlowKey, key, StringComparison.OrdinalIgnoreCase)))
                    doc.Items.Add(new FlowRoiItem { FlowKey = key });
            }
            Save(doc);
        }
        catch
        {
            // keep existing telemetry
        }
        return doc;
    }

    private static void Save(FlowRoiDocument doc)
    {
        Directory.CreateDirectory(AppPaths.DataDir);
        File.WriteAllText(StorePath, JsonSerializer.Serialize(doc, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static string? GetString(object obj, string prop) =>
        obj.GetType().GetProperty(prop, BindingFlags.Public | BindingFlags.Instance)?.GetValue(obj)?.ToString();
}
