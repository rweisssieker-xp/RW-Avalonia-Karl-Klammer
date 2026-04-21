using System;

namespace CarolusNexus_WinUI;

internal sealed class GhostOperatorState
{
    public string? LastIntent { get; set; }
    public DateTimeOffset LastShownAt { get; set; }
    public DateTimeOffset LastIgnoredAt { get; set; }
    public int IgnoreCount { get; set; }

    public void MarkShown(GhostOperatorSuggestion suggestion)
    {
        LastIntent = suggestion.Intent;
        LastShownAt = DateTimeOffset.Now;
    }

    public void MarkIgnored()
    {
        IgnoreCount++;
        LastIgnoredAt = DateTimeOffset.Now;
    }
}
