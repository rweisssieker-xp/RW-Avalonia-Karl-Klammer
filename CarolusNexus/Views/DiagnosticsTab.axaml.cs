using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using CarolusNexus;
using CarolusNexus.Services;

namespace CarolusNexus.Views;

public partial class DiagnosticsTab : UserControl
{
    public DiagnosticsTab()
    {
        InitializeComponent();
        BtnExport.Click += (_, _) => ExportDiagnostics();
        BtnCopy.Click += async (_, _) => await CopyAllToClipboardAsync();
        BtnClear.Click += (_, _) => { DiagLog.Text = ""; NexusShell.Log("Logs cleared (local)."); };
        BtnAudit.Click += (_, _) => LoadRitualAuditTail();
        BtnRuntimeReport.Click += (_, _) => LoadRuntimeReport();
        BtnAuditPackage.Click += (_, _) => ExportAuditPackage();
        BtnUspStudio.Click += (_, _) => LoadUspStudio();
        BtnUspStudioPack.Click += (_, _) => ExportUspStudioPack();
        BtnAiEvalLab.Click += (_, _) => LoadAiEvalLab();
        BtnAiEvalPack.Click += (_, _) => ExportAiEvalPack();
        BtnAiRoi.Click += (_, _) => LoadAiRoi();
        BtnAiRoiPack.Click += (_, _) => ExportAiRoiPack();
        BtnAiDemoOrchestrator.Click += (_, _) => LoadAiDemoOrchestrator();
        BtnAiDemoPack.Click += (_, _) => ExportAiDemoPack();
        BtnPilotProofPack.Click += (_, _) => ExportPilotProofPack();
        BtnDemoProgress.Click += (_, _) => LoadDemoProgress();
        BtnReleaseReady.Click += (_, _) => LoadReleaseReadiness();
        BtnQualityBadges.Click += (_, _) => LoadQualityBadges();
        BtnEvalDataset.Click += (_, _) => ExportEvalDataset();
        BtnPrivacyFirewall.Click += (_, _) => LoadPrivacyFirewall();
        BtnPromptCompiler.Click += (_, _) => LoadPromptCompiler();
        BtnProcessTimeline.Click += (_, _) => LoadProcessTimeline();
        BtnEvidenceContract.Click += (_, _) => LoadEvidenceContract();
        BtnAxFormMemory.Click += (_, _) => LoadAxFormMemory();
        BtnApprovalCenter.Click += (_, _) => LoadApprovalCenter();
        BtnRiskSim.Click += (_, _) => LoadRiskSimulation();
        BtnFlowRoi.Click += (_, _) => LoadFlowRoi();
        BtnAiRegression.Click += (_, _) => LoadAiRegression();
        BtnPilotMode.Click += (_, _) => LoadPilotMode();
    }

    private void LoadRitualAuditTail()
    {
        if (!File.Exists(AppPaths.RitualStepAudit))
        {
            NexusShell.Log("ritual-step-audit.jsonl does not exist yet.");
            DiagLog.Text = "(no audit — run a plan/ritual with steps first)";
            return;
        }

        try
        {
            var lines = File.ReadAllLines(AppPaths.RitualStepAudit);
            var tail = lines.Length <= 120 ? lines : lines.Skip(lines.Length - 120).ToArray();
            DiagLog.Text = string.Join(Environment.NewLine, tail);
            NexusShell.Log($"Diagnostics: ritual audit, last {tail.Length} lines.");
        }
        catch (Exception ex)
        {
            NexusShell.Log("Read ritual audit: " + ex.Message);
        }
    }

    public void Append(string line)
    {
        DiagLog.Text += line + Environment.NewLine;
    }

    public void ExportDiagnostics()
    {
        var name = Path.Combine(AppPaths.DataDir, $"diagnostics-{DateTime.Now:yyyyMMdd-HHmmss}.log");
        var header = AppBuildInfo.Summary + Environment.NewLine + new string('=', 60) + Environment.NewLine;
        File.WriteAllText(name, header + (DiagLog.Text ?? ""));
        NexusShell.Log($"export diagnostics → {name}");
    }

    private void LoadRuntimeReport()
    {
        var settings = NexusContext.GetSettings?.Invoke() ?? new CarolusNexus.Models.NexusSettings();
        DiagLog.Text = RuntimeDiagnosticsService.BuildReport(settings);
        var path = RuntimeDiagnosticsService.SaveReport(settings);
        NexusShell.Log("Diagnostics runtime report → " + path);
    }

