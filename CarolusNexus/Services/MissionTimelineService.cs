using System;
using System.IO;
using System.Linq;
using System.Text;

namespace CarolusNexus.Services;

public static class MissionTimelineService
{
    public static string BuildTimeline(int maxEntries = 40)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Mission timeline");
        if (!File.Exists(AppPaths.RitualStepAudit))
        {
            sb.AppendLine("(no step audit yet)");
            return sb.ToString().TrimEnd();
        }

        var lines = File.ReadAllLines(AppPaths.RitualStepAudit)
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .TakeLast(Math.Max(1, maxEntries))
            .ToList();
        if (lines.Count == 0)
        {
            sb.AppendLine("(no step audit yet)");
            return sb.ToString().TrimEnd();
        }

        foreach (var line in lines)
            sb.AppendLine(line);
        return sb.ToString().TrimEnd();
    }
}
