using System;
using System.IO;
using System.Text;
using CarolusNexus.Models;

namespace CarolusNexus.Services;

public static class AiUspCommandService
{
    public static string BuildRagGapReport(NexusSettings settings, string? currentPrompt = null)
    {
        var rag = KnowledgeSnippetService.BuildAugmentationResult(currentPrompt, 3200);
        var keyOk = DotEnvStore.HasProviderKey(settings.Provider);
        var hasKnowledgeDir = Directory.Exists(AppPaths.KnowledgeDir);
        var knowledgeFiles = hasKnowledgeDir ? SafeCountFiles(AppPaths.KnowledgeDir) : 0;
        var hasChunks = File.Exists(AppPaths.KnowledgeChunks);
        var hasFts = File.Exists(AppPaths.KnowledgeFtsDb);
        var hasEmbeddings = File.Exists(AppPaths.KnowledgeEmbeddings);
        var sb = new StringBuilder();
        sb.AppendLine("AI/RAG Gap Report");
        sb.AppendLine($"Provider key: {(keyOk ? "ready" : "missing")} ({settings.Provider})");
        sb.AppendLine($"Knowledge folder: {(hasKnowledgeDir ? "present" : "missing")}");
        sb.AppendLine($"Knowledge files: {knowledgeFiles}");
        sb.AppendLine($"Chunks: {(hasChunks ? "present" : "missing")}");
        sb.AppendLine($"FTS index: {(hasFts ? "present" : "missing")}");
        sb.AppendLine($"Embeddings: {(hasEmbeddings ? "present" : "missing")}");
        sb.AppendLine($"Current retrieval tier: {rag.Tier}");
        sb.AppendLine();
        sb.AppendLine("Gaps to close for max KI USP:");
        if (!keyOk) sb.AppendLine("- Add provider key in .env for live LLM execution.");
        if (knowledgeFiles == 0) sb.AppendLine("- Add real SOPs, AX process notes, screenshots, exports, or pilot docs to knowledge.");
        if (!hasChunks) sb.AppendLine("- Rebuild knowledge index to create chunked retrieval.");
        if (!hasFts) sb.AppendLine("- Rebuild FTS index for offline keyword fallback.");
        if (!hasEmbeddings) sb.AppendLine("- Build embeddings for semantic RAG demos.");
        if (!settings.IncludeUiaContextInAsk) sb.AppendLine("- Enable UIA context in Ask for stronger live-app grounding.");
        if (!string.Equals(settings.Safety.Profile, "power-user", StringComparison.OrdinalIgnoreCase))
            sb.AppendLine("- Switch to power-user only in a safe demo environment when real UI execution should be shown.");
        if (keyOk && knowledgeFiles > 0 && hasChunks && (hasFts || hasEmbeddings))
            sb.AppendLine("- No critical RAG gap detected. Next: run an AI Opportunity Flow and export the proof pack.");
        if (rag.Hints.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Retrieval hints:");
            foreach (var h in rag.Hints)
                sb.AppendLine("- " + h);
        }
        return sb.ToString().TrimEnd();
    }

    public static string BuildExecutiveOnePager(NexusSettings settings, string? currentPrompt = null)
    {
        var insight = OperatorInsightService.BuildSnapshot(settings);
        var readinessPercent = insight.ReadinessMax <= 0 ? 0 : insight.ReadinessScore * 100 / insight.ReadinessMax;
        var sb = new StringBuilder();
        sb.AppendLine("Executive AI One-Pager");
        sb.AppendLine();
        sb.AppendLine("Positioning:");
        sb.AppendLine("Carolus Nexus is a governed AI operator cockpit for Windows-heavy enterprise work: local knowledge, live desktop context, AX/Office awareness, and auditable human approval gates.");
        sb.AppendLine();
        sb.AppendLine("Top USPs:");
        sb.AppendLine("- Live application context becomes an AI-grounded workflow, not a generic chat response.");
        sb.AppendLine("- Local RAG provides evidence and assumptions before decisions.");
        sb.AppendLine("- AX and Office tasks stay read-first with explicit checkpoints before mutation.");
        sb.AppendLine("- Proof packs turn demos into audit-ready pilot artifacts.");
        sb.AppendLine("- Same AI features are visible in Avalonia and WinUI.");
        sb.AppendLine();
        sb.AppendLine("Current proof signals:");
        sb.AppendLine($"- Readiness: {readinessPercent}% ({insight.ReadinessScore}/{insight.ReadinessMax})");
        sb.AppendLine($"- Provider: {settings.Provider} / key {(DotEnvStore.HasProviderKey(settings.Provider) ? "present" : "missing")}");
        sb.AppendLine($"- Live context: {insight.ProcessName} / {insight.AdapterFamily}");
        sb.AppendLine($"- Safe next action: {insight.SafeNextAction}");
        sb.AppendLine($"- Operator posture: {insight.OperatorPosture}");
        sb.AppendLine();
        sb.AppendLine("Pilot demo script:");
        sb.AppendLine("1. Open AX, Office, browser, or the target business app.");
        sb.AppendLine("2. Use Live Context -> Context-to-Flow to create a guarded draft flow.");
        sb.AppendLine("3. Use Ask -> AI USP Prompt Pack to generate a grounded operator prompt.");
        sb.AppendLine("4. Use Ask -> AI Opportunity Flow to create a reviewable AI workflow.");
        sb.AppendLine("5. Export AI USP Brief and USP Proof Pack as pilot evidence.");
        sb.AppendLine();
        sb.AppendLine("Commercial hook:");
        sb.AppendLine("Reduce time from task discovery to governed automation proposal from days to minutes, while preserving human approval and local evidence.");
        return sb.ToString().TrimEnd();
    }