    private void ExportAuditPackage()
    {
        var settings = NexusContext.GetSettings?.Invoke() ?? new CarolusNexus.Models.NexusSettings();
        var path = AuditExportPackageService.Export(null, settings);
        NexusShell.Log("Audit package → " + path);
        DiagLog.Text = "Audit package exported:\n" + path + "\n\n" + RuntimeDiagnosticsService.BuildReport(settings);
    }

    private void LoadUspStudio()
    {
        var settings = NexusContext.GetSettings?.Invoke() ?? new CarolusNexus.Models.NexusSettings();
        DiagLog.Text = UspStudioService.BuildStudioReport(settings, "");
        NexusShell.Log("USP Studio report generated.");
    }

    private void ExportUspStudioPack()
    {
        var settings = NexusContext.GetSettings?.Invoke() ?? new CarolusNexus.Models.NexusSettings();
        var path = UspStudioService.ExportStudioPack(settings, "");
        NexusShell.Log("USP Studio pack → " + path);
        DiagLog.Text = "USP Studio pack exported:\n" + path + "\n\n" + UspStudioService.BuildStudioReport(settings, "");
    }

    private void LoadAiEvalLab()
    {
        var settings = NexusContext.GetSettings?.Invoke() ?? new CarolusNexus.Models.NexusSettings();
        DiagLog.Text = AiEvaluationLabService.BuildEvalLabReport(settings, "") + "\n\n" + AiEvaluationLabService.BuildHallucinationGuard(settings, "");
        NexusShell.Log("AI Evaluation Lab report generated.");
    }

    private void ExportAiEvalPack()
    {
        var settings = NexusContext.GetSettings?.Invoke() ?? new CarolusNexus.Models.NexusSettings();
        var path = AiEvaluationLabService.ExportEvaluationPack(settings, "");
        NexusShell.Log("AI Evaluation Lab pack → " + path);
        DiagLog.Text = "AI Evaluation Lab pack exported:\n" + path + "\n\n" + AiEvaluationLabService.BuildEvalLabReport(settings, "");
    }

    private void LoadAiRoi()
    {
        var settings = NexusContext.GetSettings?.Invoke() ?? new CarolusNexus.Models.NexusSettings();
        DiagLog.Text = AiRoiOpportunityService.BuildRoiReport(settings, "") + "\n\n" + AiRoiOpportunityService.BuildOpportunityMatrix(settings, "");
        NexusShell.Log("AI ROI opportunity report generated.");
    }

    private void ExportAiRoiPack()
    {
        var settings = NexusContext.GetSettings?.Invoke() ?? new CarolusNexus.Models.NexusSettings();
        var path = AiRoiOpportunityService.ExportRoiPack(settings, "");
        NexusShell.Log("AI ROI opportunity pack → " + path);
        DiagLog.Text = "AI ROI opportunity pack exported:\n" + path + "\n\n" + AiRoiOpportunityService.BuildRoiReport(settings, "");
    }

    private void LoadAiDemoOrchestrator()
    {
        var settings = NexusContext.GetSettings?.Invoke() ?? new CarolusNexus.Models.NexusSettings();
        DiagLog.Text = AiDemoOrchestratorService.BuildDemoRunbook(settings, "") + "\n\n" + AiDemoOrchestratorService.BuildClickPath(settings);
        NexusShell.Log("AI Demo Orchestrator report generated.");
    }

    private void ExportAiDemoPack()
    {
        var settings = NexusContext.GetSettings?.Invoke() ?? new CarolusNexus.Models.NexusSettings();
        var path = AiDemoOrchestratorService.ExportDemoPack(settings, "");
        NexusShell.Log("AI Demo Orchestrator pack → " + path);
        DiagLog.Text = "AI Demo Orchestrator pack exported:\n" + path + "\n\n" + AiDemoOrchestratorService.BuildDemoRunbook(settings, "");
    }

    private void ExportPilotProofPack()
    {
        var settings = NexusContext.GetSettings?.Invoke() ?? new CarolusNexus.Models.NexusSettings();
        DemoProgressTrackerService.Mark("buyer-pack-exported");
        var path = PilotProofPackService.ExportMasterPack(settings, "");
        NexusShell.Log("Pilot Proof Master Pack → " + path);
        DiagLog.Text = "Pilot Proof Master Pack exported:\n" + path + "\n\n" + PilotProofPackService.BuildPilotSummary(settings, "");
    }

    private void LoadDemoProgress()
    {
        DiagLog.Text = DemoProgressTrackerService.BuildProgressReport();
        NexusShell.Log("Demo progress report generated.");
    }

