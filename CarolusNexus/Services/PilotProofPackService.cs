using System;
using System.IO;
using System.Text;
using CarolusNexus.Models;

namespace CarolusNexus.Services;

public static class PilotProofPackService
{
    public static string BuildPilotSummary(NexusSettings settings, string? currentPrompt = null)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Carolus Nexus Pilot Proof Summary");
        sb.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();
        sb.AppendLine("Positioning");
        sb.AppendLine("- Carolus Nexus turns operator context into governed, auditable AI automation.");
        sb.AppendLine("- The pilot proof is the chain: context -> answer -> flow -> guard -> eval -> ROI -> export.");
        sb.AppendLine("- WinUI is the release-facing shell; Avalonia remains useful as parity/reference until explicitly retired.");
        sb.AppendLine();
        sb.AppendLine("What this pack proves");
        sb.AppendLine("- AI value can be explained to business buyers with ROI and demo narrative.");
        sb.AppendLine("- AI risk can be managed with evidence mode, hallucination guard, safe mutation checks, and audit exports.");
        sb.AppendLine("- AX/operator specificity is visible through context capture, flow affinity, and foreground automation paths.");
        sb.AppendLine("- A pilot can start with one repeated operator process instead of a full ERP transformation.");
        sb.AppendLine();
        sb.AppendLine("Recommended pilot acceptance criteria");
        sb.AppendLine("- One repeated AX/operator task captured in watch mode.");
        sb.AppendLine("- One flow candidate created from real context.");
        sb.AppendLine("- One dry-run/demo route completed with safety gate visible.");
        sb.AppendLine("- One ROI estimate and one governance proof export produced.");
        sb.AppendLine("- Buyer receives one indexed proof pack after the session.");
        return sb.ToString().TrimEnd();
    }

    public static string ExportMasterPack(NexusSettings settings, string? currentPrompt = null)
    {
        Directory.CreateDirectory(AppPaths.DataDir);
        var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var dir = Path.Combine(AppPaths.DataDir, $"pilot-proof-master-{stamp}");
        Directory.CreateDirectory(dir);

        Write(dir, "00-pilot-summary.md", BuildPilotSummary(settings, currentPrompt));
        Write(dir, "01-demo-runbook.md", AiDemoOrchestratorService.BuildDemoRunbook(settings, currentPrompt));
        Write(dir, "02-click-path.md", AiDemoOrchestratorService.BuildClickPath(settings));
        Write(dir, "03-roi-opportunity.md", AiRoiOpportunityService.BuildRoiReport(settings, currentPrompt) + "\n\n" + AiRoiOpportunityService.BuildOpportunityMatrix(settings, currentPrompt));
        Write(dir, "04-ai-evaluation.md", AiEvaluationLabService.BuildEvalLabReport(settings, currentPrompt) + "\n\n" + AiEvaluationLabService.BuildHallucinationGuard(settings, currentPrompt));
        Write(dir, "05-governance.md", AiGovernanceUspService.BuildEvidenceModeReport(settings, currentPrompt) + "\n\n" + AiGovernanceUspService.BuildAutonomyAndMutationReport(settings));
        Write(dir, "06-competitive.md", CompetitiveUspService.BuildBattlecard(settings, currentPrompt) + "\n\n" + CompetitiveUspService.BuildFeatureMatrix(settings, currentPrompt));
        Write(dir, "07-pilot-scorecard.md", PilotReadinessUspService.BuildPilotScorecard(settings, currentPrompt));
        Write(dir, "08-answer-quality.md", AiAnswerQualityBadgeService.BuildQualityBadge(settings, currentPrompt));
        Write(dir, "09-release-readiness.md", ReleaseReadinessService.BuildReadinessReport(settings));
        Write(dir, "10-demo-progress.md", DemoProgressTrackerService.BuildProgressReport());
        Write(dir, "index.md", BuildIndex(stamp));
        return dir;
    }

    private static void Write(string dir, string file, string content) =>
        File.WriteAllText(Path.Combine(dir, file), content.TrimEnd() + Environment.NewLine);

    private static string BuildIndex(string stamp) =>
        "# Carolus Nexus Pilot Proof Master Pack\n\n"
        + $"Generated: {stamp}\n\n"
        + "- [Pilot Summary](00-pilot-summary.md)\n"
        + "- [Demo Runbook](01-demo-runbook.md)\n"
        + "- [Click Path](02-click-path.md)\n"
        + "- [ROI Opportunity](03-roi-opportunity.md)\n"
        + "- [AI Evaluation](04-ai-evaluation.md)\n"
        + "- [Governance](05-governance.md)\n"
        + "- [Competitive](06-competitive.md)\n"
        + "- [Pilot Scorecard](07-pilot-scorecard.md)\n"
        + "- [Answer Quality](08-answer-quality.md)\n"
        + "- [Release Readiness](09-release-readiness.md)\n"
        + "- [Demo Progress](10-demo-progress.md)\n";
}
