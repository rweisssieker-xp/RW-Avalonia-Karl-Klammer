using System;
using System.Linq;
using CarolusNexus.Models;

namespace CarolusNexus.Services;

public static class PlanGuard
{
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
