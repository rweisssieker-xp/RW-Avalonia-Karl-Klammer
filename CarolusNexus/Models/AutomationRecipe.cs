using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace CarolusNexus.Models;

public sealed class AutomationRecipe
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("steps")]
    public List<RecipeStep> Steps { get; set; } = new();
}

public sealed class RecipeStep
{
    [JsonPropertyName("actionType")]
    public string ActionType { get; set; } = "";

    [JsonPropertyName("actionArgument")]
    public string ActionArgument { get; set; } = "";

    [JsonPropertyName("waitMs")]
    public int WaitMs { get; set; }
}