    private void LoadReleaseReadiness()
    {
        var settings = NexusContext.GetSettings?.Invoke() ?? new CarolusNexus.Models.NexusSettings();
        DiagLog.Text = ReleaseReadinessService.BuildReadinessReport(settings) + "\n\n" + ReleaseReadinessService.BuildPilotModeReport(settings);
        NexusShell.Log("Release readiness report generated.");
    }

    private void LoadQualityBadges()
    {
        var settings = NexusContext.GetSettings?.Invoke() ?? new CarolusNexus.Models.NexusSettings();
        DiagLog.Text = AiAnswerQualityBadgeService.BuildQualityBadge(settings, "");
        NexusShell.Log("AI answer quality badges generated.");
    }

    private void ExportEvalDataset()
    {
        var settings = NexusContext.GetSettings?.Invoke() ?? new CarolusNexus.Models.NexusSettings();
        var path = AiEvaluationDatasetBuilderService.ExportSeedDataset(settings, "");
        NexusShell.Log("AI evaluation dataset → " + path);
        DiagLog.Text = "AI evaluation dataset exported:\n" + path;
    }

    private void LoadPrivacyFirewall()
    {
        var settings = NexusContext.GetSettings?.Invoke() ?? new CarolusNexus.Models.NexusSettings();
        DiagLog.Text = AiPrivacyFirewallService.BuildFirewallReport(settings, DiagLog.Text ?? "");
        NexusShell.Log("AI privacy firewall report generated.");
    }

    private void LoadPromptCompiler()
    {
        var settings = NexusContext.GetSettings?.Invoke() ?? new CarolusNexus.Models.NexusSettings();
        DiagLog.Text = PromptToFlowCompilerService.BuildCompiledFlow(settings, DiagLog.Text ?? "");
        NexusShell.Log("Prompt-to-flow compiler report generated.");
    }

    private void LoadProcessTimeline()
    {
        DiagLog.Text = AiProcessMiningTimelineService.BuildTimeline();
        NexusShell.Log("AI process mining timeline generated.");
    }

    private void LoadEvidenceContract()
    {
        var settings = NexusContext.GetSettings?.Invoke() ?? new CarolusNexus.Models.NexusSettings();
        DiagLog.Text = AiEvidenceAnswerContractService.BuildContract(settings, DiagLog.Text ?? "");
        NexusShell.Log("AI evidence answer contract generated.");
    }

    private void LoadAxFormMemory()
    {
        var settings = NexusContext.GetSettings?.Invoke() ?? new CarolusNexus.Models.NexusSettings();
        DiagLog.Text = AxFormMemoryService.BuildMemoryReport(settings);
        NexusShell.Log("AX form memory report generated.");
    }

    private void LoadApprovalCenter()
    {
        DiagLog.Text = HumanApprovalCenterService.SeedDemoApproval();
        NexusShell.Log("Approval center report generated.");
    }

    private void LoadRiskSimulation()
    {
        var settings = NexusContext.GetSettings?.Invoke() ?? new CarolusNexus.Models.NexusSettings();
        DiagLog.Text = AiRiskSimulatorService.BuildRiskSimulation(settings, DiagLog.Text ?? "");
        NexusShell.Log("AI risk simulation generated.");
    }

    private void LoadFlowRoi()
    {
        DiagLog.Text = FlowRoiTelemetryService.BuildReport();
        NexusShell.Log("Flow ROI telemetry report generated.");
    }

    private void LoadAiRegression()
    {
        var settings = NexusContext.GetSettings?.Invoke() ?? new CarolusNexus.Models.NexusSettings();
        DiagLog.Text = AiRegressionSuiteService.BuildRegressionReport(settings);
        NexusShell.Log("AI regression suite report generated.");
    }

    private void LoadPilotMode()
    {
        var settings = NexusContext.GetSettings?.Invoke() ?? new CarolusNexus.Models.NexusSettings();
        DemoProgressTrackerService.Reset();
        DiagLog.Text = ReleaseReadinessService.BuildPilotModeReport(settings) + "\n\n" + DemoProgressTrackerService.BuildProgressReport();
        NexusShell.Log("Pilot mode initialized.");
    }

    private async Task CopyAllToClipboardAsync()
    {
        var top = TopLevel.GetTopLevel(this);
        if (top?.Clipboard == null)
        {
            NexusShell.Log("Clipboard not available.");
            return;
        }

        await top.Clipboard.SetTextAsync(DiagLog.Text ?? "").ConfigureAwait(true);
        NexusShell.Log("Diagnostics: full log copied to clipboard.");
    }
}
