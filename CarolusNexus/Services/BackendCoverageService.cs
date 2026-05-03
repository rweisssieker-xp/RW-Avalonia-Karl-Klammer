using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using CarolusNexus.Models;

namespace CarolusNexus.Services;

public sealed record BackendCapabilityRow(
    string Area,
    string Capability,
    string WinUiSurface,
    string RuntimeStatus,
    string Evidence);

public static class BackendCoverageService
{
    public static IReadOnlyList<BackendCapabilityRow> BuildCapabilityRows(NexusSettings settings)
    {
        var safety = settings.Safety?.Profile ?? "balanced";
        var axStatus = settings.AxIntegrationEnabled
            ? (string.Equals(safety, "power-user", StringComparison.OrdinalIgnoreCase) ? "real/gated" : "guarded")
            : "disabled";

        return
        [
            new("Ask", "LLM answer, local knowledge, plan extraction", "Ask", "real", "LlmChatService, KnowledgeSnippetService, ActionPlanExtractor"),
            new("Ask", "Preflight readiness and guarded execution", "Ask", "real/gated", "AutomationTokenReadiness, SimplePlanSimulator, PlanGuard"),
            new("Operator flows", "Ritual library, resume, job queue, quality gate", "Operator flows", "real", "RitualRecipeStore, FlowResumeStore, RitualJobQueueStore"),
            new("History", "Step audit, self-heal hint, flow creation", "History", "real", "RitualStepAudit, SelfHealSuggestionService"),
            new("Knowledge", "Index, search, import, reindex", "Knowledge", "real", "KnowledgeIndexService, KnowledgeFtsStore, EmbeddingRagService"),
            new("Live Context", "Foreground window, adapter family, inspector", "Live Context", "real", "ForegroundWindowInfo, OperatorAdapterRegistry, ForegroundUiAutomationContext"),
            new("Desktop automation", "Win32 hotkey/type/open/click/move tokens", "Ask, Operator flows, Experiments", string.Equals(safety, "power-user", StringComparison.OrdinalIgnoreCase) ? "real" : "guarded", "Win32AutomationExecutor, UiAutomationActions"),
            new("AX", "AX read context, OData/AIF probe, UIA delegation", "Setup, Live Context, Diagnostics, Backend Coverage", axStatus, "AxClientAutomationService, Ax2012ODataClient, Ax2012ComBusinessConnectorRuntime"),
            new("API/Script", "HTTP hooks and script hooks", "Ask, Operator flows, Diagnostics", string.Equals(safety, "power-user", StringComparison.OrdinalIgnoreCase) ? "real" : "guarded", "ApiHookRunner, ScriptHookRunner"),
            new("Trust", "Evidence, recovery, adaptive memory, timeline", "Ask, Operator flows, Diagnostics, USP Studio", "real", "ExecutionEvidenceService, RecoverySuggestionService, AdaptiveOperatorMemoryService, MissionTimelineService"),
            new("USPs", "ROI, proof packs, drift, heatmap, Watch-to-SOP", "USP Studio, Diagnostics, Backend Coverage", "real/report", "PilotProofPackService, MissionControlScoreService, DriftDetectionService, ConfidenceHeatmapService"),
            new("AX+Excel", "Read-only Excel list validation against AX 2012", "Excel + AX Check, Backend Coverage", settings.AxIntegrationEnabled ? "real/read-only" : "guarded", "ExcelAxValidationService, Ax2012ODataClient, AxClientAutomationService"),
            new("Voice", "PTT, recording, STT, TTS", "Header/Ask/Setup", "configured/gated", "PushToTalkHotkeyWindow, WindowsMicRecorder, SpeechTranscriptionService, TextToSpeechService"),
            new("Companion", "Tray, companion window, reduce motion, shell commands", "Header, Tray", "real", "WinUI shell, WinUiTrayHelper, KarlCompanionWinUiWindow"),
            new("Evaluation", "Eval dataset, regression, answer quality", "Diagnostics, USP Studio", "real/report", "AiEvaluationLabService, AiRegressionSuiteService, AiAnswerQualityBadgeService")
        ];
    }

    public static string BuildCoverageReport(NexusSettings settings)
    {
        var rows = BuildCapabilityRows(settings);
        var services = GetServiceInventory();
        var sb = new StringBuilder();
        sb.AppendLine("Backend coverage");
        sb.AppendLine("Safety profile: " + (settings.Safety?.Profile ?? "balanced"));
        sb.AppendLine("AX integration: " + settings.AxIntegrationEnabled);
        sb.AppendLine();

        foreach (var group in rows.GroupBy(r => r.Area))
        {
            sb.AppendLine("## " + group.Key);
            foreach (var r in group)
            {
                sb.AppendLine("- " + r.Capability);
                sb.AppendLine("  surface: " + r.WinUiSurface);
                sb.AppendLine("  status: " + r.RuntimeStatus);
                sb.AppendLine("  evidence: " + r.Evidence);
            }
            sb.AppendLine();
        }

        sb.AppendLine("Service inventory");
        sb.AppendLine("public services: " + services.Count);
        foreach (var s in services)
            sb.AppendLine("- " + s);

        return sb.ToString().TrimEnd();
    }

