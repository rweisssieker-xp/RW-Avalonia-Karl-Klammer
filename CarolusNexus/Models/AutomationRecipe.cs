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

    /// <summary>Freie Kategorie (SOP-Bucket, Team, …) — Filter in der Flow-Bibliothek.</summary>
    [JsonPropertyName("category")]
    public string Category { get; set; } = "";

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
            var cat = string.IsNullOrWhiteSpace(Category) ? "" : $" · [{Category}]";
            return $"{name}{arch} · {state}{risk}{cat}";
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

    /// <summary>Zusätzliche Versuche bei Fehler (0 = ein Versuch; 2 = bis zu drei Versuche insgesamt).</summary>
    [JsonPropertyName("retryCount")]
    public int RetryCount { get; set; }

    /// <summary>Wartezeit zwischen Wiederholungen (ms).</summary>
    [JsonPropertyName("retryDelayMs")]
    public int RetryDelayMs { get; set; } = 400;

    /// <summary>Schutz: Vordergrundprozessname muss diesen Teilstring enthalten (leer = kein Check).</summary>
    [JsonPropertyName("guardProcessContains")]
    public string? GuardProcessContains { get; set; }

    /// <summary>Schutz: Fenstertitel muss diesen Teilstring enthalten (leer = kein Check).</summary>
    [JsonPropertyName("guardWindowTitleContains")]
    public string? GuardWindowTitleContains { get; set; }

    /// <summary>Wenn true und Guard schlägt fehl: gesamten Lauf abbrechen. Wenn false: Schritt überspringen.</summary>
    [JsonPropertyName("guardStopRunOnMismatch")]
    public bool GuardStopRunOnMismatch { get; set; } = true;

    /// <summary>Bei Fehlschlag: <c>stop</c>, <c>skip</c> (nächster Schritt), <c>continue</c> (wie skip).</summary>
    [JsonPropertyName("onFailure")]
    public string OnFailure { get; set; } = "stop";

    /// <summary>Setzt den Autonomie-Zähler zurück (Human-in-the-Loop-Grenze).</summary>
    [JsonPropertyName("checkpoint")]
    public bool Checkpoint { get; set; }

    /// <summary><c>ui</c> (Standard), <c>script</c>, <c>api</c> — siehe AutomationToolRouter.</summary>
    [JsonPropertyName("channel")]
    public string Channel { get; set; } = "ui";

    /// <summary>Nach Erfolg zu diesem 0-basierten Schrittindex springen (null = sequenziell).</summary>
    [JsonPropertyName("jumpToStepIndexOnSuccess")]
    public int? JumpToStepIndexOnSuccess { get; set; }

    /// <summary>Nach Fehlschlag zu diesem 0-basierten Schrittindex springen (null = sequenziell).</summary>
    [JsonPropertyName("jumpToStepIndexOnFailure")]
    public int? JumpToStepIndexOnFailure { get; set; }

    /// <summary>Optional: PNG-Template für CV-Klick, wenn die primäre Aktion fehlschlägt (Hybrid-Self-Heal).</summary>
    [JsonPropertyName("fallbackCvTemplatePath")]
    public string? FallbackCvTemplatePath { get; set; }
}
