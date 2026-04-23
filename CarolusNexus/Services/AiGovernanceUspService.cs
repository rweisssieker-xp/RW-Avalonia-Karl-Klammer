using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CarolusNexus.Models;

namespace CarolusNexus.Services;

public static class AiGovernanceUspService
{
    private static readonly string[] MutationWords =
    [
        "send", "mail.", "outlook.", "teams.", "delete", "remove", "post", "book", "approve", "submit",
        "write", "save", "upload", "download", "create", "update", "insert", "modify", "commit", "execute"
    ];

    public static string BuildEvidenceModeReport(NexusSettings settings, string? currentPrompt = null)
    {
        var insight = OperatorInsightService.BuildSnapshot(settings);
        var rag = KnowledgeSnippetService.BuildAugmentationResult(currentPrompt, 3600);
        var watch = WatchSessionService.LoadOrEmpty();
        var sb = new StringBuilder();
        sb.AppendLine("AI Evidence Mode");
        sb.AppendLine($"Provider: {settings.Provider} / key {(DotEnvStore.HasProviderKey(settings.Provider) ? "present" : "missing")}");
        sb.AppendLine($"Live context: {Show(insight.ProcessName)} / {Show(insight.WindowTitle)} / {insight.AdapterFamily}");
        sb.AppendLine($"RAG tier: {rag.Tier}");
        sb.AppendLine($"RAG sources: {rag.Bundle.Sources.Count}");
        sb.AppendLine($"Watch evidence entries: {watch.Entries.Count}");
        sb.AppendLine($"Operator posture: {insight.OperatorPosture}");
        sb.AppendLine();
        sb.AppendLine("Evidence used:");
        sb.AppendLine("- Foreground process/window/adapter heuristic");
        sb.AppendLine("- Local knowledge retrieval tier and source refs");
        sb.AppendLine("- Watch-session history when present");
        sb.AppendLine("- Safety profile, denylist, and approval posture");
        sb.AppendLine();
        sb.AppendLine("Assumptions to show explicitly:");
        if (!DotEnvStore.HasProviderKey(settings.Provider))
            sb.AppendLine("- Live LLM call may be unavailable until provider key is configured.");
        if (rag.Bundle.Sources.Count == 0)
            sb.AppendLine("- No local RAG source supports the prompt yet.");
        if (string.IsNullOrWhiteSpace(insight.ProcessName))
            sb.AppendLine("- No foreground application evidence was captured.");
        if (!settings.IncludeUiaContextInAsk)
            sb.AppendLine("- UIA tree is not included in Ask prompts unless enabled.");
        if (watch.Entries.Count == 0)
            sb.AppendLine("- No process-history evidence exists yet; watch mode can collect it.");
        if (DotEnvStore.HasProviderKey(settings.Provider) && rag.Bundle.Sources.Count > 0 && !string.IsNullOrWhiteSpace(insight.ProcessName))
            sb.AppendLine("- Evidence baseline is strong enough for a grounded demo answer.");
        sb.AppendLine();
        sb.AppendLine("User-facing answer contract:");
        sb.AppendLine("Every high-value answer should include Evidence, Assumptions, Risk, Safe next action, and Approval boundary.");
        return sb.ToString().TrimEnd();
    }

