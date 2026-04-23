using System;
using System.IO;
using System.Linq;
using System.Text;
using CarolusNexus.Models;

namespace CarolusNexus.Services;

public static class PilotReadinessUspService
{
    public static string BuildPilotScorecard(NexusSettings settings, string? currentPrompt = null)
    {
        var recipes = SafeLoadRecipes();
        var watch = WatchSessionService.LoadOrEmpty();
        var insight = OperatorInsightService.BuildSnapshot(settings);
        var rag = KnowledgeSnippetService.BuildAugmentationResult(currentPrompt, 2200);
        var provider = DotEnvStore.HasProviderKey(settings.Provider);
        var knowledgeFiles = CountFiles(AppPaths.KnowledgeDir);
        var hasIndex = File.Exists(AppPaths.KnowledgeIndex) || File.Exists(AppPaths.KnowledgeChunks);
        var hasSemanticOrFts = File.Exists(AppPaths.KnowledgeEmbeddings) || File.Exists(AppPaths.KnowledgeFtsDb);
        var hasFlows = recipes.Count > 0;
        var hasPublishedFlow = recipes.Any(r => string.Equals(r.PublicationState, "published", StringComparison.OrdinalIgnoreCase));
        var hasAxProof = settings.AxIntegrationEnabled || recipes.Any(r => string.Equals(r.AdapterAffinity, "ax2012", StringComparison.OrdinalIgnoreCase));
        var hasGovernance = settings.Safety.PanicStopEnabled && settings.HighRiskSecondConfirm && settings.Safety.NeverAutoSend && settings.Safety.NeverAutoPostBook;
        var hasWatch = watch.Entries.Count >= 3;

        var score = 0;
        score += provider ? 12 : 0;
        score += knowledgeFiles > 0 ? 10 : 0;
        score += hasIndex ? 10 : 0;
        score += hasSemanticOrFts ? 8 : 0;
        score += OperatingSystem.IsWindows() ? 10 : 0;
        score += hasFlows ? 10 : 0;
        score += hasPublishedFlow ? 8 : 0;
        score += hasAxProof ? 10 : 0;
        score += hasGovernance ? 12 : 0;
        score += hasWatch ? 10 : 0;
        score = Math.Clamp(score, 0, 100);

        var stage = score switch
        {
            >= 85 => "Pilot-ready",
            >= 70 => "Strong demo-ready",
            >= 50 => "Demo-ready with gaps",
            _ => "Prototype/demo preparation needed"
        };

        var sb = new StringBuilder();
        sb.AppendLine("Pilot Readiness Scorecard");
        sb.AppendLine($"Score: {score}/100 - {stage}");
        sb.AppendLine($"Foreground: {insight.ProcessName} / {insight.AdapterFamily}");
        sb.AppendLine($"RAG tier: {rag.Tier}");
        sb.AppendLine($"Flows: {recipes.Count} total, {recipes.Count(r => string.Equals(r.PublicationState, "published", StringComparison.OrdinalIgnoreCase))} published");
        sb.AppendLine($"Watch entries: {watch.Entries.Count}");
        sb.AppendLine();
        sb.AppendLine("Score signals:");
        sb.AppendLine($"- Provider key: {(provider ? "ready" : "missing")}");
        sb.AppendLine($"- Knowledge docs: {knowledgeFiles}");
        sb.AppendLine($"- RAG index: {(hasIndex ? "ready" : "missing")}");
        sb.AppendLine($"- Semantic/FTS fallback: {(hasSemanticOrFts ? "ready" : "missing")}");
        sb.AppendLine($"- Windows live context: {(OperatingSystem.IsWindows() ? "ready" : "missing")}");
        sb.AppendLine($"- Flow library: {(hasFlows ? "ready" : "empty")}");
        sb.AppendLine($"- Published flow: {(hasPublishedFlow ? "ready" : "missing")}");
        sb.AppendLine($"- AX proof: {(hasAxProof ? "ready" : "missing")}");
        sb.AppendLine($"- Governance gates: {(hasGovernance ? "ready" : "incomplete")}");
        sb.AppendLine($"- Watch/process mining: {(hasWatch ? "ready" : "needs more history")}");
        sb.AppendLine();
        sb.AppendLine("Top next actions:");
        foreach (var action in NextActions(provider, knowledgeFiles, hasIndex, hasSemanticOrFts, hasFlows, hasPublishedFlow, hasWatch, hasGovernance).Take(6))
            sb.AppendLine("- " + action);
        sb.AppendLine();
        sb.AppendLine("Pilot storyline:");
        sb.AppendLine(BuildPilotStoryline(settings, currentPrompt));
        return sb.ToString().TrimEnd();
    }

