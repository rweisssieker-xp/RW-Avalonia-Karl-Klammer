using System;
using System.Collections.Generic;

namespace CarolusNexus.Services;

/// <summary>Merged activity line for the global status bar (Companion + Ask + operator flow).</summary>
public static class ActivityStatusHub
{
    private static readonly object Gate = new();
    private static CompanionVisualState _companion = CompanionVisualState.Ready;
    private static bool _askBusy;
    private static string? _cliAgentLabel;

    public static void SetCompanionState(CompanionVisualState state)
    {
        lock (Gate)
            _companion = state;
        Push();
    }

    public static void SetAskBusy(bool busy)
    {
        lock (Gate)
            _askBusy = busy;
        Push();
    }

    /// <summary>Re-read flow progress (AgentRunStateStore) and refresh the line.</summary>
    public static void RefreshFromStores() => Push();

    /// <summary>Local CLI agent (codex / claude code / openclaw) — null when idle.</summary>
    public static void SetCliAgentRun(string? shortAgentName)
    {
        lock (Gate)
            _cliAgentLabel = string.IsNullOrWhiteSpace(shortAgentName) ? null : shortAgentName.Trim();
        Push();
    }

    private static void Push()
    {
        var line = BuildLine();
        NexusShell.SetGlobalStatus(line);
        NexusShell.SetGlobalBusyIndicator?.Invoke(ComputeBusy());
    }

    public static bool ComputeBusy()
    {
        CompanionVisualState c;
        bool ask;
        string? cli;
        lock (Gate)
        {
            c = _companion;
            ask = _askBusy;
            cli = _cliAgentLabel;
        }

        var ar = AgentRunStateStore.Snapshot();
        if (!string.IsNullOrEmpty(cli))
            return true;
        if (ask)
            return true;
        if (!string.IsNullOrEmpty(ar.RunId) && ar.TotalSteps > 0)
            return true;
        return c is CompanionVisualState.Listening
            or CompanionVisualState.Transcribing
            or CompanionVisualState.Thinking
            or CompanionVisualState.Speaking;
    }

    private static string BuildLine()
    {
        CompanionVisualState comp;
        bool askBusy;
        string? cliLabel;
        lock (Gate)
        {
            comp = _companion;
            askBusy = _askBusy;
            cliLabel = _cliAgentLabel;
        }

        var ar = AgentRunStateStore.Snapshot();
        var parts = new List<string>();

        if (comp == CompanionVisualState.Error)
            parts.Add("Companion: err");

        if (!string.IsNullOrEmpty(ar.RunId) && ar.TotalSteps > 0)
        {
            var step = Math.Min(ar.CurrentStepIndex + 1, ar.TotalSteps);
            var name = string.IsNullOrWhiteSpace(ar.RecipeName) ? "Flow" : ar.RecipeName.Trim();
            if (name.Length > 42)
                name = name[..42] + "…";
            var flow = $"Flow: {name} {step}/{ar.TotalSteps}";
            if (!string.IsNullOrEmpty(ar.LastError))
                flow += " (step error)";
            parts.Add(flow);
        }

        if (!string.IsNullOrEmpty(cliLabel))
        {
            var c = cliLabel.Length > 28 ? cliLabel[..27] + "…" : cliLabel;
            parts.Add("CLI: " + c);
        }

        if (askBusy)
            parts.Add("Ask: busy");

        if (comp != CompanionVisualState.Ready && comp != CompanionVisualState.Error)
            parts.Add("Companion: " + CompanionTag(comp));

        if (parts.Count == 0)
            return "Ready · Ctrl+P palette";

        return string.Join(" · ", parts);
    }

    private static string CompanionTag(CompanionVisualState s) => s switch
    {
        CompanionVisualState.Ready => "ready",
        CompanionVisualState.Listening => "listen",
        CompanionVisualState.Transcribing => "STT",
        CompanionVisualState.Thinking => "think",
        CompanionVisualState.Speaking => "speak",
        CompanionVisualState.Error => "err",
        _ => "?"
    };
}
