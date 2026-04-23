using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CarolusNexus.Models;

namespace CarolusNexus.Services;

public static class AxFormMemoryService
{
    public static string StorePath => Path.Combine(AppPaths.DataDir, "ax-form-memory.json");

    public sealed class FormMemory
    {
        [JsonPropertyName("key")]
        public string Key { get; set; } = "";

        [JsonPropertyName("title")]
        public string Title { get; set; } = "";

        [JsonPropertyName("adapterFamily")]
        public string AdapterFamily { get; set; } = "";

        [JsonPropertyName("seenCount")]
        public int SeenCount { get; set; }

        [JsonPropertyName("lastSeenUtc")]
        public DateTime LastSeenUtc { get; set; } = DateTime.UtcNow;

        [JsonPropertyName("suggestedFlows")]
        public List<string> SuggestedFlows { get; set; } = new();
    }

    public sealed class FormMemoryDocument
    {
        [JsonPropertyName("version")]
        public int Version { get; set; } = 1;

        [JsonPropertyName("forms")]
        public List<FormMemory> Forms { get; set; } = new();
    }

    public static FormMemoryDocument Load()
    {
        try
        {
            if (!File.Exists(StorePath))
                return new FormMemoryDocument();
            return JsonSerializer.Deserialize<FormMemoryDocument>(File.ReadAllText(StorePath)) ?? new FormMemoryDocument();
        }
        catch
        {
            return new FormMemoryDocument();
        }
    }

    public static int LearnFromWatchSessions()
    {
        var doc = Load();
        var watch = WatchSessionService.LoadOrEmpty();
        var flows = SafeRecipeNames();
        var changed = 0;
        foreach (var entry in watch.Entries.Where(e => !string.IsNullOrWhiteSpace(e.WindowTitle)))
        {
            var key = Normalize(entry.WindowTitle!);
            var form = doc.Forms.FirstOrDefault(f => string.Equals(f.Key, key, StringComparison.OrdinalIgnoreCase));
            if (form == null)
            {
                form = new FormMemory
                {
                    Key = key,
                    Title = entry.WindowTitle!,
                    AdapterFamily = entry.AdapterFamily ?? "",
                    SuggestedFlows = flows.Take(5).ToList()
                };
                doc.Forms.Add(form);
            }

            form.SeenCount++;
            form.LastSeenUtc = entry.UtcAt;
            changed++;
        }

        if (changed > 0)
            Save(doc);
        return changed;
    }

    public static string BuildMemoryReport(NexusSettings settings)
    {
        LearnFromWatchSessions();
        var doc = Load();
        var sb = new StringBuilder();
        sb.AppendLine("AX / Form Memory");
        sb.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"Known forms: {doc.Forms.Count}");
        sb.AppendLine($"AX enabled: {settings.AxIntegrationEnabled}");
        sb.AppendLine();
        if (doc.Forms.Count == 0)
        {
            sb.AppendLine("No form memory yet. Enable watch mode while using AX/operator screens.");
        }
        else
        {
            foreach (var form in doc.Forms.OrderByDescending(f => f.SeenCount).Take(12))
            {
                sb.AppendLine($"- {form.SeenCount}x | {form.AdapterFamily} | {form.Title}");
                if (form.SuggestedFlows.Count > 0)
                    sb.AppendLine($"  suggested: {string.Join(", ", form.SuggestedFlows.Take(3))}");
            }
        }

        sb.AppendLine();
        sb.AppendLine("Next action");
        sb.AppendLine("- Use the most repeated known form as the next context-to-flow candidate.");
        return sb.ToString().TrimEnd();
    }

    private static void Save(FormMemoryDocument doc)
    {
        Directory.CreateDirectory(AppPaths.DataDir);
        File.WriteAllText(StorePath, JsonSerializer.Serialize(doc, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static string Normalize(string value)
    {
        var key = value.Trim().ToLowerInvariant();
        return key.Length > 96 ? key[..96] : key;
    }

    private static List<string> SafeRecipeNames()
    {
        try
        {
            return RitualRecipeStore.LoadAll()
                .Select(r => GetString(r, "Name") ?? GetString(r, "Title") ?? GetString(r, "Id") ?? "flow")
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch
        {
            return new List<string>();
        }
    }

    private static string? GetString(object obj, string prop) =>
        obj.GetType().GetProperty(prop, BindingFlags.Public | BindingFlags.Instance)?.GetValue(obj)?.ToString();
}
