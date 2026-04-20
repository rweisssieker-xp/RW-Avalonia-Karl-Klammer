using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using CarolusNexus.Models;

namespace CarolusNexus.Services;

/// <summary>JSONL: ein Eintrag pro ausgeführtem / simuliertem Plan-Schritt.</summary>
public static class RitualStepAudit
{
    private static readonly object Gate = new();

    public static void Append(int stepIndex, int total, RecipeStep step, bool dryRun, string result)
    {
        try
        {
            Directory.CreateDirectory(AppPaths.DataDir);
            var row = new RowDto
            {
                UtcAt = DateTime.UtcNow,
                StepIndex = stepIndex,
                Total = total,
                DryRun = dryRun,
                ActionType = step.ActionType,
                ActionArgument = step.ActionArgument ?? "",
                WaitMs = step.WaitMs,
                Result = result.Length > 2000 ? result[..2000] + "…" : result
            };
            var line = JsonSerializer.Serialize(row, JsonOpts());
            lock (Gate)
                File.AppendAllText(AppPaths.RitualStepAudit, line + Environment.NewLine);
        }
        catch (Exception ex)
        {
            NexusShell.Log("Ritual audit: " + ex.Message);
        }
    }

    private static JsonSerializerOptions JsonOpts() => new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private sealed class RowDto
    {
        public DateTime UtcAt { get; set; }
        public int StepIndex { get; set; }
        public int Total { get; set; }
        public bool DryRun { get; set; }
        public string? ActionType { get; set; }
        public string ActionArgument { get; set; } = "";
        public int WaitMs { get; set; }
        public string Result { get; set; } = "";
    }
}
