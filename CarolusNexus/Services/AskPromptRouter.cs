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

    /// <summary>Optional multimonitor vision summary before the CLI run.</summary>
    public bool WithScreenSummary { get; }
}

/// <summary>
/// Ask + CLI triggers (English + legacy German) for one shell (handbook / USP).
/// </summary>
public static class AskPromptRouter
{
    private static readonly Regex PersonaGreetingRx = new(
        @"^(hey|hallo|hi|hello|howdy|guten\s+tag|servus)\s*,?\s*karl\s+klammer\s*[\!\?\.]*\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static bool TryPersonaGreeting(string? prompt, out string reply)
    {
        reply = "";
        if (string.IsNullOrWhiteSpace(prompt))
            return false;
        if (!PersonaGreetingRx.IsMatch(prompt.Trim()))
            return false;
        reply = "Hey boss — at your service.";
        return true;
    }

    public static bool TryParseCliRoute(string? prompt, out CliAskRoute? route)
    {
        route = null;
        if (string.IsNullOrWhiteSpace(prompt))
            return false;

        var raw = prompt.Trim();
        var lower = raw.ToLowerInvariant();

        if (lower.StartsWith("use codex with screen", StringComparison.Ordinal))
        {
            var rest = TrimTriggerTail(raw, "use codex with screen");
            route = new CliAskRoute("codex", rest, withScreenSummary: true);
            return true;
        }

        if (lower.StartsWith("use codex", StringComparison.Ordinal))
        {
            var rest = TrimTriggerTail(raw, "use codex");
            route = new CliAskRoute("codex", rest, withScreenSummary: false);
            return true;
        }

        if (lower.StartsWith("use claude code", StringComparison.Ordinal))
        {
            var rest = TrimTriggerTail(raw, "use claude code");
            route = new CliAskRoute("claude code", rest, withScreenSummary: false);
            return true;
        }

        if (lower.StartsWith("use openclaw", StringComparison.Ordinal))
        {
            var rest = TrimTriggerTail(raw, "use openclaw");
            route = new CliAskRoute("openclaw", rest, withScreenSummary: false);
            return true;
        }

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
        if (raw.Length < triggerLower.Length)
            return "";
        var tail = raw[triggerLower.Length..].TrimStart(' ', '\t', ':', '–', '-', '—');
        return tail;
    }
}
