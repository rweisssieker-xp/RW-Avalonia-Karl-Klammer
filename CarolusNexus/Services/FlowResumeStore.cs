using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using CarolusNexus.Models;

namespace CarolusNexus.Services;

public static class FlowResumeStore
{
    public static string StatePath => Path.Combine(AppPaths.DataDir, "flow-resume-state.json");

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static void Begin(AutomationRecipe? recipe, int totalSteps)
    {
        if (recipe == null || string.IsNullOrWhiteSpace(recipe.Id))
            return;
        var doc = Load();
        doc[recipe.Id] = new FlowResumeState
        {
            RecipeId = recipe.Id,
            RecipeName = recipe.Name,
            TotalSteps = totalSteps,
            NextStepIndex = 0,
            Status = "running",
            UpdatedUtc = DateTime.UtcNow
        };
        Save(doc);
    }

    public static void RecordStep(AutomationRecipe? recipe, int zeroBasedIndex, int totalSteps, string result)
    {
        if (recipe == null || string.IsNullOrWhiteSpace(recipe.Id))
            return;
        var doc = Load();
        doc.TryGetValue(recipe.Id, out var state);
        state ??= new FlowResumeState { RecipeId = recipe.Id, RecipeName = recipe.Name };
        state.TotalSteps = totalSteps;
        state.LastResult = result.Length > 800 ? result[..800] : result;
        state.UpdatedUtc = DateTime.UtcNow;
        if (ExecutionReliabilityGate.IsHardFailureMessage(result))
        {
            state.NextStepIndex = zeroBasedIndex;
            state.Status = "paused";
        }
        else
        {
            state.NextStepIndex = Math.Min(zeroBasedIndex + 1, totalSteps);
            state.Status = state.NextStepIndex >= totalSteps ? "completed" : "running";
        }
        doc[recipe.Id] = state;
        Save(doc);
    }

    public static void Finish(AutomationRecipe? recipe, bool completed, string? result = null)
    {
        if (recipe == null || string.IsNullOrWhiteSpace(recipe.Id))
            return;
        var doc = Load();
        doc.TryGetValue(recipe.Id, out var state);
        state ??= new FlowResumeState { RecipeId = recipe.Id, RecipeName = recipe.Name };
        state.Status = completed ? "completed" : "paused";
        if (completed)
            state.NextStepIndex = state.TotalSteps;
        if (!string.IsNullOrWhiteSpace(result))
            state.LastResult = result.Length > 800 ? result[..800] : result;
        state.UpdatedUtc = DateTime.UtcNow;
        doc[recipe.Id] = state;
        Save(doc);
    }

    public static FlowResumeState? Get(string recipeId)
    {
        if (string.IsNullOrWhiteSpace(recipeId))
            return null;
        var doc = Load();
        return doc.TryGetValue(recipeId, out var state) ? state : null;
    }

    public static string FormatSummary()
    {
        var doc = Load();
        if (doc.Count == 0)
            return "No resumable flow state.";
        var lines = new List<string>();
        foreach (var s in doc.Values)
            lines.Add($"{s.Status}: {s.RecipeName} next {s.NextStepIndex + 1}/{Math.Max(1, s.TotalSteps)} · {s.UpdatedUtc.ToLocalTime():yyyy-MM-dd HH:mm}");
        return string.Join(Environment.NewLine, lines);
    }

    private static Dictionary<string, FlowResumeState> Load()
    {
        try
        {
            if (!File.Exists(StatePath))
                return new Dictionary<string, FlowResumeState>(StringComparer.Ordinal);
            return JsonSerializer.Deserialize<Dictionary<string, FlowResumeState>>(File.ReadAllText(StatePath), JsonOpts)
                   ?? new Dictionary<string, FlowResumeState>(StringComparer.Ordinal);
        }
        catch
        {
            return new Dictionary<string, FlowResumeState>(StringComparer.Ordinal);
        }
    }

    private static void Save(Dictionary<string, FlowResumeState> doc)
    {
        Directory.CreateDirectory(AppPaths.DataDir);
        File.WriteAllText(StatePath, JsonSerializer.Serialize(doc, JsonOpts));
    }
}

public sealed class FlowResumeState
{
    public string RecipeId { get; set; } = "";
    public string RecipeName { get; set; } = "";
    public int TotalSteps { get; set; }
    public int NextStepIndex { get; set; }
    public string Status { get; set; } = "idle";
    public string LastResult { get; set; } = "";
    public DateTime UpdatedUtc { get; set; }
}
