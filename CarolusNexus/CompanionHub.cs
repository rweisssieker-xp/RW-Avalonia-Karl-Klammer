using System;
using CarolusNexus.Services;

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

    /// <summary>Wenn ein Control-Rect bekannt ist (z. B. UIA), Companion dorthin schwenken (B3).</summary>
    public static event Action<int, int, int, int>? JumpToTargetScreenRect;

    public static void Publish(CompanionVisualState state)
    {
        ActivityStatusHub.SetCompanionState(state);
        StateChanged?.Invoke(state);
    }

    public static void PublishJumpToTarget(int left, int top, int width, int height) =>
        JumpToTargetScreenRect?.Invoke(left, top, width, height);
}