    public static string BuildPromptPack(NexusSettings settings, string? currentPrompt = null)
    {
        var insight = OperatorInsightService.BuildSnapshot(settings);
        var rag = KnowledgeSnippetService.BuildAugmentationResult(currentPrompt, 2400);
        var sb = new StringBuilder();
        sb.AppendLine("AI USP Prompt Pack");
        sb.AppendLine($"Provider: {settings.Provider} / key {(DotEnvStore.HasProviderKey(settings.Provider) ? "present" : "missing")}");
        sb.AppendLine($"RAG tier: {rag.Tier}");
        sb.AppendLine($"Live app: {insight.ProcessName} / {insight.AdapterFamily}");
        sb.AppendLine($"Readiness: {insight.ReadinessScore}/{insight.ReadinessMax}");
        sb.AppendLine();
        sb.AppendLine("1. Executive AX operator prompt");
        sb.AppendLine("Inspect the active AX context, cite local knowledge if relevant, and produce a guarded read-first plan. Do not write, post, send, delete, or approve without explicit human approval.");
        sb.AppendLine();
        sb.AppendLine("2. Pilot proof prompt");
        sb.AppendLine("Create a pilot-ready proof summary: current context, available evidence, missing credentials/data, safest next action, and the exact flow that should be reviewed before execution.");
        sb.AppendLine();
        sb.AppendLine("3. Flow extraction prompt");
        sb.AppendLine("Convert this task into JSON steps with actionType, actionArgument, waitMs, risk, guardProcessContains, guardWindowTitleContains, and checkpoint fields. Every write action needs a checkpoint.");
        sb.AppendLine();
        sb.AppendLine("4. Knowledge-grounded answer prompt");
        sb.AppendLine("Answer using only local RAG snippets and live context. Mark assumptions explicitly. If evidence is missing, say what document or AX screen is needed.");
        sb.AppendLine();
        sb.AppendLine("Suggested next prompt:");
        sb.AppendLine(BuildBestPrompt(settings, currentPrompt));
        if (rag.Hints.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("RAG hints:");
            foreach (var h in rag.Hints)
                sb.AppendLine("- " + h);
        }
        return sb.ToString().TrimEnd();
    }

    public static string BuildBestPrompt(NexusSettings settings, string? currentPrompt = null)
    {
        var insight = OperatorInsightService.BuildSnapshot(settings);
        var task = string.IsNullOrWhiteSpace(currentPrompt)
            ? insight.LikelyTask
            : currentPrompt.Trim();
        return "Use Carolus Nexus as a governed AI operator. "
               + $"Current task: {task}. "
               + $"Foreground context: {insight.ProcessName} / {insight.WindowTitle}. "
               + "Use local knowledge when available, cite evidence, create a guarded plan, label every risk, and stop before any irreversible action.";
    }

    public static AutomationRecipe CreateAiOpportunityFlow(NexusSettings settings, string? currentPrompt = null)
    {
        var insight = OperatorInsightService.BuildSnapshot(settings);
        var (title, proc) = ForegroundWindowInfo.TryRead();
        var prompt = BuildBestPrompt(settings, currentPrompt);
        var recipe = new AutomationRecipe
        {
            Id = Guid.NewGuid().ToString("n"),
            Name = "AI Opportunity - " + Shorten(insight.AdapterFamily + " " + insight.LikelyTask, 48),
            Description = "AI-generated opportunity flow for demo/pilot review. Starts with RAG/context proof, then requires human approval.",
            Category = "AI USP",
            PublicationState = "draft",
            ApprovalMode = "manual",
            RiskLevel = "medium",
            AdapterAffinity = insight.AdapterFamily,
            ConfidenceSource = "AI USP command pack + live context + local RAG readiness",
            MaxAutonomySteps = 1
        };
        recipe.Steps.Add(new RecipeStep
        {
            ActionType = "ai.prompt_pack",
            ActionArgument = prompt,
            WaitMs = 100,
            GuardProcessContains = string.IsNullOrWhiteSpace(proc) ? null : proc,
            GuardWindowTitleContains = string.IsNullOrWhiteSpace(title) ? null : Shorten(title, 32),
            GuardStopRunOnMismatch = false,
            OnFailure = "skip",
            Channel = "ui"
        });
        recipe.Steps.Add(new RecipeStep
        {
            ActionType = "checkpoint",
            ActionArgument = "Human review: verify citations, target app, risk labels, and approval boundary before execution.",
            WaitMs = 100,
            Checkpoint = true,
            OnFailure = "stop",
            Channel = "ui"
        });
        RitualRecipeStore.AppendRecipe(recipe);
        return recipe;
    }

