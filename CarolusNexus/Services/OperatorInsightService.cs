using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CarolusNexus.Models;

namespace CarolusNexus.Services;

public sealed class OperatorInsightSnapshot
{
    public int ReadinessScore { get; init; }
    public int ReadinessMax { get; init; } = 6;
    public string Usp { get; init; } = "";
    public string ProcessName { get; init; } = "";
    public string WindowTitle { get; init; } = "";
    public string AdapterFamily { get; init; } = "generic";
    public string LikelyTask { get; init; } = "";
    public string SafeNextAction { get; init; } = "";
    public string RiskyAction { get; init; } = "";
    public string RecommendedFlow { get; init; } = "";
    public string WatchSummary { get; init; } = "";
    public string ContextReplay { get; init; } = "";
    public string OperatorPosture { get; init; } = "";
}

public sealed class FlowQualitySnapshot
{
    public int Score { get; init; }
    public int Max { get; init; } = 100;
    public string Grade { get; init; } = "C";
    public int StepCount { get; init; }
    public int WriteLikeSteps { get; init; }
    public int AxLikeSteps { get; init; }
    public int WaitSteps { get; init; }
    public string Summary { get; init; } = "";
}

public static class OperatorInsightService
{
    public static OperatorInsightSnapshot BuildSnapshot(NexusSettings settings)
    {
        var (title, proc) = OperatingSystem.IsWindows()
            ? ForegroundWindowInfo.TryRead()
            : ("", "");
        var family = OperatingSystem.IsWindows()
            ? OperatorAdapterRegistry.ResolveFamily(proc, title)
            : "generic";
        var watch = WatchSessionService.LoadOrEmpty();
        var keyOk = DotEnvStore.HasProviderKey(settings.Provider);
        var hasKnowledge = Directory.Exists(AppPaths.KnowledgeDir) && Directory.GetFiles(AppPaths.KnowledgeDir).Length > 0;
        var hasIndex = File.Exists(AppPaths.KnowledgeIndex) || File.Exists(AppPaths.KnowledgeChunks);
        var liveContext = OperatingSystem.IsWindows();
        var powerUser = string.Equals(settings.Safety.Profile, "power-user", StringComparison.OrdinalIgnoreCase);
        var watchMode = string.Equals(settings.Mode, "watch", StringComparison.OrdinalIgnoreCase);
        var toolHost = settings.EnableLocalToolHost;

        var score = 0;
        score += keyOk ? 1 : 0;
        score += hasKnowledge || hasIndex ? 1 : 0;
        score += liveContext ? 1 : 0;
        score += powerUser ? 1 : 0;
        score += watchMode ? 1 : 0;
        score += toolHost ? 1 : 0;

        var likelyTask = InferLikelyTask(family, title);
        return new OperatorInsightSnapshot
        {
            ReadinessScore = score,
            Usp = score >= 5
                ? "Governed local AI cockpit: Windows context, knowledge, watch history, and safety posture in one operator view."
                : "Privacy-aware AI desktop assistant: local context and knowledge become guarded operator suggestions.",
            ProcessName = proc,
            WindowTitle = title,
            AdapterFamily = family,
            LikelyTask = likelyTask,
            SafeNextAction = InferSafeNextAction(family, hasKnowledge || hasIndex),
            RiskyAction = InferRiskyAction(family, powerUser),
            RecommendedFlow = InferRecommendedFlow(family, likelyTask),
            WatchSummary = FormatWatchSummary(watch, settings.Mode),
            ContextReplay = FormatContextReplay(watch.Entries),
            OperatorPosture = powerUser ? "power-user guarded execution" : "simulation / confirmation-first"
        };
    }

    public static FlowQualitySnapshot ScoreFlow(IReadOnlyList<RecipeStep> steps, NexusSettings settings)
    {
        if (steps.Count == 0)
            return new FlowQualitySnapshot { Score = 0, StepCount = 0, Grade = "—", Summary = "No plan steps detected." };

        var writeLike = steps.Count(IsWriteLike);
        var axLike = steps.Count(IsAxLike);
        var waitSteps = steps.Count(s => s.WaitMs > 0);
        var score = 55;
        score += Math.Min(20, steps.Count * 2);
        score += waitSteps > 0 ? 8 : 0;
        score -= writeLike * 10;
        score -= axLike * 8;
        if (!string.Equals(settings.Safety.Profile, "power-user", StringComparison.OrdinalIgnoreCase) && writeLike > 0)
            score -= 12;
        score = Math.Clamp(score, 0, 100);

        var grade = score switch
        {
            >= 85 => "A",
            >= 70 => "B",
            >= 55 => "C",
            >= 40 => "D",
            _ => "E"
        };

        return new FlowQualitySnapshot
        {
            Score = score,
            Grade = grade,
            StepCount = steps.Count,
            WriteLikeSteps = writeLike,
            AxLikeSteps = axLike,
            WaitSteps = waitSteps,
            Summary = $"Quality {grade} ({score}/100): {steps.Count} steps, {writeLike} write-like, {axLike} AX-like, {waitSteps} waits."
        };
    }