    public static string BuildPilotStoryline(NexusSettings settings, string? currentPrompt = null)
    {
        var insight = OperatorInsightService.BuildSnapshot(settings);
        var task = string.IsNullOrWhiteSpace(currentPrompt) ? insight.LikelyTask : currentPrompt.Trim();
        var sb = new StringBuilder();
        sb.AppendLine("1. Show the current business app and live context.");
        sb.AppendLine($"2. Ask Carolus Nexus to handle: {task}");
        sb.AppendLine("3. Display Evidence Mode: sources, assumptions, risk, approval boundary.");
        sb.AppendLine("4. Generate AI Opportunity Flow and Safe Mutation Scan.");
        sb.AppendLine("5. Export Governance Proof + AI USP Brief as pilot evidence.");
        sb.AppendLine("6. Close with ROI: repeated desktop work becomes governed automation candidates.");
        return sb.ToString().TrimEnd();
    }

    public static string BuildBuyerObjectionPack(NexusSettings settings, string? currentPrompt = null)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Buyer Objection Pack");
        sb.AppendLine();
        sb.AppendLine("Objection: AI is not auditable.");
        sb.AppendLine("Answer: Evidence Mode and Governance Proof export sources, assumptions, risk, approval boundaries, and runtime context.");
        sb.AppendLine();
        sb.AppendLine("Objection: Agents are unsafe in ERP.");
        sb.AppendLine("Answer: AX flows are read-first, write-like steps are detected, and posting/booking/sending can stay blocked by policy.");
        sb.AppendLine();
        sb.AppendLine("Objection: RAG quality is unclear.");
        sb.AppendLine("Answer: RAG Gap Report exposes retrieval tier, missing chunks/FTS/embeddings, and source count.");
        sb.AppendLine();
        sb.AppendLine("Objection: Automation discovery is manual.");
        sb.AppendLine("Answer: Process Mining Light turns watch history into repeated-process candidates.");
        sb.AppendLine();
        sb.AppendLine("Objection: This is only a demo shell.");
        sb.AppendLine("Answer: The app creates real local flow drafts, audit/proof packs, diagnostics, and guarded execution plans.");
        sb.AppendLine();
        sb.AppendLine("Recommended close:");
        sb.AppendLine(BuildPilotStoryline(settings, currentPrompt));
        return sb.ToString().TrimEnd();
    }

    public static string ExportPilotDealRoom(NexusSettings settings, string? currentPrompt = null)
    {
        Directory.CreateDirectory(AppPaths.DataDir);
        var path = Path.Combine(AppPaths.DataDir, $"pilot-deal-room-{DateTime.Now:yyyyMMdd-HHmmss}.md");
        var sb = new StringBuilder();
        sb.AppendLine("# Carolus Nexus Pilot Deal Room");
        sb.AppendLine();
        sb.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();
        sb.AppendLine("## Pilot Readiness");
        sb.AppendLine(BuildPilotScorecard(settings, currentPrompt));
        sb.AppendLine();
        sb.AppendLine("## Buyer Objections");
        sb.AppendLine(BuildBuyerObjectionPack(settings, currentPrompt));
        sb.AppendLine();
        sb.AppendLine("## AI Governance");
        sb.AppendLine(AiGovernanceUspService.BuildEvidenceModeReport(settings, currentPrompt));
        sb.AppendLine();
        sb.AppendLine(AiGovernanceUspService.BuildAutonomyAndMutationReport(settings));
        sb.AppendLine();
        sb.AppendLine("## AI USP Brief");
        sb.AppendLine(AiUspCommandService.BuildExecutiveOnePager(settings, currentPrompt));
        File.WriteAllText(path, sb.ToString());
        return path;
    }

    private static string[] NextActions(bool provider, int knowledgeFiles, bool hasIndex, bool hasSemanticOrFts, bool hasFlows, bool hasPublishedFlow, bool hasWatch, bool hasGovernance)
    {
        var actions = new System.Collections.Generic.List<string>();
        if (!provider) actions.Add("Configure provider key for live LLM demos.");
        if (knowledgeFiles == 0) actions.Add("Add 3-5 real SOP/AX/process documents to knowledge.");
        if (!hasIndex) actions.Add("Rebuild knowledge index to create chunked RAG.");
        if (!hasSemanticOrFts) actions.Add("Build embeddings or FTS so retrieval quality is explainable.");
        if (!hasFlows) actions.Add("Create one Context-to-Flow and one AI Opportunity Flow.");
        if (!hasPublishedFlow) actions.Add("Review and publish one low-risk demo flow.");
        if (!hasWatch) actions.Add("Run watch mode long enough to collect at least 3 repeated process entries.");
        if (!hasGovernance) actions.Add("Enable panic stop, second confirmation, never-auto-send, and never-auto-post/book.");
        if (actions.Count == 0) actions.Add("Run the pilot script and export Pilot Deal Room for stakeholders.");
        return actions.ToArray();
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