    public static AutomationRecipe CreateAiDemoScriptFlow(NexusSettings settings, string? currentPrompt = null)
    {
        var insight = OperatorInsightService.BuildSnapshot(settings);
        var (title, proc) = ForegroundWindowInfo.TryRead();
        var recipe = new AutomationRecipe
        {
            Id = Guid.NewGuid().ToString("n"),
            Name = "AI Demo Script - " + Shorten(insight.AdapterFamily + " " + DateTime.Now.ToString("HHmm"), 48),
            Description = "Demo flow that proves KI/RAG/live-context USPs with explicit evidence export and human checkpoint.",
            Category = "AI USP",
            PublicationState = "draft",
            ApprovalMode = "manual",
            RiskLevel = "low",
            AdapterAffinity = insight.AdapterFamily,
            ConfidenceSource = "AI demo script generator",
            MaxAutonomySteps = 1
        };
        recipe.Steps.Add(DemoStep("ai.rag_gap_report", BuildRagGapReport(settings, currentPrompt), proc, title, checkpoint: false));
        recipe.Steps.Add(DemoStep("ai.executive_onepager", BuildExecutiveOnePager(settings, currentPrompt), proc, title, checkpoint: false));
        recipe.Steps.Add(DemoStep("ai.prompt_pack", BuildBestPrompt(settings, currentPrompt), proc, title, checkpoint: false));
        recipe.Steps.Add(DemoStep("checkpoint", "Present proof pack and get human approval before any write/execution step.", proc, title, checkpoint: true));
        RitualRecipeStore.AppendRecipe(recipe);
        return recipe;
    }

    public static string ExportAiBrief(NexusSettings settings, string? currentPrompt = null)
    {
        Directory.CreateDirectory(AppPaths.DataDir);
        var path = Path.Combine(AppPaths.DataDir, $"ai-usp-brief-{DateTime.Now:yyyyMMdd-HHmmss}.md");
        var sb = new StringBuilder();
        sb.AppendLine("# Carolus Nexus AI USP Brief");
        sb.AppendLine();
        sb.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();
        sb.AppendLine("## Prompt pack");
        sb.AppendLine(BuildPromptPack(settings, currentPrompt));
        sb.AppendLine();
        sb.AppendLine("## Operator USP radar");
        sb.AppendLine(OperatorUspPackService.BuildUspRadar(settings));
        sb.AppendLine();
        sb.AppendLine("## RAG gap report");
        sb.AppendLine(BuildRagGapReport(settings, currentPrompt));
        sb.AppendLine();
        sb.AppendLine("## Executive one-pager");
        sb.AppendLine(BuildExecutiveOnePager(settings, currentPrompt));
        sb.AppendLine();
        sb.AppendLine("## AI governance proof");
        sb.AppendLine(AiGovernanceUspService.BuildEvidenceModeReport(settings, currentPrompt));
        sb.AppendLine();
        sb.AppendLine(AiGovernanceUspService.BuildProcessMiningReport(settings));
        sb.AppendLine();
        sb.AppendLine(AiGovernanceUspService.BuildAutonomyAndMutationReport(settings));
        File.WriteAllText(path, sb.ToString());
        return path;
    }

    public static string ExportExecutiveOnePager(NexusSettings settings, string? currentPrompt = null)
    {
        Directory.CreateDirectory(AppPaths.DataDir);
        var path = Path.Combine(AppPaths.DataDir, $"ai-executive-onepager-{DateTime.Now:yyyyMMdd-HHmmss}.md");
        File.WriteAllText(path, BuildExecutiveOnePager(settings, currentPrompt));
        return path;
    }

    private static RecipeStep DemoStep(string actionType, string argument, string proc, string title, bool checkpoint)
    {
        return new RecipeStep
        {
            ActionType = actionType,
            ActionArgument = argument,
            WaitMs = 100,
            GuardProcessContains = string.IsNullOrWhiteSpace(proc) ? null : proc,
            GuardWindowTitleContains = string.IsNullOrWhiteSpace(title) ? null : Shorten(title, 32),
            GuardStopRunOnMismatch = false,
            OnFailure = checkpoint ? "stop" : "skip",
            Checkpoint = checkpoint,
            Channel = "ui"
        };
    }

    private static int SafeCountFiles(string dir)
    {
        try { return Directory.Exists(dir) ? Directory.GetFiles(dir).Length : 0; }
        catch { return 0; }
    }

    private static string Shorten(string value, int max)
    {
        value = (value ?? "").ReplaceLineEndings(" ").Trim();
        return value.Length <= max ? value : value[..max].TrimEnd() + "...";
    }
}