    private static string InferLikelyTask(string family, string title) =>
        family switch
        {
            "excel" => "Review or reconcile spreadsheet data",
            "word" => "Draft or review document content",
            "browser" => "Research, lookup, or web workflow",
            "outlook" or "mail" => "Triage or draft communication",
            "teams" => "Meeting or collaboration follow-up",
            "explorer" => "Find, organize, or import local files",
            "ax2012" => "ERP lookup, validation, or transaction support",
            _ => string.IsNullOrWhiteSpace(title) ? "No active task inferred yet" : "Inspect active window and summarize context"
        };

    private static string InferSafeNextAction(string family, bool hasKnowledge) =>
        family switch
        {
            "ax2012" => "Read foreground context and summarize fields before any write action.",
            "excel" => "Summarize the visible workbook and propose a validation checklist.",
            "browser" => "Capture page context and retrieve matching local knowledge.",
            "outlook" or "mail" => "Draft a response as text only; do not send automatically.",
            _ => hasKnowledge ? "Summarize active context with local knowledge." : "Refresh active app and ask for a context summary."
        };

    private static string InferRiskyAction(string family, bool powerUser) =>
        family switch
        {
            "ax2012" => powerUser ? "Writing ERP data still requires explicit confirmation." : "ERP write actions remain blocked/guarded.",
            "outlook" or "mail" => "Sending messages should remain manual or confirmed.",
            "browser" => "Posting, booking, or submitting forms needs confirmation.",
            _ => powerUser ? "Plan execution is possible but should stay stepwise for demos." : "Automation remains simulation/guarded."
        };

    private static string InferRecommendedFlow(string family, string likelyTask) =>
        family switch
        {
            "ax2012" => "AX context capture -> validation checklist -> operator approval",
            "excel" => "Spreadsheet review -> anomaly notes -> export summary",
            "browser" => "Research page -> knowledge match -> next action draft",
            "outlook" or "mail" => "Email context -> draft response -> manual send",
            _ => likelyTask + " -> summary -> safe next step"
        };

    private static string FormatWatchSummary(WatchSessionService.WatchDocument doc, string mode)
    {
        if (doc.Entries.Count == 0)
            return $"{mode}: no watch entries";
        var last = doc.Entries[^1].UtcAt.ToLocalTime();
        return $"{mode}: {doc.Entries.Count} entries, latest {last:HH:mm:ss}";
    }

    private static string FormatContextReplay(IReadOnlyList<WatchSessionService.WatchEntry> entries)
    {
        if (entries.Count == 0)
            return "No watch replay yet.";

        var sb = new StringBuilder();
        foreach (var e in entries.Skip(Math.Max(0, entries.Count - 6)))
        {
            var t = e.UtcAt.ToLocalTime();
            var proc = string.IsNullOrWhiteSpace(e.ProcessName) ? "?" : e.ProcessName;
            var title = e.WindowTitle ?? "";
            if (title.Length > 54)
                title = title[..54] + "...";
            sb.AppendLine($"{t:HH:mm:ss} | {proc} | {e.AdapterFamily ?? "generic"} | {title}");
        }

        return sb.ToString().TrimEnd();
    }

    private static bool IsWriteLike(RecipeStep step)
    {
        var a = step.ActionArgument ?? "";
        return a.Contains("[ACTION:", StringComparison.OrdinalIgnoreCase)
               || a.Contains("click", StringComparison.OrdinalIgnoreCase)
               || a.Contains("type", StringComparison.OrdinalIgnoreCase)
               || a.Contains("send", StringComparison.OrdinalIgnoreCase)
               || a.Contains("post", StringComparison.OrdinalIgnoreCase)
               || a.Contains("book", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsAxLike(RecipeStep step)
    {
        var a = step.ActionArgument ?? "";
        return a.Contains("ax.", StringComparison.OrdinalIgnoreCase)
               || a.Contains("ax|", StringComparison.OrdinalIgnoreCase)
               || a.Contains("dynamics", StringComparison.OrdinalIgnoreCase);
    }
}
