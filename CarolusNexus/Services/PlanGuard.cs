using System;
using System.Linq;
using CarolusNexus.Models;

namespace CarolusNexus.Services;

public static class PlanGuard
{
    /// <summary>Wenn <see cref="SafetySettings.AllowedAppFamilies"/> gesetzt ist: nur Ausführung im passenden Vordergrund-Kontext.</summary>
    public static bool IsForegroundFamilyAllowed(NexusSettings settings)
    {
        var raw = (settings.Safety.AllowedAppFamilies ?? "").Trim();
        if (string.IsNullOrEmpty(raw))
            return true;

        if (!OperatingSystem.IsWindows())
            return false;

        var allowed = raw
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => x.Length > 0)
            .Select(x => x.ToLowerInvariant())
            .ToList();
        if (allowed.Count == 0)
            return true;

        var d = ForegroundWindowInfo.TryReadDetail();
        if (d == null)
            return false;

        var fam = OperatorAdapterRegistry.ResolveFamily(d.Value.ProcessName, d.Value.Title).ToLowerInvariant();
        return allowed.Any(a => fam.Contains(a, StringComparison.Ordinal) || fam == a);
    }

    public static bool IsAllowed(NexusSettings settings, string stepArgument)
    {
        var a = (stepArgument ?? "").ToLowerInvariant();
        var deny = (settings.Safety.Denylist ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => x.Length > 0)
            .ToList();
        foreach (var d in deny)
        {
            if (a.Contains(d, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        if (settings.Safety.NeverAutoSend)
        {
            if (a.Contains("mail.", StringComparison.Ordinal) ||
                a.Contains("outlook.", StringComparison.Ordinal) ||
                a.Contains("teams.", StringComparison.Ordinal) ||
                a.Contains("send", StringComparison.Ordinal))
                return false;
        }

        if (settings.Safety.NeverAutoPostBook)
        {
            if (a.Contains("post", StringComparison.Ordinal) ||
                a.Contains("book", StringComparison.Ordinal))
                return false;
        }

        return true;
    }
}
