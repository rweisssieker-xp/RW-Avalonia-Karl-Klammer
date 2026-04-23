using System;
using System.IO;
using System.Linq;
using System.Text;

namespace CarolusNexus.Services;

public static class MissionControlScoreService
{
    public static string BuildScore()
    {
        var evidenceCount = File.Exists(AppPaths.ExecutionEvidence) ? File.ReadLines(AppPaths.ExecutionEvidence).Count() : 0;
        var memory = File.Exists(AppPaths.AdaptiveOperatorMemory) ? AdaptiveOperatorMemoryService.BuildReport(8) : "";
        var watch = WatchSessionService.LoadOrEmpty();
        var history = ActionHistoryService.Load();

        var confidence = Math.Min(100, 35 + evidenceCount * 2);
        var autonomy = Math.Min(100, 20 + history.Entries.Count(x => !x.DryRun) * 5);
        var governance = Math.Min(100, 40 + (evidenceCount > 0 ? 20 : 0) + (watch.Entries.Count > 0 ? 15 : 0));
        var recoverability = memory.Contains("error=", StringComparison.OrdinalIgnoreCase) ? 70 : 45;
        var proof = Math.Min(100, 30 + evidenceCount * 3);

        var sb = new StringBuilder();
        sb.AppendLine("Mission Control Score");
        sb.AppendLine($"- Confidence: {confidence}/100");
        sb.AppendLine($"- Autonomy: {autonomy}/100");
        sb.AppendLine($"- Governance: {governance}/100");
        sb.AppendLine($"- Recoverability: {recoverability}/100");
        sb.AppendLine($"- Evidence completeness: {proof}/100");
        sb.AppendLine();
        sb.AppendLine("Interpretation");
        sb.AppendLine(confidence >= 70
            ? "- System is moving beyond demo territory into evidence-backed operator execution."
            : "- More real runs and evidence are needed to raise mission confidence.");
        return sb.ToString().TrimEnd();
    }
}
