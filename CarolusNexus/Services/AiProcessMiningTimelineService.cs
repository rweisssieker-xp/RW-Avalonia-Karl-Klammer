using System;
using System.IO;
using System.Linq;
using System.Text;

namespace CarolusNexus.Services;

public static class AiProcessMiningTimelineService
{
    public static string BuildTimeline()
    {
        var doc = WatchSessionService.LoadOrEmpty();
        var sb = new StringBuilder();
        sb.AppendLine("AI Process Mining Timeline");
        sb.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();
        if (doc.Entries.Count == 0)
        {
            sb.AppendLine("No watch evidence yet. Enable watch mode during real operator work to mine repeated process patterns.");
            return sb.ToString().TrimEnd();
        }

        sb.AppendLine($"Watch entries: {doc.Entries.Count}");
        sb.AppendLine();
        sb.AppendLine("Recent timeline");
        foreach (var e in doc.Entries.TakeLast(18))
        {
            var title = string.IsNullOrWhiteSpace(e.WindowTitle) ? "(unknown window)" : e.WindowTitle!;
            if (title.Length > 70)
                title = title[..70] + "...";
            sb.AppendLine($"- {e.UtcAt.ToLocalTime():HH:mm:ss} | {e.ProcessName ?? "?"} | {e.AdapterFamily ?? "generic"} | {title}");
        }

        sb.AppendLine();
        sb.AppendLine("Top automation candidates");
        var groups = doc.Entries
            .Where(e => !string.IsNullOrWhiteSpace(e.WindowTitle))
            .GroupBy(e => e.WindowTitle!.Trim())
            .OrderByDescending(g => g.Count())
            .Take(8)
            .ToList();
        foreach (var g in groups)
        {
            var score = Math.Min(100, 30 + g.Count() * 12);
            sb.AppendLine($"- score {score} | {g.Count()}x | {g.Key}");
        }

        sb.AppendLine();
        sb.AppendLine("Next best proof action");
        sb.AppendLine(groups.Count == 0
            ? "- Capture at least 10 foreground observations in watch mode."
            : "- Convert the highest repeated window into a guarded flow candidate and attach ROI telemetry.");
        return sb.ToString().TrimEnd();
    }

    public static string ExportTimeline()
    {
        Directory.CreateDirectory(AppPaths.DataDir);
        var path = Path.Combine(AppPaths.DataDir, $"process-mining-timeline-{DateTime.Now:yyyyMMdd-HHmmss}.md");
        File.WriteAllText(path, BuildTimeline() + Environment.NewLine);
        return path;
    }
}
