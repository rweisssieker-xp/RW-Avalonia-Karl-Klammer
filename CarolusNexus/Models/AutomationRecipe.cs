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

    /// <summary>z. B. <c>manual</c> (nur nach Freigabe/Queue) oder <c>auto</c>.</summary>
    [JsonPropertyName("approvalMode")]
    public string ApprovalMode { get; set; } = "manual";

    /// <summary>z. B. <c>low</c>, <c>medium</c>, <c>high</c> — Hinweis für Operatoren / künftiges Gating.</summary>
    [JsonPropertyName("riskLevel")]
    public string RiskLevel { get; set; } = "medium";

    /// <summary>Bevorzugte Adapter-Familie (z. B. <c>explorer</c>, <c>ax2012</c>) — Heuristik, keine harte Bindung.</summary>
    [JsonPropertyName("adapterAffinity")]
    public string AdapterAffinity { get; set; } = "";

    /// <summary>Freitext: woher stammt die Konfidenz (z. B. „Teach“, „LLM“, „SOP xyz.md“).</summary>
    [JsonPropertyName("confidenceSource")]
    public string ConfidenceSource { get; set; } = "";

    /// <summary>Max. aufeinanderfolgende autonome Schritte ohne Pause; 0 = unbegrenzt (nur Dokumentation/Governance).</summary>
    [JsonPropertyName("maxAutonomySteps")]
    public int MaxAutonomySteps { get; set; }

    [JsonPropertyName("steps")]
    public List<RecipeStep> Steps { get; set; } = new();

    [JsonIgnore]
    public string ListCaption
    {
        get
        {
            var name = string.IsNullOrWhiteSpace(Name) ? "(unnamed)" : Name;
            var arch = Archived ? " · archived" : "";
            var state = string.IsNullOrWhiteSpace(PublicationState) ? "draft" : PublicationState;
            var risk = string.IsNullOrWhiteSpace(RiskLevel) ? "" : $" · {RiskLevel}";
            return $"{name}{arch} · {state}{risk}";
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