    public static string BuildProcessMiningReport(NexusSettings settings)
    {
        var doc = WatchSessionService.LoadOrEmpty();
        var sb = new StringBuilder();
        sb.AppendLine("Process Mining Light");
        sb.AppendLine($"Watch entries: {doc.Entries.Count}");
        if (doc.Entries.Count == 0)
        {
            sb.AppendLine("No watch history yet. Switch to watch mode, work normally for 10-20 minutes, then rerun this report.");
            return sb.ToString().TrimEnd();
        }

        var byFamily = doc.Entries
            .GroupBy(e => string.IsNullOrWhiteSpace(e.AdapterFamily) ? "generic" : e.AdapterFamily!.Trim())
            .Select(g => new { Family = g.Key, Count = g.Count(), Last = g.Max(e => e.UtcAt) })
            .OrderByDescending(x => x.Count)
            .ThenByDescending(x => x.Last)
            .Take(8)
            .ToList();
        var byProcess = doc.Entries
            .GroupBy(e => string.IsNullOrWhiteSpace(e.ProcessName) ? "unknown" : e.ProcessName!.Trim())
            .Select(g => new { Process = g.Key, Count = g.Count(), LastTitle = g.LastOrDefault()?.WindowTitle ?? "" })
            .OrderByDescending(x => x.Count)
            .Take(8)
            .ToList();

        sb.AppendLine();
        sb.AppendLine("Top app families:");
        foreach (var x in byFamily)
            sb.AppendLine($"- {x.Family}: {x.Count} entries, last {x.Last.ToLocalTime():HH:mm:ss}");
        sb.AppendLine();
        sb.AppendLine("Top processes:");
        foreach (var x in byProcess)
            sb.AppendLine($"- {x.Process}: {x.Count} entries, sample \"{Shorten(x.LastTitle, 54)}\"");
        sb.AppendLine();
        sb.AppendLine("Automation candidates:");
        foreach (var x in byFamily.Where(x => x.Count >= 3))
            sb.AppendLine($"- {x.Family}: repeated context detected. Create a read-first flow template and add approval gates for mutations.");
        if (!byFamily.Any(x => x.Count >= 3))
            sb.AppendLine("- Not enough repetition yet. Need at least 3 entries in the same app family.");
        sb.AppendLine();
        sb.AppendLine("ROI hook:");
        sb.AppendLine("Repeated foreground patterns become prioritized automation candidates with evidence, not guesswork.");
        return sb.ToString().TrimEnd();
    }

    public static string BuildAutonomyAndMutationReport(NexusSettings settings, IReadOnlyList<RecipeStep>? steps = null)
    {
        steps ??= Array.Empty<RecipeStep>();
        var level = InferAutonomyLevel(settings, steps);
        var mutations = steps.Select((s, i) => new { Step = s, Index = i + 1, Mutation = IsMutationLike(s) }).ToList();
        var writeCount = mutations.Count(x => x.Mutation);
        var blockedCount = steps.Count(s => !PlanGuard.IsAllowed(settings, (s.ActionType + " " + s.ActionArgument).Trim()));
        var sb = new StringBuilder();
        sb.AppendLine("Autonomy Levels + Safe Mutation Detector");
        sb.AppendLine($"Current autonomy level: L{level.Level} - {level.Label}");
        sb.AppendLine($"Safety profile: {settings.Safety.Profile}");
        sb.AppendLine($"Steps inspected: {steps.Count}");
        sb.AppendLine($"Mutation-like steps: {writeCount}");
        sb.AppendLine($"Blocked by policy/denylist: {blockedCount}");
        sb.AppendLine();
        sb.AppendLine("Level guide:");
        sb.AppendLine("L0 read only · L1 suggest · L2 prepare · L3 execute with approval · L4 bounded autonomous execution");
        if (steps.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Step risk scan:");
            foreach (var x in mutations.Take(20))
            {
                var raw = $"{x.Step.ActionType} {x.Step.ActionArgument}".Trim();
                var allowed = PlanGuard.IsAllowed(settings, raw);
                var checkpoint = x.Step.Checkpoint ? "checkpoint" : "no checkpoint";
                sb.AppendLine($"- #{x.Index}: {(x.Mutation ? "MUTATION" : "read/safe")} / {(allowed ? "allowed" : "blocked")} / {checkpoint} / {Shorten(raw, 90)}");
            }
        }
        else
        {
            sb.AppendLine();
            sb.AppendLine("No plan steps supplied. Use this report after Ask creates a plan or after generating an AI Opportunity Flow.");
        }
        sb.AppendLine();
        sb.AppendLine("USP:");
        sb.AppendLine("The app can explain how autonomous it is before it acts, and can flag write/send/post/delete risks before execution.");
        return sb.ToString().TrimEnd();
    }

