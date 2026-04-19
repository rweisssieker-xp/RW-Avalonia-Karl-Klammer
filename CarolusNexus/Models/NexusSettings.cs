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
