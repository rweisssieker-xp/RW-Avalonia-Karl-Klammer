using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using CarolusNexus.Models;

namespace CarolusNexus.Services;

public sealed record AutomationTokenReadinessResult(
    string Token,
    string Channel,
    string Mode,
    bool IsExecutable,
    string Reason,
    string Capability);

public static class AutomationTokenReadiness
{
    private static readonly Regex ActionRx = new(@"\[ACTION:(\w+)(?:\|([^\]]*))?\]", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static AutomationTokenReadinessResult Classify(string? token, NexusSettings settings, string? channel = null)
    {
        var raw = (token ?? "").Trim();
        var ch = (channel ?? InferChannel(raw)).Trim().ToLowerInvariant();
        if (raw.Length == 0)
            return Result(raw, ch, "unsupported", false, "empty token", "none");

        if (!OperatingSystem.IsWindows() && ch is "ui" or "desktop")
            return Result(raw, ch, "guarded", false, "requires Windows foreground desktop", "windows");

        var powerUser = string.Equals(settings.Safety.Profile, "power-user", StringComparison.OrdinalIgnoreCase);
        if (ch is "api" or "script" && !powerUser)
            return Result(raw, ch, "guarded", false, "requires safety profile power-user", ch);

        if (ch is "ui" or "desktop" && IsWriteLike(raw) && !powerUser)
            return Result(raw, "ui", "guarded", false, "write-like desktop action requires power-user", "desktop automation");

        if (raw.StartsWith("ax.", StringComparison.OrdinalIgnoreCase))
        {
            if (!settings.AxIntegrationEnabled)
                return Result(raw, "ui", "guarded", false, "AX integration disabled in setup", "ax");
            return IsKnownAx(raw)
                ? Result(raw, "ui", powerUser ? "real" : "guarded", powerUser, powerUser ? "AX token delegated to configured backend/UIA" : "AX requires power-user", "ax")
                : Result(raw, "ui", "unsupported", false, "unknown ax.* token", "ax");
        }

        if (raw.StartsWith("uia.", StringComparison.OrdinalIgnoreCase))
            return IsKnownUia(raw)
                ? Result(raw, "ui", powerUser ? "real" : "guarded", powerUser, powerUser ? "UIA token can execute through foreground UIA" : "UIA requires power-user", "uia")
                : Result(raw, "ui", "unsupported", false, "unknown uia.* token", "uia");

        if (raw.StartsWith("api.get:", StringComparison.OrdinalIgnoreCase) || raw.StartsWith("api.post:", StringComparison.OrdinalIgnoreCase))
            return Result(raw, "api", powerUser ? "real" : "guarded", powerUser, powerUser ? "HTTP action can execute" : "API requires power-user", "api");

        if (raw.StartsWith("powershell:", StringComparison.OrdinalIgnoreCase) || raw.StartsWith("cmd:", StringComparison.OrdinalIgnoreCase))
            return Result(raw, "script", powerUser ? "real" : "guarded", powerUser, powerUser ? "script action can execute" : "script requires power-user", "script");

        if (raw.StartsWith("app|", StringComparison.OrdinalIgnoreCase))
            return Result(raw, "ui", powerUser ? "real" : "guarded", powerUser, powerUser ? "app launcher token can execute" : "app launcher requires power-user", "app");

        var action = ActionRx.Match(raw);
        if (action.Success)
        {
            var kind = action.Groups[1].Value.ToLowerInvariant();
            return kind is "hotkey" or "type" or "open" or "click" or "move"
                ? Result(raw, "ui", powerUser ? "real" : "guarded", powerUser, powerUser ? "Win32 action can execute" : "Win32 action requires power-user", "win32")
                : Result(raw, "ui", "unsupported", false, "unknown ACTION kind", "win32");
        }

        if (raw.StartsWith("browser.open:", StringComparison.OrdinalIgnoreCase)
            || raw.StartsWith("explorer.open_path:", StringComparison.OrdinalIgnoreCase)
            || raw.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || raw.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return Result(raw, "ui", powerUser ? "real" : "guarded", powerUser, powerUser ? "open/navigation token can execute" : "open/navigation requires power-user", "launcher");

        return Result(raw, ch, "unsupported", false, "no executable adapter matched this token", "unknown");
    }

    public static string BuildReport(IEnumerable<RecipeStep> steps, NexusSettings settings)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Automation readiness");
        var i = 0;
        foreach (var step in steps)
        {
            i++;
            var r = Classify(step.ActionArgument, settings, step.Channel);
            sb.AppendLine($"{i}. {r.Mode.ToUpperInvariant()} · {r.Capability} · {r.Token}");
            sb.AppendLine($"   channel={r.Channel}; executable={r.IsExecutable}; reason={r.Reason}");
        }

        if (i == 0)
            sb.AppendLine("(no steps)");
        return sb.ToString().TrimEnd();
    }

    private static string InferChannel(string raw)
    {
        if (raw.StartsWith("api.", StringComparison.OrdinalIgnoreCase))
            return "api";
        if (raw.StartsWith("powershell:", StringComparison.OrdinalIgnoreCase) || raw.StartsWith("cmd:", StringComparison.OrdinalIgnoreCase))
            return "script";
        return "ui";
    }

    private static bool IsKnownAx(string raw)
    {
        var lower = raw.ToLowerInvariant();
        return lower is "ax.integration.status" or "ax.aif.ping" or "ax.read_context" or "ax.snapshot" or "ax.form_summary" or "ax.com.logon"
               || lower.StartsWith("ax.odata.get:", StringComparison.Ordinal)
               || lower.StartsWith("ax.invoke:", StringComparison.Ordinal)
               || lower.StartsWith("ax.setvalue:", StringComparison.Ordinal)
               || lower.StartsWith("ax.expand:", StringComparison.Ordinal);
    }

    private static bool IsKnownUia(string raw)
    {
        var lower = raw.ToLowerInvariant();
        return lower.StartsWith("uia.invoke:", StringComparison.Ordinal)
               || lower.StartsWith("uia.setvalue:", StringComparison.Ordinal)
               || lower.StartsWith("uia.expand:", StringComparison.Ordinal);
    }

    private static bool IsWriteLike(string raw) =>
        raw.Contains("[ACTION:", StringComparison.OrdinalIgnoreCase)
        || raw.StartsWith("uia.", StringComparison.OrdinalIgnoreCase)
        || raw.StartsWith("ax.invoke:", StringComparison.OrdinalIgnoreCase)
        || raw.StartsWith("ax.setvalue:", StringComparison.OrdinalIgnoreCase)
        || raw.StartsWith("api.post:", StringComparison.OrdinalIgnoreCase)
        || raw.StartsWith("powershell:", StringComparison.OrdinalIgnoreCase)
        || raw.StartsWith("cmd:", StringComparison.OrdinalIgnoreCase);

    private static AutomationTokenReadinessResult Result(string token, string channel, string mode, bool executable, string reason, string capability) =>
        new(token, channel, mode, executable, reason, capability);
}
