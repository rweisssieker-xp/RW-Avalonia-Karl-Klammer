using System;
using System.IO;
using System.Linq;
using System.Text;
using CarolusNexus.Models;

namespace CarolusNexus.Services;

public static class OperatorUspPackService
{
    public static string BuildUspRadar(NexusSettings settings)
    {
        var insight = OperatorInsightService.BuildSnapshot(settings);
        var recipes = SafeLoadRecipes();
        var axFlows = recipes.Count(r => string.Equals(r.AdapterAffinity, "ax2012", StringComparison.OrdinalIgnoreCase));
        var published = recipes.Count(r => string.Equals(r.PublicationState, "published", StringComparison.OrdinalIgnoreCase));
        var proofSignals = 0;
        if (DotEnvStore.HasProviderKey(settings.Provider)) proofSignals++;
        if (Directory.Exists(AppPaths.KnowledgeDir) && Directory.GetFiles(AppPaths.KnowledgeDir).Length > 0) proofSignals++;
        if (File.Exists(AppPaths.KnowledgeIndex) || File.Exists(AppPaths.KnowledgeFtsDb)) proofSignals++;
        if (ForegroundWindowInfo.TryRead().Title.Length > 0) proofSignals++;
        if (recipes.Count > 0) proofSignals++;
        if (File.Exists(AppPaths.RitualStepAudit) || File.Exists(AppPaths.ActionHistory)) proofSignals++;

        var readinessPercent = insight.ReadinessMax <= 0 ? 0 : insight.ReadinessScore * 100 / insight.ReadinessMax;
        var score = Math.Min(100, (readinessPercent * 2 / 3) + proofSignals * 6);
        var sb = new StringBuilder();
        sb.AppendLine($"USP score: {score}/100");
        sb.AppendLine($"Operator readiness: {readinessPercent}%");
        sb.AppendLine($"Live context: {insight.AdapterFamily} / {Show(insight.ProcessName)} / {Show(insight.WindowTitle)}");
        sb.AppendLine($"Likely work: {insight.LikelyTask}");
        sb.AppendLine($"Safe next action: {insight.SafeNextAction}");
        sb.AppendLine($"Flow library: {recipes.Count} flows, {published} published, {axFlows} AX-affine");
        sb.AppendLine("10x claims:");
        sb.AppendLine("- Foreground app becomes a guarded operator flow in one click.");
        sb.AppendLine("- AX/UIA context is captured as evidence before automation.");
        sb.AppendLine("- Demo/audit pack is exportable without external services.");
        sb.AppendLine("- Human checkpoint and risk labels are embedded in generated flows.");
        sb.AppendLine("Next demo move:");
        sb.AppendLine(score >= 80
            ? "Run Context-to-Flow on a live AX or Office window, then export the proof pack."
            : "Add provider key/knowledge/one published AX flow, then export the proof pack.");
        return sb.ToString().TrimEnd();
    }

    public static string ExportProofPack(NexusSettings settings)
    {
        Directory.CreateDirectory(AppPaths.DataDir);
        var (title, proc) = ForegroundWindowInfo.TryRead();
        var fam = OperatorAdapterRegistry.ResolveFamily(proc, title);
        var insight = OperatorInsightService.BuildSnapshot(settings);
        var recipes = SafeLoadRecipes();
        var path = Path.Combine(AppPaths.DataDir, $"usp-proof-pack-{DateTime.Now:yyyyMMdd-HHmmss}.md");
        var sb = new StringBuilder();
        sb.AppendLine("# Carolus Nexus USP Proof Pack");
        sb.AppendLine();
        sb.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();
        sb.AppendLine("## Executive proof");
        sb.AppendLine(BuildUspRadar(settings));
        sb.AppendLine();
        sb.AppendLine("## AI command proof");
        sb.AppendLine(AiUspCommandService.BuildPromptPack(settings, ""));
        sb.AppendLine();
        sb.AppendLine("## Live foreground");
        sb.AppendLine($"- Process: {Show(proc)}");
        sb.AppendLine($"- Title: {Show(title)}");
        sb.AppendLine($"- Adapter family: {fam}");
        sb.AppendLine();
        sb.AppendLine("## Readiness signals");
        sb.AppendLine($"- Provider: {settings.Provider}");
        sb.AppendLine($"- Provider key: {(DotEnvStore.HasProviderKey(settings.Provider) ? "present" : "missing")}");
        sb.AppendLine($"- Knowledge files: {CountFiles(AppPaths.KnowledgeDir)}");
        sb.AppendLine($"- Knowledge index: {(File.Exists(AppPaths.KnowledgeIndex) ? "present" : "missing")}");
        sb.AppendLine($"- Local FTS: {(File.Exists(AppPaths.KnowledgeFtsDb) ? "present" : "missing")}");
        sb.AppendLine($"- Watch mode: {string.Equals(settings.Mode, "watch", StringComparison.OrdinalIgnoreCase)}");
        sb.AppendLine($"- Power-user mode: {string.Equals(settings.Safety.Profile, "power-user", StringComparison.OrdinalIgnoreCase)}");
        sb.AppendLine();
        sb.AppendLine("## Operator recommendation");
        sb.AppendLine($"- Posture: {insight.OperatorPosture}");
        sb.AppendLine($"- Recommended flow: {insight.RecommendedFlow}");
        sb.AppendLine($"- Safe next action: {insight.SafeNextAction}");
        sb.AppendLine($"- Risky action to avoid: {insight.RiskyAction}");
        sb.AppendLine();
        sb.AppendLine("## Flow inventory");
        foreach (var r in recipes.Take(25))
            sb.AppendLine($"- {Show(r.Name)} | {Show(r.PublicationState)} | risk {Show(r.RiskLevel)} | adapter {Show(r.AdapterAffinity)} | steps {r.Steps.Count}");
        if (recipes.Count > 25)
            sb.AppendLine($"- ... {recipes.Count - 25} more");
        File.WriteAllText(path, sb.ToString());
        return path;
    }