    public static string BuildGapReport(NexusSettings settings)
    {
        var rows = BuildCapabilityRows(settings);
        var sb = new StringBuilder();
        sb.AppendLine("Backend gaps / product truth");
        sb.AppendLine();

        foreach (var r in rows.Where(r => r.RuntimeStatus.Contains("guarded", StringComparison.OrdinalIgnoreCase)
                                          || r.RuntimeStatus.Contains("disabled", StringComparison.OrdinalIgnoreCase)
                                          || r.RuntimeStatus.Contains("configured", StringComparison.OrdinalIgnoreCase)
                                          || r.RuntimeStatus.Contains("report", StringComparison.OrdinalIgnoreCase)))
        {
            sb.AppendLine("- " + r.Area + " · " + r.Capability);
            sb.AppendLine("  status: " + r.RuntimeStatus);
            sb.AppendLine("  next: " + NextStepFor(r, settings));
        }

        return sb.ToString().TrimEnd();
    }

    public static string BuildAxWorkbenchReport(NexusSettings settings)
    {
        var sb = new StringBuilder();
        sb.AppendLine("AX / Adapter Workbench");
        sb.AppendLine();
        if (AxClientAutomationService.TryExecute("ax.integration.status", settings, out var status))
            sb.AppendLine(status);
        sb.AppendLine();
        sb.AppendLine("Known AX readiness");
        foreach (var token in new[]
                 {
                     "ax.integration.status",
                     "ax.read_context",
                     "ax.form_summary",
                     "ax.aif.ping",
                     "ax.odata.get:Customers",
                     "ax.invoke:OK",
                     "ax.setvalue:Field=Value",
                     "ax.expand:Tab"
                 })
        {
            var r = AutomationTokenReadiness.Classify(token, settings);
            sb.AppendLine("- " + r.Mode.ToUpperInvariant() + " · " + token + " · " + r.Reason);
        }

        sb.AppendLine();
        sb.AppendLine("Foreground probe");
        sb.AppendLine(ForegroundUiAutomationContext.BuildFormSummary(settings, 24, 8));
        return sb.ToString().TrimEnd();
    }

    public static string BuildServiceInventoryReport()
    {
        var services = GetServiceInventory();
        var sb = new StringBuilder();
        sb.AppendLine("Backend service inventory");
        sb.AppendLine("Total public service classes: " + services.Count);
        sb.AppendLine();
        foreach (var group in services.GroupBy(ClassifyServiceArea))
        {
            sb.AppendLine("## " + group.Key);
            foreach (var s in group)
                sb.AppendLine("- " + s);
            sb.AppendLine();
        }

        return sb.ToString().TrimEnd();
    }

    public static string BuildRuntimeFilesReport()
    {
        var root = AppPaths.RepoRoot;
        var data = Path.Combine(root, "windows", "data");
        var sb = new StringBuilder();
        sb.AppendLine("Runtime files");
        sb.AppendLine("Repo root: " + root);
        sb.AppendLine("Data path: " + data);
        sb.AppendLine();

        if (!Directory.Exists(data))
        {
            sb.AppendLine("(windows/data missing)");
            return sb.ToString().TrimEnd();
        }

        foreach (var f in Directory.GetFiles(data).OrderBy(Path.GetFileName))
        {
            var info = new FileInfo(f);
            sb.AppendLine("- " + info.Name + " · " + info.Length + " bytes · " + info.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss"));
        }

        return sb.ToString().TrimEnd();
    }

    private static IReadOnlyList<string> GetServiceInventory() =>
        typeof(ActionHistoryService).Assembly
            .GetTypes()
            .Where(t => t.IsClass && t.IsPublic && t.Namespace == "CarolusNexus.Services")
            .Select(t => t.Name)
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static string ClassifyServiceArea(string serviceName)
    {
        var n = serviceName.ToLowerInvariant();
        if (n.Contains("ax")) return "AX / ERP";
        if (n.Contains("ai") || n.Contains("llm") || n.Contains("embedding")) return "AI";
        if (n.Contains("ritual") || n.Contains("flow") || n.Contains("plan")) return "Plans / Flows";
        if (n.Contains("automation") || n.Contains("uia") || n.Contains("win32") || n.Contains("foreground")) return "Desktop Automation";
        if (n.Contains("knowledge")) return "Knowledge";
        if (n.Contains("evidence") || n.Contains("audit") || n.Contains("risk") || n.Contains("recovery")) return "Trust";
        if (n.Contains("watch") || n.Contains("screen") || n.Contains("ocr")) return "Watch / Vision";
        if (n.Contains("speech") || n.Contains("voice") || n.Contains("mic") || n.Contains("talk")) return "Voice";
        return "Core";
    }

    private static string NextStepFor(BackendCapabilityRow row, NexusSettings settings)
    {
        if (row.Area == "AX" && !settings.AxIntegrationEnabled)
            return "enable AX integration in Setup and configure OData/AIF/COM test backend";
        if (row.RuntimeStatus.Contains("guarded", StringComparison.OrdinalIgnoreCase))
            return "switch Safety profile to power-user and pass PlanGuard";
        if (row.RuntimeStatus.Contains("configured", StringComparison.OrdinalIgnoreCase))
            return "verify local device/backend setup end-to-end";
        if (row.RuntimeStatus.Contains("report", StringComparison.OrdinalIgnoreCase))
            return "turn report into first-class workflow cards where needed";
        return "no immediate action";
    }
}