    public static AutomationRecipe CreateProcessMiningFlow(NexusSettings settings)
    {
        var report = BuildProcessMiningReport(settings);
        var insight = OperatorInsightService.BuildSnapshot(settings);
        var recipe = new AutomationRecipe
        {
            Id = Guid.NewGuid().ToString("n"),
            Name = "Process Mining Candidate - " + Shorten(insight.AdapterFamily + " " + DateTime.Now.ToString("HHmm"), 44),
            Description = "Generated from watch-session patterns. Review before publishing.",
            Category = "Process Mining",
            PublicationState = "draft",
            ApprovalMode = "manual",
            RiskLevel = "low",
            AdapterAffinity = insight.AdapterFamily,
            ConfidenceSource = "WatchSessionService pattern analysis",
            MaxAutonomySteps = 1,
            Steps =
            [
                new RecipeStep
                {
                    ActionType = "ai.process_mining_report",
                    ActionArgument = report,
                    WaitMs = 100,
                    OnFailure = "skip",
                    Channel = "ui"
                },
                new RecipeStep
                {
                    ActionType = "checkpoint",
                    ActionArgument = "Human selects which repeated process should become a real automation flow.",
                    WaitMs = 100,
                    Checkpoint = true,
                    OnFailure = "stop",
                    Channel = "ui"
                }
            ]
        };
        RitualRecipeStore.AppendRecipe(recipe);
        return recipe;
    }

    public static string ExportGovernanceProofPack(NexusSettings settings, string? currentPrompt = null, IReadOnlyList<RecipeStep>? steps = null)
    {
        Directory.CreateDirectory(AppPaths.DataDir);
        var path = Path.Combine(AppPaths.DataDir, $"ai-governance-proof-{DateTime.Now:yyyyMMdd-HHmmss}.md");
        var sb = new StringBuilder();
        sb.AppendLine("# Carolus Nexus AI Governance Proof");
        sb.AppendLine();
        sb.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();
        sb.AppendLine("## Evidence Mode");
        sb.AppendLine(BuildEvidenceModeReport(settings, currentPrompt));
        sb.AppendLine();
        sb.AppendLine("## Process Mining Light");
        sb.AppendLine(BuildProcessMiningReport(settings));
        sb.AppendLine();
        sb.AppendLine("## Autonomy + Mutation Risk");
        sb.AppendLine(BuildAutonomyAndMutationReport(settings, steps));
        File.WriteAllText(path, sb.ToString());
        return path;
    }

    private static (int Level, string Label) InferAutonomyLevel(NexusSettings settings, IReadOnlyList<RecipeStep> steps)
    {
        if (steps.Count == 0)
            return (1, "suggest only");
        var hasMutation = steps.Any(IsMutationLike);
        var checkpoints = steps.Count(s => s.Checkpoint);
        if (!hasMutation)
            return (2, "prepare/read-first");
        if (checkpoints > 0 || string.Equals(settings.Safety.Profile, "power-user", StringComparison.OrdinalIgnoreCase))
            return (3, "execute only with approval/gates");
        return (1, "suggest only; mutation lacks approval gate");
    }

    private static bool IsMutationLike(RecipeStep step)
    {
        var raw = (step.ActionType + " " + step.ActionArgument).ToLowerInvariant();
        return MutationWords.Any(raw.Contains);
    }

    private static string Show(string? value) => string.IsNullOrWhiteSpace(value) ? "(empty)" : value.Trim();

    private static string Shorten(string? value, int max)
    {
        value = (value ?? "").ReplaceLineEndings(" ").Trim();
        return value.Length <= max ? value : value[..max].TrimEnd() + "...";
    }
}