    public static AutomationRecipe CreateFlowFromForeground(NexusSettings settings)
    {
        var (title, proc) = ForegroundWindowInfo.TryRead();
        var fam = OperatorAdapterRegistry.ResolveFamily(proc, title);
        var safeTitle = Shorten(string.IsNullOrWhiteSpace(title) ? "current window" : title.Trim(), 54);
        var recipe = new AutomationRecipe
        {
            Id = Guid.NewGuid().ToString("n"),
            Name = $"Context Flow - {safeTitle}",
            Description = $"Generated from the active foreground window. Process={Show(proc)}, adapter={fam}.",
            Category = "Live Context",
            PublicationState = "draft",
            ApprovalMode = "manual",
            RiskLevel = fam == "ax2012" ? "low" : "medium",
            AdapterAffinity = fam,
            ConfidenceSource = "Live Context foreground snapshot + OperatorInsightService",
            MaxAutonomySteps = 2
        };

        if (fam == "ax2012")
        {
            recipe.Steps.Add(Step("ax.read_context", "", proc, title, checkpoint: false));
            recipe.Steps.Add(Step("ax.form_summary", "", proc, title, checkpoint: true));
            recipe.Steps.Add(Step("checkpoint", "Human approval before write or posting action", proc, title, checkpoint: true));
        }
        else if (fam == "browser")
        {
            recipe.Steps.Add(Step("context.read", $"browser|{proc}|{title}", proc, title, checkpoint: false));
            recipe.Steps.Add(Step("checkpoint", "Confirm target tab and task before navigation or form fill", proc, title, checkpoint: true));
        }
        else if (fam == "excel")
        {
            recipe.Steps.Add(Step("context.read", $"excel|{proc}|{title}", proc, title, checkpoint: false));
            recipe.Steps.Add(Step("checkpoint", "Confirm workbook and selected range before edits", proc, title, checkpoint: true));
        }
        else
        {
            recipe.Steps.Add(Step("context.read", $"{fam}|{proc}|{title}", proc, title, checkpoint: false));
            recipe.Steps.Add(Step("checkpoint", "Confirm target app before any mutation", proc, title, checkpoint: true));
        }

        RitualRecipeStore.AppendRecipe(recipe);
        return recipe;
    }

    private static RecipeStep Step(string actionType, string argument, string proc, string title, bool checkpoint)
    {
        return new RecipeStep
        {
            ActionType = actionType,
            ActionArgument = argument,
            WaitMs = 250,
            RetryCount = 0,
            GuardProcessContains = string.IsNullOrWhiteSpace(proc) ? null : proc,
            GuardWindowTitleContains = string.IsNullOrWhiteSpace(title) ? null : Shorten(title, 32),
            GuardStopRunOnMismatch = false,
            OnFailure = checkpoint ? "stop" : "skip",
            Checkpoint = checkpoint,
            Channel = "ui"
        };
    }

    private static string Show(string? value) => string.IsNullOrWhiteSpace(value) ? "(empty)" : value.Trim();

    private static string Shorten(string value, int max)
    {
        value = value.ReplaceLineEndings(" ").Trim();
        return value.Length <= max ? value : value[..max].TrimEnd() + "...";
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
