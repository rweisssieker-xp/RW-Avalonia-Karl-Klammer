using System;

namespace CarolusNexus;

/// <summary>Visuelle KI-Zustände für den Begleiter (USP: Operator-Desktop + Sichtbarkeit des Systemzustands).</summary>
public enum CompanionVisualState
{
    Ready,
    Listening,
    Transcribing,
    Thinking,
    Speaking,
    Error
}

public static class CompanionHub
{
    public static event Action<CompanionVisualState>? StateChanged;

    public static void Publish(CompanionVisualState state) =>
        StateChanged?.Invoke(state);
}
