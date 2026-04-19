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

    /// <summary>z. B. <c>draft</c> oder <c>published</c> — Governance / Freigabe.</summary>
    [JsonPropertyName("publicationState")]
    public string PublicationState { get; set; } = "draft";

    [JsonPropertyName("archived")]
    public bool Archived { get; set; }

    [JsonPropertyName("steps")]
    public List<RecipeStep> Steps { get; set; } = new();

    [JsonIgnore]
    public string ListCaption
    {
        get
        {
            var name = string.IsNullOrWhiteSpace(Name) ? "(ohne Name)" : Name;
            var arch = Archived ? " · archiv" : "";
            var state = string.IsNullOrWhiteSpace(PublicationState) ? "draft" : PublicationState;
            return $"{name}{arch} · {state}";
        }
    }
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
