using System;
using System.Linq;
using System.Text;

namespace CarolusNexus.Services;

public static class WatchToSopGeneratorService
{
    public static string BuildSop()
    {
        var watch = WatchSessionService.LoadOrEmpty();
        var history = ActionHistoryService.Load();
        var sb = new StringBuilder();
        sb.AppendLine("Watch-to-SOP");
        sb.AppendLine();
        sb.AppendLine("Purpose");
        sb.AppendLine("Convert repeated operator observation and plan history into a reusable standard operating procedure.");
        sb.AppendLine();
        sb.AppendLine("Observed environment");
        foreach (var entry in watch.Entries.TakeLast(8))
        {
            sb.AppendLine($"- {entry.UtcAt.ToLocalTime():yyyy-MM-dd HH:mm:ss} · {entry.ProcessName ?? "?"} · {entry.AdapterFamily ?? "generic"} · {entry.WindowTitle ?? ""}");
        }

        sb.AppendLine();
        sb.AppendLine("Recommended procedure");
        var latest = history.Entries.LastOrDefault(x => x.Steps.Count > 0);
        if (latest == null)
        {
            sb.AppendLine("- No executable history yet. Run a guarded plan first.");
        }
        else
        {
            for (var i = 0; i < latest.Steps.Count; i++)
            {
                var step = latest.Steps[i];
                sb.AppendLine($"{i + 1}. {step.ActionArgument}");
            }
        }

        sb.AppendLine();
        sb.AppendLine("Controls");
        sb.AppendLine("- Use preflight readiness before execution.");
        sb.AppendLine("- Use approval for published/manual flows.");
        sb.AppendLine("- Inspect recovery guidance after any blocked/error step.");
        sb.AppendLine("- Export execution evidence for audit.");
        return sb.ToString().TrimEnd();
    }
}
