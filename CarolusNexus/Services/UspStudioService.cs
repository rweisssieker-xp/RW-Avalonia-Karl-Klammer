using System;
using System.IO;
using System.Text;
using CarolusNexus.Models;

namespace CarolusNexus.Services;

public static class UspStudioService
{
    public static string BuildStudioReport(NexusSettings settings, string? currentPrompt = null)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Carolus Nexus USP Studio");
        sb.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();
        sb.AppendLine("[1] Executive value");
        sb.AppendLine(AiUspCommandService.BuildExecutiveOnePager(settings, currentPrompt));
        sb.AppendLine();
        sb.AppendLine("[2] Pilot readiness");
        sb.AppendLine(PilotReadinessUspService.BuildPilotScorecard(settings, currentPrompt));
        sb.AppendLine();
        sb.AppendLine("[3] Evidence and governance");
        sb.AppendLine(AiGovernanceUspService.BuildEvidenceModeReport(settings, currentPrompt));
        sb.AppendLine();
        sb.AppendLine(AiGovernanceUspService.BuildAutonomyAndMutationReport(settings));
        sb.AppendLine();
        sb.AppendLine("[4] AI AgentOps");
        sb.AppendLine(AiAgentOpsUspService.BuildModelRouterReport(settings, currentPrompt));
        sb.AppendLine();
        sb.AppendLine(AiAgentOpsUspService.BuildPrivacyAndRedTeamReport(settings, currentPrompt));
        sb.AppendLine();
        sb.AppendLine("[5] Competitive position");
        sb.AppendLine(CompetitiveUspService.BuildBattlecard(settings, currentPrompt));
        sb.AppendLine();
        sb.AppendLine(CompetitiveUspService.BuildFeatureMatrix(settings, currentPrompt));
        sb.AppendLine();
        sb.AppendLine("[6] AI Evaluation Lab");
        sb.AppendLine(AiEvaluationLabService.BuildEvalLabReport(settings, currentPrompt));
        sb.AppendLine();
        sb.AppendLine(AiEvaluationLabService.BuildHallucinationGuard(settings, currentPrompt));
        sb.AppendLine();
        sb.AppendLine("[7] AI ROI + Opportunity Scoring");
        sb.AppendLine(AiRoiOpportunityService.BuildRoiReport(settings, currentPrompt));
        sb.AppendLine();
        sb.AppendLine(AiRoiOpportunityService.BuildOpportunityMatrix(settings, currentPrompt));
        sb.AppendLine();
        sb.AppendLine("[8] AI Demo Orchestrator");
        sb.AppendLine(AiDemoOrchestratorService.BuildDemoRunbook(settings, currentPrompt));
        sb.AppendLine();
        sb.AppendLine(AiDemoOrchestratorService.BuildClickPath(settings));
        sb.AppendLine();
        sb.AppendLine("[9] Pilot Proof + Readiness");
        sb.AppendLine(PilotProofPackService.BuildPilotSummary(settings, currentPrompt));
        sb.AppendLine();
        sb.AppendLine(ReleaseReadinessService.BuildReadinessReport(settings));
        sb.AppendLine();
        sb.AppendLine(AiAnswerQualityBadgeService.BuildQualityBadge(settings, currentPrompt));
        sb.AppendLine();
        sb.AppendLine("[10] Enterprise AI Controls");
        sb.AppendLine(AiPrivacyFirewallService.BuildFirewallReport(settings, currentPrompt));
        sb.AppendLine();
        sb.AppendLine(PromptToFlowCompilerService.BuildCompiledFlow(settings, currentPrompt));
        sb.AppendLine();
        sb.AppendLine(AiProcessMiningTimelineService.BuildTimeline());
        sb.AppendLine();
        sb.AppendLine(AiEvidenceAnswerContractService.BuildContract(settings, currentPrompt));
        sb.AppendLine();
        sb.AppendLine("[11] Runtime Pilot Controls");
        sb.AppendLine(AxFormMemoryService.BuildMemoryReport(settings));
        sb.AppendLine();
        sb.AppendLine(HumanApprovalCenterService.BuildReport());
        sb.AppendLine();
        sb.AppendLine(AiRiskSimulatorService.BuildRiskSimulation(settings, currentPrompt));
        sb.AppendLine();
        sb.AppendLine(FlowRoiTelemetryService.BuildReport());
        sb.AppendLine();
        sb.AppendLine(AiRegressionSuiteService.BuildRegressionReport(settings));
        sb.AppendLine();
        sb.AppendLine("[12] USP backlog");
        sb.AppendLine(BuildUspBacklog(settings, currentPrompt));
        return sb.ToString().TrimEnd();
    }

    public static string BuildUspBacklog(NexusSettings settings, string? currentPrompt = null)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Next USP Backlog");
        sb.AppendLine("P0 - Visual USP Studio tab: consolidate Ask buttons into a guided cockpit.");
        sb.AppendLine("P0 - Demo path telemetry: mark each completed proof step during a live buyer walkthrough.");
        sb.AppendLine("P1 - Source confidence badges: show RAG source count, tier, and assumptions inline in every answer.");
        sb.AppendLine("P1 - Per-flow ROI telemetry: persist estimated vs. confirmed saved time after each run.");
        sb.AppendLine("P1 - AX form memory: remember known forms/fields and suggest safer AX-specific prompts.");
        sb.AppendLine("P2 - Persona packs: CFO, IT, compliance, operations, and developer demo narratives.");
        sb.AppendLine("P2 - Prompt test bench: compare prompt variants against evidence/risk/output criteria.");
        sb.AppendLine("P2 - Export bundle index: generate a single index.md linking all proof artifacts.");
        sb.AppendLine();
        sb.AppendLine("Recommended next implementation:");
        sb.AppendLine("Build a dedicated USP Studio page or reduce Ask command clutter into grouped expanders.");
        return sb.ToString().TrimEnd();
    }

    public static string ExportStudioPack(NexusSettings settings, string? currentPrompt = null)
    {
        Directory.CreateDirectory(AppPaths.DataDir);
        var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var dir = Path.Combine(AppPaths.DataDir, $"usp-studio-{stamp}");
        Directory.CreateDirectory(dir);
        Write(dir, "00-usp-studio-report.md", BuildStudioReport(settings, currentPrompt));
        Write(dir, "01-ai-usp-brief.md", AiUspCommandService.BuildExecutiveOnePager(settings, currentPrompt));
        Write(dir, "02-pilot-scorecard.md", PilotReadinessUspService.BuildPilotScorecard(settings, currentPrompt));
        Write(dir, "03-governance-proof.md", AiGovernanceUspService.BuildEvidenceModeReport(settings, currentPrompt) + "\n\n" + AiGovernanceUspService.BuildAutonomyAndMutationReport(settings));
        Write(dir, "04-agentops.md", AiAgentOpsUspService.BuildAgentOpsRunbook(settings, currentPrompt));
        Write(dir, "05-competitive.md", CompetitiveUspService.BuildBattlecard(settings, currentPrompt) + "\n\n" + CompetitiveUspService.BuildFeatureMatrix(settings, currentPrompt));
        Write(dir, "06-ai-evaluation-lab.md", AiEvaluationLabService.BuildEvalLabReport(settings, currentPrompt) + "\n\n" + AiEvaluationLabService.BuildHallucinationGuard(settings, currentPrompt));
        Write(dir, "07-ai-roi-opportunity.md", AiRoiOpportunityService.BuildRoiReport(settings, currentPrompt) + "\n\n" + AiRoiOpportunityService.BuildOpportunityMatrix(settings, currentPrompt));
        Write(dir, "08-ai-demo-orchestrator.md", AiDemoOrchestratorService.BuildDemoRunbook(settings, currentPrompt) + "\n\n" + AiDemoOrchestratorService.BuildClickPath(settings));
        Write(dir, "09-pilot-proof-readiness.md", PilotProofPackService.BuildPilotSummary(settings, currentPrompt) + "\n\n" + ReleaseReadinessService.BuildReadinessReport(settings));
        Write(dir, "10-answer-quality.md", AiAnswerQualityBadgeService.BuildQualityBadge(settings, currentPrompt));
        Write(dir, "11-enterprise-ai-controls.md", AiPrivacyFirewallService.BuildFirewallReport(settings, currentPrompt) + "\n\n" + PromptToFlowCompilerService.BuildCompiledFlow(settings, currentPrompt) + "\n\n" + AiProcessMiningTimelineService.BuildTimeline() + "\n\n" + AiEvidenceAnswerContractService.BuildContract(settings, currentPrompt));
        Write(dir, "12-runtime-pilot-controls.md", AxFormMemoryService.BuildMemoryReport(settings) + "\n\n" + HumanApprovalCenterService.BuildReport() + "\n\n" + AiRiskSimulatorService.BuildRiskSimulation(settings, currentPrompt) + "\n\n" + FlowRoiTelemetryService.BuildReport() + "\n\n" + AiRegressionSuiteService.BuildRegressionReport(settings));
        Write(dir, "13-usp-backlog.md", BuildUspBacklog(settings, currentPrompt));
        Write(dir, "index.md", BuildIndex(stamp));
        return dir;
    }

    private static void Write(string dir, string file, string content) =>
        File.WriteAllText(Path.Combine(dir, file), content.TrimEnd() + Environment.NewLine);

    private static string BuildIndex(string stamp) =>
        "# Carolus Nexus USP Studio Pack\n\n"
        + $"Generated: {stamp}\n\n"
        + "- [USP Studio Report](00-usp-studio-report.md)\n"
        + "- [AI USP Brief](01-ai-usp-brief.md)\n"
        + "- [Pilot Scorecard](02-pilot-scorecard.md)\n"
        + "- [Governance Proof](03-governance-proof.md)\n"
        + "- [AgentOps](04-agentops.md)\n"
        + "- [Competitive](05-competitive.md)\n"
        + "- [AI Evaluation Lab](06-ai-evaluation-lab.md)\n"
        + "- [AI ROI Opportunity](07-ai-roi-opportunity.md)\n"
        + "- [AI Demo Orchestrator](08-ai-demo-orchestrator.md)\n"
        + "- [Pilot Proof Readiness](09-pilot-proof-readiness.md)\n"
        + "- [Answer Quality](10-answer-quality.md)\n"
        + "- [Enterprise AI Controls](11-enterprise-ai-controls.md)\n"
        + "- [Runtime Pilot Controls](12-runtime-pilot-controls.md)\n"
        + "- [USP Backlog](13-usp-backlog.md)\n";
}
