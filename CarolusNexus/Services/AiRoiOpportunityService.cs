using System;
using System.IO;
using System.Linq;
using System.Text;
using CarolusNexus.Models;

namespace CarolusNexus.Services;

public static class AiRoiOpportunityService
{
    public static string BuildRoiReport(NexusSettings settings, string? currentPrompt = null)
    {
        var recipes = SafeRecipes();
        var watch = WatchSessionService.LoadOrEmpty();
        var repeatedWindows = watch.Entries
            .Where(e => !string.IsNullOrWhiteSpace(e.WindowTitle))
            .GroupBy(e => Normalize(e.WindowTitle!))
            .Where(g => g.Count() >= 2)
            .OrderByDescending(g => g.Count())
            .Take(6)
            .ToList();

        var flowCount = recipes.Count;
        var published = recipes.Count(r => string.Equals(r.PublicationState, "published", StringComparison.OrdinalIgnoreCase));
        var axFlows = recipes.Count(r => string.Equals(r.AdapterAffinity, "ax2012", StringComparison.OrdinalIgnoreCase));
        var watchSignals = watch.Entries.Count;
        var repetitionSignals = repeatedWindows.Sum(g => g.Count());
        var weeklyMinutes = Math.Clamp((flowCount * 18) + (published * 12) + (axFlows * 20) + (repetitionSignals * 4), 15, 2400);
        var yearlyHours = Math.Round(weeklyMinutes * 46d / 60d, 1);
        var riskDiscount = settings.Safety.Profile.Equals("strict", StringComparison.OrdinalIgnoreCase) ? 0.72d : 0.86d;
        var confidence = ScoreConfidence(flowCount, watchSignals, published);
        var adjustedHours = Math.Round(yearlyHours * riskDiscount * confidence, 1);

        var sb = new StringBuilder();
        sb.AppendLine("AI ROI + Opportunity Scoring");
        sb.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();
        sb.AppendLine("Executive estimate");
        sb.AppendLine($"- Flow inventory: {flowCount} total, {published} published, {axFlows} AX-affine.");
        sb.AppendLine($"- Watch evidence: {watchSignals} foreground observations, {repeatedWindows.Count} repeated work patterns.");
        sb.AppendLine($"- Gross automation potential: ~{weeklyMinutes} minutes/week.");
        sb.AppendLine($"- Annualized potential: ~{yearlyHours} hours/year.");
        sb.AppendLine($"- Risk-adjusted realizable value: ~{adjustedHours} hours/year at confidence {confidence:P0}.");
        sb.AppendLine();
        sb.AppendLine("Top opportunity signals");
        if (repeatedWindows.Count == 0)
        {
            sb.AppendLine("- No repeated watch patterns yet. Enable watch mode during real AX/operator work to collect stronger ROI signals.");
        }
        else
        {
            foreach (var group in repeatedWindows)
                sb.AppendLine($"- {group.Count()}x repeated window pattern: {group.Key}");
        }

        sb.AppendLine();
        sb.AppendLine("Prioritized AI plays");
        sb.AppendLine("- P0: Convert repeated AX screens into operator-flow candidates with evidence mode enabled.");
        sb.AppendLine("- P0: Use AI Evaluation Lab before demo claims; benchmark prompt quality, citation strength, and hallucination risk.");
        sb.AppendLine("- P1: Add approval gates only where risk is high; keep low-risk read/prepare tasks one-click.");
        sb.AppendLine("- P1: Package ROI report into the pilot deal room as a measurable before/after baseline.");
        sb.AppendLine("- P2: Track per-flow saved time after each dry-run/execute cycle and feed it back into this score.");

        if (!string.IsNullOrWhiteSpace(currentPrompt))
        {
            sb.AppendLine();
            sb.AppendLine("Prompt-specific angle");
            sb.AppendLine("- Current prompt can be used as a candidate demand signal; convert it into a pilot metric if repeated by users.");
        }

        return sb.ToString().TrimEnd();
    }

    public static string BuildOpportunityMatrix(NexusSettings settings, string? currentPrompt = null)
    {
        var recipes = SafeRecipes();
        var hasAx = recipes.Any(r => string.Equals(r.AdapterAffinity, "ax2012", StringComparison.OrdinalIgnoreCase)) || settings.AxIntegrationEnabled;
        var hasRag = settings.UseLocalKnowledge && File.Exists(AppPaths.KnowledgeIndex);
        var hasWatch = WatchSessionService.LoadOrEmpty().Entries.Count > 0;
        var sb = new StringBuilder();
        sb.AppendLine("AI Opportunity Matrix");
        sb.AppendLine("Area | Impact | Readiness | Next proof");
        sb.AppendLine("--- | --- | --- | ---");
        sb.AppendLine($"AX operator co-pilot | Very high | {(hasAx ? "medium/high" : "medium")} | Run context-to-flow on a real AX screen");
        sb.AppendLine($"RAG evidence answers | High | {(hasRag ? "high" : "medium")} | Export AI brief with source confidence");
        sb.AppendLine($"Watch-to-automation mining | High | {(hasWatch ? "medium/high" : "low/medium")} | Capture 30 minutes of operator work");
        sb.AppendLine("Governed autonomy | High | medium | Demonstrate dry-run, gate, resume, and audit package");
        sb.AppendLine("Pilot deal-room automation | Medium/high | high | Export USP Studio pack and ROI report");
        return sb.ToString().TrimEnd();
    }

    public static string ExportRoiPack(NexusSettings settings, string? currentPrompt = null)
    {
        Directory.CreateDirectory(AppPaths.DataDir);
        var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var dir = Path.Combine(AppPaths.DataDir, $"ai-roi-{stamp}");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "00-ai-roi-report.md"), BuildRoiReport(settings, currentPrompt) + Environment.NewLine);
        File.WriteAllText(Path.Combine(dir, "01-opportunity-matrix.md"), BuildOpportunityMatrix(settings, currentPrompt) + Environment.NewLine);
        File.WriteAllText(Path.Combine(dir, "index.md"),
            "# AI ROI Opportunity Pack\n\n"
            + $"- Generated: {stamp}\n"
            + "- [AI ROI Report](00-ai-roi-report.md)\n"
            + "- [Opportunity Matrix](01-opportunity-matrix.md)\n");
        return dir;
    }

    private static double ScoreConfidence(int flowCount, int watchSignals, int published)
    {
        var score = 0.35d;
        if (flowCount > 0) score += 0.2d;
        if (published > 0) score += 0.15d;
        if (watchSignals >= 5) score += 0.15d;
        if (watchSignals >= 20) score += 0.1d;
        return Math.Clamp(score, 0.35d, 0.95d);
    }

    private static string Normalize(string value)
    {
        var text = value.Trim();
        if (text.Length > 72)
            text = text[..72];
        return text.Replace("|", "/");
    }

    private static System.Collections.Generic.List<AutomationRecipe> SafeRecipes()
    {
        try { return RitualRecipeStore.LoadAll(); }
        catch { return new System.Collections.Generic.List<AutomationRecipe>(); }
    }
}
