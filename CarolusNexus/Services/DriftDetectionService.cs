using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text;

namespace CarolusNexus.Services;

public static class DriftDetectionService
{
    public static string BuildReport()
    {
        if (!File.Exists(AppPaths.AdaptiveOperatorMemory))
            return "(no adaptive operator memory yet)";

        var doc = JsonSerializer.Deserialize<AdaptiveOperatorMemoryDocument>(File.ReadAllText(AppPaths.AdaptiveOperatorMemory))
                  ?? new AdaptiveOperatorMemoryDocument();
        var risky = doc.Entries
            .Where(x => x.Error + x.Unsupported > x.Success && (x.Error + x.Unsupported) > 0)
            .OrderByDescending(x => x.Error + x.Unsupported)
            .Take(12)
            .ToList();

        var sb = new StringBuilder();
        sb.AppendLine("Drift detection");
        if (risky.Count == 0)
        {
            sb.AppendLine("- No major drift detected from stored operator memory.");
            return sb.ToString().TrimEnd();
        }

        foreach (var entry in risky)
        {
            sb.AppendLine($"- {entry.AdapterFamily} · {entry.Token}");
            sb.AppendLine($"  success={entry.Success}; unsupported={entry.Unsupported}; error={entry.Error}; last={entry.LastSeenUtc.ToLocalTime():yyyy-MM-dd HH:mm:ss}");
        }

        sb.AppendLine();
        sb.AppendLine("Recommendation");
        sb.AppendLine("- Review tokens with more unsupported/error outcomes than success.");
        sb.AppendLine("- Replace generic ACTION tokens with UIA/AX/API-specific adapters where possible.");
        return sb.ToString().TrimEnd();
    }
}
