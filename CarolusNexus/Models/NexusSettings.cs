using System.Text.Json.Serialization;

namespace CarolusNexus.Models;

public sealed class NexusSettings
{
    [JsonPropertyName("provider")]
    public string Provider { get; set; } = "anthropic";

    [JsonPropertyName("model")]
    public string Model { get; set; } = "claude-sonnet-4-20250514";

    [JsonPropertyName("mode")]
    public string Mode { get; set; } = "companion";

    [JsonPropertyName("speakResponses")]
    public bool SpeakResponses { get; set; }

    [JsonPropertyName("useLocalKnowledge")]
    public bool UseLocalKnowledge { get; set; } = true;

    [JsonPropertyName("suggestAutomations")]
    public bool SuggestAutomations { get; set; } = true;

    /// <summary>Abstand zwischen Watch-Snapshots in Sekunden (15–600).</summary>
    [JsonPropertyName("watchSnapshotIntervalSeconds")]
    public int WatchSnapshotIntervalSeconds { get; set; } = 45;

    /// <summary>Im Watch-Modus: periodisch einen kurzen LLM-Hinweis fürs Dashboard holen.</summary>
    [JsonPropertyName("proactiveDashboardLlm")]
    public bool ProactiveDashboardLlm { get; set; }

    /// <summary>Mindestabstand für proaktive LLM-Aufrufe (Sekunden, 60–3600).</summary>
    [JsonPropertyName("proactiveLlmMinIntervalSeconds")]
    public int ProactiveLlmMinIntervalSeconds { get; set; } = 180;

    /// <summary>Lokalen Tool-Host (127.0.0.1) starten — siehe LOCAL_TOOL_TOKEN in .env.</summary>
    [JsonPropertyName("enableLocalToolHost")]
    public bool EnableLocalToolHost { get; set; }

    [JsonPropertyName("localToolHostPort")]
    public int LocalToolHostPort { get; set; } = 17888;

    /// <summary>Oberflächenthema: <c>Dark</c>, <c>Light</c>, <c>Default</c> (System).</summary>
    [JsonPropertyName("uiTheme")]
    public string UiTheme { get; set; } = "Dark";

    /// <summary>Bei Ask: gekürzten UIA-Baum des Vordergrundfensters in den Prompt einfügen (Windows).</summary>
    [JsonPropertyName("includeUiaContextInAsk")]
    public bool IncludeUiaContextInAsk { get; set; }

    [JsonPropertyName("safety")]
    public SafetySettings Safety { get; set; } = new();
}

public sealed class SafetySettings
{
    [JsonPropertyName("profile")]
    public string Profile { get; set; } = "balanced";

    [JsonPropertyName("neverAutoSend")]
    public bool NeverAutoSend { get; set; } = true;

    [JsonPropertyName("neverAutoPostBook")]
    public bool NeverAutoPostBook { get; set; } = true;

    [JsonPropertyName("panicStopEnabled")]
    public bool PanicStopEnabled { get; set; } = true;

    [JsonPropertyName("denylist")]
    public string Denylist { get; set; } = "mail, outlook, teams";
}
