using System;
using System.IO;
using System.Linq;
using System.Text;
using CarolusNexus.Models;

namespace CarolusNexus.Services;

public static class CompetitiveUspService
{
    public static string BuildBattlecard(NexusSettings settings, string? currentPrompt = null)
    {
        var recipes = SafeLoadRecipes();
        var insight = OperatorInsightService.BuildSnapshot(settings);
        var rag = KnowledgeSnippetService.BuildAugmentationResult(currentPrompt, 1800);
        var sb = new StringBuilder();
        sb.AppendLine("Competitive Battlecard");
        sb.AppendLine();
        sb.AppendLine("Position:");
        sb.AppendLine("Carolus Nexus is not another generic chat window. It is a governed Windows AI operator cockpit for local knowledge, foreground app context, AX/Office work, audit proof, and guarded flow execution.");
        sb.AppendLine();
        sb.AppendLine("Versus generic ChatGPT/Copilot:");
        sb.AppendLine("- Adds live foreground context, flow generation, mutation scan, proof exports, and local operator governance.");
        sb.AppendLine("- Shows RAG gaps and assumptions instead of hiding weak evidence.");
        sb.AppendLine();
        sb.AppendLine("Versus classic RPA:");
        sb.AppendLine("- Starts from observed human context and AI evidence, not brittle recorder-only scripts.");
        sb.AppendLine("- Generates reviewable draft flows and keeps human approval where risk exists.");
        sb.AppendLine();
        sb.AppendLine("Versus BI/reporting:");
        sb.AppendLine("- Acts at the work surface: AX, Office, browser, files, and live desktop tasks.");
        sb.AppendLine("- Converts repeated work into automation candidates.");
        sb.AppendLine();
        sb.AppendLine("Current proof points:");
        sb.AppendLine($"- Foreground adapter: {insight.AdapterFamily} / {insight.ProcessName}");
        sb.AppendLine($"- RAG tier: {rag.Tier}");
        sb.AppendLine($"- Flow library: {recipes.Count} flows");
        sb.AppendLine($"- Governance posture: {insight.OperatorPosture}");
        sb.AppendLine($"- Pilot score available: yes");
        return sb.ToString().TrimEnd();
    }

    public static string BuildFeatureMatrix(NexusSettings settings, string? currentPrompt = null)
    {
        var recipes = SafeLoadRecipes();
        var watch = WatchSessionService.LoadOrEmpty();
        var rag = KnowledgeSnippetService.BuildAugmentationResult(currentPrompt, 1200);
        var sb = new StringBuilder();
        sb.AppendLine("USP Feature Matrix");
        sb.AppendLine("| Capability | Status | Demo proof |");
        sb.AppendLine("|---|---:|---|");
        sb.AppendLine($"| Live Windows context | {(OperatingSystem.IsWindows() ? "ready" : "missing")} | Live Context tab |");
        sb.AppendLine($"| Local RAG | {rag.Tier} | Ask retrieval/context pane |");
        sb.AppendLine($"| Evidence Mode | ready | Ask -> Evidence mode |");
        sb.AppendLine($"| Safe Mutation Detector | ready | Ask -> Safe mutation scan |");
        sb.AppendLine($"| Process Mining Light | {(watch.Entries.Count >= 3 ? "ready" : "needs history")} | Ask -> Process mining |");
        sb.AppendLine($"| Context-to-Flow | ready | Live Context -> Context -> Flow |");
        sb.AppendLine($"| AI Opportunity Flow | ready | Ask -> AI opportunity flow |");
        sb.AppendLine($"| Pilot Deal Room | ready | Ask -> Export pilot deal room |");
        sb.AppendLine($"| AX posture | {(settings.AxIntegrationEnabled ? "enabled" : "disabled")} | Live Context AX / proof packs |");
        sb.AppendLine($"| Published flows | {recipes.Count(r => string.Equals(r.PublicationState, "published", StringComparison.OrdinalIgnoreCase))} | Rituals flow library |");
        return sb.ToString().TrimEnd();
    }

    public static string BuildWinUiReleaseReadiness(NexusSettings settings, string? currentPrompt = null)
    {
        var scorecard = PilotReadinessUspService.BuildPilotScorecard(settings, currentPrompt);
        var recipes = SafeLoadRecipes();
        var sb = new StringBuilder();
        sb.AppendLine("WinUI Release Readiness");
        sb.AppendLine();
        sb.AppendLine("Go criteria:");
        sb.AppendLine("- Ask has AI USP, Governance, and Pilot/Sales command surfaces.");
        sb.AppendLine("- Live Context can create flows and proof packs.");
        sb.AppendLine("- Diagnostics include runtime, RAG, evidence, process mining, autonomy, and pilot readiness.");
        sb.AppendLine("- Rituals can review, publish, queue, resume, test, and export flows.");
        sb.AppendLine();
        sb.AppendLine("Current release signals:");
        sb.AppendLine($"- Flow count: {recipes.Count}");
        sb.AppendLine($"- Published flows: {recipes.Count(r => string.Equals(r.PublicationState, "published", StringComparison.OrdinalIgnoreCase))}");
        sb.AppendLine($"- Provider key: {(DotEnvStore.HasProviderKey(settings.Provider) ? "present" : "missing")}");
        sb.AppendLine($"- Knowledge files: {CountFiles(AppPaths.KnowledgeDir)}");
        sb.AppendLine($"- AX enabled: {settings.AxIntegrationEnabled}");
        sb.AppendLine();
        sb.AppendLine("Recommended release stance:");
        sb.AppendLine("WinUI can be positioned as the primary app shell when layout smoke test passes. Avalonia remains useful as fallback/reference until WinUI visual QA and installer/distribution are signed off.");
        sb.AppendLine();
        sb.AppendLine("Embedded pilot score:");
        sb.AppendLine(scorecard);
        return sb.ToString().TrimEnd();
    }

    public static string ExportCompetitivePack(NexusSettings settings, string? currentPrompt = null)
    {
        Directory.CreateDirectory(AppPaths.DataDir);
        var path = Path.Combine(AppPaths.DataDir, $"competitive-usp-pack-{DateTime.Now:yyyyMMdd-HHmmss}.md");
        var sb = new StringBuilder();
        sb.AppendLine("# Carolus Nexus Competitive USP Pack");
        sb.AppendLine();
        sb.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();
        sb.AppendLine("## Battlecard");
        sb.AppendLine(BuildBattlecard(settings, currentPrompt));
        sb.AppendLine();
        sb.AppendLine("## Feature Matrix");
        sb.AppendLine(BuildFeatureMatrix(settings, currentPrompt));
        sb.AppendLine();
        sb.AppendLine("## WinUI Release Readiness");
        sb.AppendLine(BuildWinUiReleaseReadiness(settings, currentPrompt));
        File.WriteAllText(path, sb.ToString());
        return path;
    }

    private static int CountFiles(string dir)
    {
        try { return Directory.Exists(dir) ? Directory.GetFiles(dir).Length : 0; }
        catch { return 0; }
    }

    private static System.Collections.Generic.List<AutomationRecipe> SafeLoadRecipes()
    {
        try { return RitualRecipeStore.LoadAll(); }
        catch { return new System.Collections.Generic.List<AutomationRecipe>(); }
    }
}
