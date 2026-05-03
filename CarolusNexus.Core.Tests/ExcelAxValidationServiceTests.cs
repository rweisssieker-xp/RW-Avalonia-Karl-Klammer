using CarolusNexus;
using CarolusNexus.Models;
using CarolusNexus.Services;
using Xunit;

namespace CarolusNexus.Core.Tests;

public sealed class ExcelAxValidationServiceTests
{
    [Fact]
    public void Validate_csv_marks_duplicate_missing_and_ax_unavailable()
    {
        AppPaths.DiscoverRepoRoot();
        AppPaths.EnsureDataTree();
        var dir = Path.Combine(Path.GetTempPath(), "carolus-excel-ax-tests");
        Directory.CreateDirectory(dir);
        var file = Path.Combine(dir, "customers.csv");
        File.WriteAllLines(file,
        [
            "AccountNum;Name;DataAreaId",
            "1000;Mueller GmbH;DEMF",
            "1000;Müller GmbH Berlin;DEMF",
            ";Missing;DEMF",
            "2000;Beta;USMF"
        ]);

        var preview = ExcelAxValidationService.LoadPreview(file);
        var options = ExcelAxValidationService.BuildDefaultOptions(preview, "Debitor/Kunde") with { MaxRows = 20 };
        var settings = new NexusSettings { AxIntegrationEnabled = false };

        var run = ExcelAxValidationService.Validate(file, options, settings);

        Assert.Equal("AccountNum", preview.SuggestedKeyColumn);
        Assert.Contains(run.Rows, r => r.Status == "DuplicateInExcel");
        Assert.Contains(run.Rows, r => r.Status == "MissingKey");
        Assert.Contains(run.Rows, r => r.Status == "AxUnavailable");
        Assert.Contains(run.Rows, r => r.Status == "DuplicateInExcel" && r.Insight.Category == "Duplicate");
        Assert.Contains(run.Rows, r => r.Status == "MissingKey" && r.Insight.Category == "ExcelDataQuality");
        Assert.Contains(run.Rows, r => r.Status == "AxUnavailable" && r.Insight.Category == "AxConnectivity");
        Assert.Contains("AI Reconciliation Copilot", ExcelAxValidationService.BuildReconciliationCopilotReport(run));
        var score = ExcelAxValidationService.BuildReadinessScore(run);
        Assert.InRange(score.Score, 0, 100);
        Assert.NotEmpty(score.Blockers);
        var inbox = ExcelAxValidationService.BuildExceptionInbox(run);
        Assert.Contains(inbox, i => i.Category == "AxConnectivity" && i.Owner == "AX/IT owner");
        Assert.Contains("AI Exception Inbox", ExcelAxValidationService.BuildExceptionInboxReport(run));
        var pack = ExcelAxValidationService.CreatePilotPack(run);
        Assert.True(File.Exists(pack.MarkdownPath));
        Assert.Contains("AX/Excel Pilot Pack", File.ReadAllText(pack.MarkdownPath));
        Assert.Contains("AX/IT connectivity", ExcelAxValidationService.AnswerRunQuestion(run, "what belongs to IT?"));
        Assert.Contains("AX Safe-Mode Certificate", ExcelAxValidationService.BuildSafeModeCertificateReport(run));
        var certificate = ExcelAxValidationService.CreateSafeModeCertificate(run);
        Assert.True(File.Exists(certificate.CertificatePath));
        Assert.Contains("read-only evidence mode", File.ReadAllText(certificate.CertificatePath));
        Assert.Contains("Excel Risk Heatmap", ExcelAxValidationService.BuildRiskHeatmapReport(preview, run));
        Assert.Contains("AX Field Mapping Intelligence", ExcelAxValidationService.BuildFieldMappingReport(preview, options));
        Assert.Contains("DataArea / Company Copilot", ExcelAxValidationService.BuildDataAreaCopilotReport(preview));
        Assert.Contains("Duplicate Intelligence 2.0", ExcelAxValidationService.BuildDuplicateIntelligenceReport(preview));
        Assert.Contains("AI Fix Proposal Pack", ExcelAxValidationService.BuildFixProposalPackReport(run));
        Assert.Contains("Pilot ROI Estimator", ExcelAxValidationService.BuildRoiEstimatorReport(run));
        Assert.Contains("AX Process Twin", ExcelAxValidationService.BuildProcessTwinReport(run));
        Assert.Contains("Evidence Timeline", ExcelAxValidationService.BuildEvidenceTimelineReport(run));
        Assert.Contains("Operator Task Board", ExcelAxValidationService.BuildOperatorTaskBoardReport(run));
        Assert.Contains("Offline AX Knowledge", ExcelAxValidationService.AnswerOfflineAxKnowledge("Debitor in AX"));
        Assert.Contains("Excel Risk Heatmap", ExcelAxValidationService.BuildPreviewIntelligenceReport(preview, options, run));
        var fixExport = ExcelAxValidationService.CreateFixExportCsv(run);
        Assert.True(File.Exists(fixExport.CsvPath));
        Assert.Contains("carolusFix", File.ReadAllText(fixExport.CsvPath));
        var bundle = ExcelAxValidationService.CreateEvidenceBundleZip(run);
        Assert.True(File.Exists(bundle.ZipPath));
        Assert.NotEmpty(bundle.IncludedFiles);
        Assert.Contains("Run Dashboard", ExcelAxValidationService.BuildRunDashboardReport());
        Assert.True(File.Exists(run.ExportPath));
        Assert.Contains("category,severity,explanation,nextAction", File.ReadAllText(run.ExportPath));
    }
}
