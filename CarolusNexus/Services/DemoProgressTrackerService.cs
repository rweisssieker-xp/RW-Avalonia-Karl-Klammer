using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CarolusNexus.Services;

public static class DemoProgressTrackerService
{
    public static string StatePath => Path.Combine(AppPaths.DataDir, "demo-progress.json");

    private static readonly string[] Steps =
    {
        "context-captured",
        "flow-created",
        "evidence-mode-shown",
        "roi-generated",
        "eval-generated",
        "governance-exported",
        "buyer-pack-exported"
    };

    public sealed class DemoProgressState
    {
        [JsonPropertyName("updatedUtc")]
        public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;

        [JsonPropertyName("completed")]
        public List<string> Completed { get; set; } = new();
    }

    public static DemoProgressState Load()
    {
        try
        {
            if (!File.Exists(StatePath))
                return new DemoProgressState();
            return JsonSerializer.Deserialize<DemoProgressState>(File.ReadAllText(StatePath)) ?? new DemoProgressState();
        }
        catch
        {
            return new DemoProgressState();
        }
    }

    public static void Mark(string step)
    {
        var state = Load();
        if (!state.Completed.Contains(step, StringComparer.OrdinalIgnoreCase))
            state.Completed.Add(step);
        state.UpdatedUtc = DateTime.UtcNow;
        Directory.CreateDirectory(AppPaths.DataDir);
        File.WriteAllText(StatePath, JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true }));
    }

    public static void Reset()
    {
        Directory.CreateDirectory(AppPaths.DataDir);
        File.WriteAllText(StatePath, JsonSerializer.Serialize(new DemoProgressState(), new JsonSerializerOptions { WriteIndented = true }));
    }

    public static string BuildProgressReport()
    {
        var state = Load();
        var done = Steps.Count(s => state.Completed.Contains(s, StringComparer.OrdinalIgnoreCase));
        var sb = new StringBuilder();
        sb.AppendLine("Live Demo Progress Tracker");
        sb.AppendLine($"Updated: {state.UpdatedUtc.ToLocalTime():yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"Progress: {done}/{Steps.Length}");
        sb.AppendLine();
        foreach (var step in Steps)
        {
            var mark = state.Completed.Contains(step, StringComparer.OrdinalIgnoreCase) ? "[x]" : "[ ]";
            sb.AppendLine($"{mark} {Label(step)}");
        }
        sb.AppendLine();
        sb.AppendLine(done == Steps.Length
            ? "Status: Demo proof chain complete."
            : "Status: Continue with the next unchecked proof step.");
        return sb.ToString().TrimEnd();
    }

    private static string Label(string step) => step switch
    {
        "context-captured" => "Context captured from foreground/operator screen",
        "flow-created" => "Flow candidate created or selected",
        "evidence-mode-shown" => "Evidence mode / source confidence shown",
        "roi-generated" => "ROI opportunity generated",
        "eval-generated" => "AI evaluation / hallucination guard generated",
        "governance-exported" => "Governance or audit proof exported",
        "buyer-pack-exported" => "Buyer pilot proof pack exported",
        _ => step
    };
}
