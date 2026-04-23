using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CarolusNexus.Models;

namespace CarolusNexus.Services;

public sealed class ExecutionEvidenceEntry
{
    [JsonPropertyName("at")]
    public DateTime UtcAt { get; set; }

    [JsonPropertyName("runId")]
    public string RunId { get; set; } = "";

    [JsonPropertyName("stepIndex")]
    public int StepIndex { get; set; }

    [JsonPropertyName("stepCount")]
    public int StepCount { get; set; }

    [JsonPropertyName("dryRun")]
    public bool DryRun { get; set; }

    [JsonPropertyName("token")]
    public string Token { get; set; } = "";

    [JsonPropertyName("result")]
    public string Result { get; set; } = "";

    [JsonPropertyName("mode")]
    public string Mode { get; set; } = "";

    [JsonPropertyName("capability")]
    public string Capability { get; set; } = "";

    [JsonPropertyName("reason")]
    public string Reason { get; set; } = "";
}

public static class ExecutionEvidenceService
{
    public static void Append(
        string runId,
        int stepIndex,
        int stepCount,
        RecipeStep step,
        bool dryRun,
        AutomationTokenReadinessResult readiness,
        string result)
    {
        try
        {
            Directory.CreateDirectory(AppPaths.DataDir);
            var entry = new ExecutionEvidenceEntry
            {
                UtcAt = DateTime.UtcNow,
                RunId = runId,
                StepIndex = stepIndex,
                StepCount = stepCount,
                DryRun = dryRun,
                Token = step.ActionArgument ?? "",
                Result = result,
                Mode = readiness.Mode,
                Capability = readiness.Capability,
                Reason = readiness.Reason
            };
            File.AppendAllText(AppPaths.ExecutionEvidence, JsonSerializer.Serialize(entry) + Environment.NewLine);
        }
        catch
        {
            // ignore
        }
    }

    public static string BuildReport(int maxEntries = 24)
    {
        if (!File.Exists(AppPaths.ExecutionEvidence))
            return "(no execution evidence yet)";

        var lines = File.ReadAllLines(AppPaths.ExecutionEvidence)
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .TakeLast(Math.Max(1, maxEntries))
            .ToList();
        var sb = new StringBuilder();
        sb.AppendLine("Execution evidence");
        foreach (var line in lines)
        {
            try
            {
                var e = JsonSerializer.Deserialize<ExecutionEvidenceEntry>(line);
                if (e == null)
                    continue;
                sb.AppendLine($"{e.UtcAt.ToLocalTime():yyyy-MM-dd HH:mm:ss} · {e.StepIndex}/{e.StepCount} · {(e.DryRun ? "dry" : "run")} · {e.Mode}/{e.Capability}");
                sb.AppendLine($"  token: {e.Token}");
                sb.AppendLine($"  result: {e.Result}");
                sb.AppendLine($"  why: {e.Reason}");
            }
            catch
            {
                // ignore malformed rows
            }
        }

        return sb.ToString().TrimEnd();
    }
}
