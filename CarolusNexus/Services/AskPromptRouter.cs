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

public sealed class RadicalPlanRoute
{
    public RadicalPlanRoute(string goal, bool runPlan, bool realRun)
    {
        Goal = goal;
        RunPlan = runPlan;
        RealRun = realRun;
    }

    public string Goal { get; }

    /// <summary>Wenn true: automatisch die generierten Schritte direkt im Ask-Flow starten.</summary>
    public bool RunPlan { get; }

    /// <summary>Wenn true: echten Run (kein Dry-Run) versuchen; Safety/Gating gilt weiterhin.</summary>
    public bool RealRun { get; }
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

    public static bool TryParseRadicalRoute(string? prompt, out RadicalPlanRoute? route)
    {
        route = null;
        if (string.IsNullOrWhiteSpace(prompt))
            return false;

        var raw = prompt.Trim();
        var lower = raw.ToLowerInvariant();

        if (!lower.StartsWith("radical", StringComparison.Ordinal))
            return false;

        route = new RadicalPlanRoute(TrimTriggerTail(raw, "radical"), runPlan: true, realRun: false);
        return true;
    }

    public static bool TryParseRadicalAutoRoute(string? prompt, out RadicalPlanRoute? route)
    {
        route = null;
        if (string.IsNullOrWhiteSpace(prompt))
            return false;

        var raw = prompt.Trim();
        var lower = raw.ToLowerInvariant();

        if (!lower.StartsWith("radical-auto", StringComparison.Ordinal) &&
            !lower.StartsWith("radical auto", StringComparison.Ordinal))
            return false;

        var goal = lower.StartsWith("radical-auto", StringComparison.Ordinal)
            ? TrimTriggerTail(raw, "radical-auto")
            : TrimTriggerTail(raw, "radical auto");

        route = new RadicalPlanRoute(goal, runPlan: true, realRun: true);
        return true;
    }

    public static bool TryParseAutonomyRoute(string? prompt, out RadicalPlanRoute? route)
    {
        route = null;
        if (string.IsNullOrWhiteSpace(prompt))
            return false;

        var raw = prompt.Trim();
        var lower = raw.ToLowerInvariant();

        if (!lower.StartsWith("autonomy", StringComparison.Ordinal))
            return false;

        var autonomyGoal = TrimTriggerTail(raw, "autonomy");
        route = new RadicalPlanRoute(autonomyGoal, runPlan: true, realRun: true);
        return true;
    }

    public static bool TryParseOrbitalRoute(string? prompt, out RadicalPlanRoute? route)
    {
        route = null;
        if (string.IsNullOrWhiteSpace(prompt))
            return false;

        var raw = prompt.Trim();
        var lower = raw.ToLowerInvariant();
        var goal = "";

        if (lower.StartsWith("orbit", StringComparison.Ordinal))
            goal = TrimTriggerTail(raw, "orbit");
        else if (lower.StartsWith("one-intent orbit", StringComparison.Ordinal))
            goal = TrimTriggerTail(raw, "one-intent orbit");
        else if (lower.Contains("one-intent orbit", StringComparison.Ordinal))
        {
            var idx = lower.IndexOf("one-intent orbit", StringComparison.Ordinal);
            goal = idx < 0
                ? ""
                : raw[(idx + "one-intent orbit".Length)..].TrimStart(' ', '\t', ':', '–', '-', '—');
        }
        else if (lower.Contains("orbital", StringComparison.Ordinal))
        {
            var idx = lower.IndexOf("orbital", StringComparison.Ordinal);
            goal = idx < 0
                ? ""
                : raw[(idx + "orbital".Length)..].TrimStart(' ', '\t', ':', '–', '-', '—');
        }
        else
            return false;

        route = new RadicalPlanRoute($"orbit {goal} today critical".Trim(), runPlan: true, realRun: true);
        return true;
    }

    public static bool TryParsePredictiveAutonomyRoute(string? prompt, out RadicalPlanRoute? route)
    {
        route = null;
        if (string.IsNullOrWhiteSpace(prompt))
            return false;

        var raw = prompt.Trim();
        var lower = raw.ToLowerInvariant();
        var promptGoal = "";

        if (lower.StartsWith("predictive", StringComparison.Ordinal))
            promptGoal = TrimTriggerTail(raw, "predictive");
        else if (lower.Contains("predictive", StringComparison.Ordinal))
        {
            var idx = lower.IndexOf("predictive", StringComparison.Ordinal);
            promptGoal = idx < 0
                ? ""
                : raw[(idx + "predictive".Length)..].TrimStart(' ', '\t', ':', '–', '-', '—');
        }
        else if (lower.Contains("vorhersage", StringComparison.Ordinal))
        {
            var idx = lower.IndexOf("vorhersage", StringComparison.Ordinal);
            promptGoal = idx < 0
                ? ""
                : raw[(idx + "vorhersage".Length)..].TrimStart(' ', '\t', ':', '–', '-', '—');
        }
        else
            return false;

        route = new RadicalPlanRoute($"predictive {promptGoal}".Trim(), runPlan: true, realRun: false);
        return true;
    }

    private static string TrimTriggerTail(string raw, string triggerLower)
    {
        if (raw.Length < triggerLower.Length)
            return "";
        var tail = raw[triggerLower.Length..].TrimStart(' ', '\t', ':', '–', '-', '—');
        return tail;
    }
}
