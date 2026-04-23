using System;
using System.IO;
using System.Linq;
using System.Text;
using CarolusNexus.Models;

namespace CarolusNexus.Services;

public static class AiRegressionSuiteService
{
    public static string BuildRegressionReport(NexusSettings settings)
    {
        var latestDataset = Directory.Exists(AppPaths.DataDir)
            ? Directory.GetFiles(AppPaths.DataDir, "ai-eval-dataset-*.json").OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault()
            : null;
        var sb = new StringBuilder();
        sb.AppendLine("AI Regression Test Suite");
        sb.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();
        sb.AppendLine($"Dataset: {(latestDataset == null ? "missing" : latestDataset)}");
        sb.AppendLine(Check(DotEnvStore.HasProviderKey(settings.Provider), "Provider key present"));
        sb.AppendLine(Check(File.Exists(AppPaths.KnowledgeIndex) || !settings.UseLocalKnowledge, "Knowledge condition satisfied"));
        sb.AppendLine(Check(latestDataset != null, "Evaluation dataset exists"));
        sb.AppendLine(Check(settings.Safety.PanicStopEnabled || settings.HighRiskSecondConfirm, "Safety gate configured"));
        sb.AppendLine();
        sb.AppendLine("Regression checks to run");
        sb.AppendLine("- Answer format contains facts, assumptions, confidence, risk, safe next action.");
        sb.AppendLine("- Privacy firewall redacts sensitive values.");
        sb.AppendLine("- Prompt-to-flow compiler adds dry-run and approval gates.");
        sb.AppendLine("- High-risk execution is blocked or requires second confirmation.");
        return sb.ToString().TrimEnd();
    }

    private static string Check(bool ok, string label) => (ok ? "[x] " : "[ ] ") + label;
}
