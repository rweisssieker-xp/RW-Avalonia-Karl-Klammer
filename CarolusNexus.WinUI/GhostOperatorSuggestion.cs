using System;

namespace CarolusNexus_WinUI;

internal sealed class GhostOperatorSuggestion
{
    public string Id { get; init; } = Guid.NewGuid().ToString("n");
    public string Title { get; init; } = "Ghost Operator";
    public string Situation { get; init; } = "";
    public string ActionLabel { get; init; } = "Do it";
    public string SecondaryLabel { get; init; } = "Open in Ask";
    public string Why { get; init; } = "";
    public string Intent { get; init; } = "";
    public string Risk { get; init; } = "low";
    public double Confidence { get; init; }
    public bool RequiresApproval { get; init; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.Now;
}
