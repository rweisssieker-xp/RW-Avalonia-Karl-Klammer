using System;
using System.Text.RegularExpressions;

namespace CarolusNexus.Services;

public sealed class CliAskRoute
{
    public CliAskRoute(string agent, string payload, bool withScreenSummary)
    {
        Agent = agent;
        Payload = payload;
        WithScreenSummary = withScreenSummary;
    }

    /// <summary>codex, claude code, openclaw</summary>
    public string Agent { get; }

    public string Payload { get; set; }

    /// <summary>Vor dem CLI-Lauf kurze Vision-Zusammenfassung einfügen (Multimonitor).</summary>
    public bool WithScreenSummary { get; }
}

/// <summary>
/// Deutsche Trigger aus dem Handbuch: einheitliche Shell Ask + CLI (USP P4 / I3).
/// </summary>
public static class AskPromptRouter
{
    private static readonly Regex PersonaGreetingRx = new(
        @"^(hey|hallo|hi|guten\s+tag|servus)\s*,?\s*karl\s+klammer\s*[\!\?\.]*\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static bool TryPersonaGreeting(string? prompt, out string reply)
    {
        reply = "";
        if (string.IsNullOrWhiteSpace(prompt))
            return false;
        if (!PersonaGreetingRx.IsMatch(prompt.Trim()))
            return false;
        reply = "Hey Meister, stehts zu diensten.";
        return true;
    }

    public static bool TryParseCliRoute(string? prompt, out CliAskRoute? route)
    {
        route = null;
        if (string.IsNullOrWhiteSpace(prompt))
            return false;

        var raw = prompt.Trim();
        var lower = raw.ToLowerInvariant();

        if (lower.StartsWith("nimm codex mit screen", StringComparison.Ordinal))
        {
            var rest = TrimTriggerTail(raw, "nimm codex mit screen");
            route = new CliAskRoute("codex", rest, withScreenSummary: true);
            return true;
        }

        if (lower.StartsWith("nimm codex", StringComparison.Ordinal))
        {
            var rest = TrimTriggerTail(raw, "nimm codex");
            route = new CliAskRoute("codex", rest, withScreenSummary: false);
            return true;
        }

        if (lower.StartsWith("nimm claude code", StringComparison.Ordinal))
        {
            var rest = TrimTriggerTail(raw, "nimm claude code");
            route = new CliAskRoute("claude code", rest, withScreenSummary: false);
            return true;
        }

        if (lower.StartsWith("nimm openclaw", StringComparison.Ordinal))
        {
            var rest = TrimTriggerTail(raw, "nimm openclaw");
            route = new CliAskRoute("openclaw", rest, withScreenSummary: false);
            return true;
        }

        return false;
    }

    private static string TrimTriggerTail(string raw, string triggerLower)
    {
        // raw behält Casing; triggerLower ist bereits lower — Länge vom gematchten Prefix
        if (raw.Length < triggerLower.Length)
            return "";
        var tail = raw[triggerLower.Length..].TrimStart(' ', '\t', ':', '–', '-', '—');
        return tail;
    }
}
