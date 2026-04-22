using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using CarolusNexus.Models;

namespace CarolusNexus.Services;

public sealed record MissionPlan(
    string Goal,
    string PlanText,
    string FilePath,
    string RiskLevel,
    double Confidence,
    string DecisionReason,
    IReadOnlyList<string> GuardHints,
    List<RecipeStep> Steps,
    bool RequiresManualApproval,
    bool UsedAi);

public sealed record MissionExecutionLine(
    int StepIndex,
    string Channel,
    string ActionArgument,
    string Result);

public sealed record MissionExecutionReport(
    string Status,
    List<LessonEntry> StepLog,
    int SuccessCount,
    int FailureCount,
    int BlockedCount,
    string Transcript,
    bool Completed);

public sealed record LessonEntry(
    int StepIndex,
    string StepSummary,
    string Result,
    int RetryCount,
    bool Recovered);

public enum MissionIntentKind
{
    Baseline,
    OrbitalBatch,
    Predictive,
    ComplianceAudit
}

public sealed record MissionIntent(
    MissionIntentKind Kind,
    string CanonicalGoal,
    string? ContextHint = null);

public static class MissionOrchestrator
{
    public static async Task<MissionPlan> BuildAsync(NexusSettings settings, string goal, CancellationToken ct = default)
    {
        var requestedGoal = string.IsNullOrWhiteSpace(goal)
            ? "Autonomous Work OS baseline mission."
            : goal.Trim();
        var intent = MissionIntentIntake.Parse(requestedGoal);

        var context = MissionContextOracle.Capture(settings, requestedGoal, intent);
        var usedAi = false;
        var planJson = "";
        if (DotEnvStore.HasProviderKey(settings.Provider))
        {
            var prompt = BuildMissionPrompt(intent, context);
            const string system =
                "You are a mission planner for a desktop operator assistant. Output strict JSON only.\n" +
                "JSON schema: { goal, planText, riskLevel, confidence, decisionReason, requiresManualApproval, steps[] }.\n" +
                "Each step has: actionType, actionArgument, waitMs, channel, retryCount, retryDelayMs, guardProcessContains, guardWindowTitleContains, guardStopRunOnMismatch, onFailure, checkpoint.\n" +
                "riskLevel is one of low|medium|high.\n" +
                "Prefer concise actionArgument token style compatible with existing executors.";
            planJson = await LlmChatService.CompleteUtilityAsync(settings, system, prompt, ct).ConfigureAwait(false);
            if (!LooksLikeLlmError(planJson))
                usedAi = true;
            else
                planJson = "";
        }

        var mission = await ParseOrFallbackAsync(settings, requestedGoal, context, usedAi, planJson, ct).ConfigureAwait(false);
        mission = mission with
        {
            Goal = intent.CanonicalGoal,
            PlanText = mission.PlanText + Environment.NewLine + "Intent: " + intent.Kind,
            GuardHints = mission.GuardHints.Concat(
                new[] { $"Intent class: {intent.Kind}", $"Context hint: {intent.ContextHint ?? "n/a"}" }).ToList()
        };

        if (intent.Kind == MissionIntentKind.OrbitalBatch && mission.Steps.Count < 3)
            mission = MissionOrbitalEngine.EnhanceWithBatchFallback(mission);
        if (intent.Kind == MissionIntentKind.Predictive && mission.Steps.Count == 0)
            mission = MissionPredictiveEngine.EnhancePredictiveBaseline(mission);

        return mission;
    }

    private static async Task<MissionPlan> ParseOrFallbackAsync(
        NexusSettings settings,
        string goal,
        string context,
        bool usedAi,
        string llmOutput,
        CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(llmOutput))
        {
            var parsed = ParsePlanJson(settings, goal, context, usedAi, llmOutput);
            if (parsed != null && parsed.Steps.Count > 0)
                return parsed;
        }

        await Task.Yield();
        return BuildFallbackMission(settings, goal, context, usedAi);
    }

    private static MissionPlan BuildFallbackMission(NexusSettings settings, string goal, string context, bool usedAi)
    {
        var steps = new List<RecipeStep>
        {
            new()
            {
                ActionType = "token",
                ActionArgument = "ax.form_summary",
                Channel = "ui",
                WaitMs = 700,
                RetryCount = 1,
                RetryDelayMs = 400,
                GuardProcessContains = "ax2012",
                GuardWindowTitleContains = "",
                Checkpoint = true
            },
            new()
            {
                ActionType = "token",
                ActionArgument = "ax.read_context",
                Channel = "ui",
                WaitMs = 700,
                Checkpoint = true,
                RetryCount = 1,
                RetryDelayMs = 400
            }
        };

        var txt =
            "# Mission (Fallback)\n" +
            "Fallback mission built without LLM.\n" +
            $"Goal: {goal}\n" +
            $"Context: {context}\n";

        return WriteAndCreate(settings, goal, txt, usedAi, steps, "high", 0.48, "LLM unavailable; fallback mission.", true);
    }

    private static string BuildMissionPrompt(MissionIntent intent, string context)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Goal:");
        sb.AppendLine(intent.CanonicalGoal);
        sb.AppendLine();
        sb.AppendLine("Intent:");
        sb.AppendLine(intent.Kind.ToString());
        if (!string.IsNullOrWhiteSpace(intent.ContextHint))
        {
            sb.AppendLine();
            sb.AppendLine("Context hint:");
            sb.AppendLine(intent.ContextHint);
        }
        sb.AppendLine();
        sb.AppendLine("Context (read-only):");
        sb.AppendLine(context);
        sb.AppendLine();
        sb.AppendLine("Output constraints:");
        sb.AppendLine("- Strict JSON only.");
        sb.AppendLine("- No Markdown.");
        sb.AppendLine("- 2..12 actionable steps.");
        sb.AppendLine("- Use existing token formats (ax.*, [ACTION:...], uia.*, browser.open, explorer.open_path, script:cmd:..., api.get:..., etc.).");
        sb.AppendLine("- Keep actions safe and short.");
        return sb.ToString();
    }

    private static bool LooksLikeLlmError(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return true;
        if (text.TrimStart().StartsWith("{", StringComparison.Ordinal))
            return false;

        return text.Contains("Missing ", StringComparison.OrdinalIgnoreCase)
               || text.Contains("OPENAI", StringComparison.OrdinalIgnoreCase)
               || text.Contains("ANTHROPIC", StringComparison.OrdinalIgnoreCase)
               || text.StartsWith("Error", StringComparison.OrdinalIgnoreCase)
               || text.StartsWith("Unknown", StringComparison.OrdinalIgnoreCase);
    }

    private static MissionPlan? ParsePlanJson(NexusSettings settings, string goal, string context, bool usedAi, string output)
    {
        try
        {
            using var doc = JsonDocument.Parse(output);
            var root = doc.RootElement;
            var risk = root.TryGetProperty("riskLevel", out var riskEl)
                ? riskEl.GetString() ?? "medium"
                : "medium";
            var decisionReason = root.TryGetProperty("decisionReason", out var drEl)
                ? drEl.GetString() ?? string.Empty
                : string.Empty;
            var planText = root.TryGetProperty("planText", out var planEl)
                ? planEl.GetString() ?? ""
                : "";
            var requireApproval = root.TryGetProperty("requiresManualApproval", out var rapEl) && rapEl.GetBoolean();
            var conf = root.TryGetProperty("confidence", out var confEl) && confEl.TryGetDouble(out var c) ? c : 0.5;
            var steps = new List<RecipeStep>();

            if (root.TryGetProperty("steps", out var stepsEl) && stepsEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var stepEl in stepsEl.EnumerateArray())
                {
                    if (stepEl.ValueKind != JsonValueKind.Object)
                        continue;

                    var actionType = GetString(stepEl, "actionType") ?? "token";
                    var arg = GetString(stepEl, "actionArgument") ?? "";
                    var channel = GetString(stepEl, "channel") ?? "";
                    if (string.IsNullOrWhiteSpace(arg))
                        continue;

                    var step = new RecipeStep
                    {
                        ActionType = actionType,
                        ActionArgument = arg,
                        Channel = channel,
                        WaitMs = GetInt(stepEl, "waitMs"),
                        RetryCount = GetInt(stepEl, "retryCount"),
                        RetryDelayMs = GetInt(stepEl, "retryDelayMs", 400),
                        GuardProcessContains = GetString(stepEl, "guardProcessContains"),
                        GuardWindowTitleContains = GetString(stepEl, "guardWindowTitleContains"),
                        GuardStopRunOnMismatch = GetBool(stepEl, "guardStopRunOnMismatch", true),
                        OnFailure = GetString(stepEl, "onFailure") ?? "stop",
                        Checkpoint = GetBool(stepEl, "checkpoint"),
                        JumpToStepIndexOnFailure = null,
                        JumpToStepIndexOnSuccess = null,
                        FallbackCvTemplatePath = GetString(stepEl, "fallbackCvTemplatePath")
                    };
                    steps.Add(step);
                }
            }

            if (steps.Count == 0)
                return null;

            if (string.IsNullOrWhiteSpace(planText))
                planText = "Autonomous mission generated by AI planner.";

            return WriteAndCreate(settings, goal,
                planText + Environment.NewLine + Environment.NewLine + "Context: " + context,
                usedAi,
                steps, risk, conf, decisionReason, requireApproval);
        }
        catch
        {
            return null;
        }
    }

    private static string? GetString(JsonElement el, string name)
    {
        return el.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String
            ? p.GetString()
            : null;
    }

    private static int GetInt(JsonElement el, string name, int fallback = 0)
    {
        if (!el.TryGetProperty(name, out var p))
            return fallback;
        if (p.ValueKind == JsonValueKind.Number && p.TryGetInt32(out var i))
            return i;
        return fallback;
    }

    private static bool GetBool(JsonElement el, string name, bool fallback = false)
    {
        if (!el.TryGetProperty(name, out var p))
            return fallback;
        if (p.ValueKind == JsonValueKind.True || p.ValueKind == JsonValueKind.False)
            return p.GetBoolean();
        return fallback;
    }

    private static MissionPlan WriteAndCreate(
        NexusSettings settings,
        string goal,
        string planText,
        bool usedAi,
        List<RecipeStep> steps,
        string risk,
        double confidence,
        string decisionReason,
        bool requiresManualApproval)
    {
        var docsDir = Path.Combine(AppPaths.RepoRoot, "docs");
        Directory.CreateDirectory(docsDir);
        var fileName = $"mission-{DateTime.UtcNow:yyyyMMdd-HHmmss}.md";
        var filePath = Path.Combine(docsDir, fileName);

        var json = JsonSerializer.Serialize(
            new
            {
                goal,
                planText,
                riskLevel = risk,
                confidence,
                decisionReason,
                requiresManualApproval,
                steps,
                usedAi,
                safetyProfile = settings.Safety.Profile
            },
            new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(filePath, json, Encoding.UTF8);

        var guardHints = new List<string>
        {
            "File persisted as mission JSON",
            "Executor supports channel fallback: ui -> script -> api",
            "Execution auto-pauses on hard failures depending on step.onFailure"
        };

        return new MissionPlan(
            goal,
            $"Mission file: {fileName}{Environment.NewLine}{planText}",
            filePath,
            risk,
            confidence,
            decisionReason,
            guardHints,
            steps,
            requiresManualApproval,
            usedAi);
    }
}

public static class MissionContextOracle
{
    public static string Capture(NexusSettings settings, string goal, MissionIntent? forcedIntent = null)
    {
        var s = new StringBuilder();
        s.AppendLine($"goal={goal}");
        var d = ForegroundWindowInfo.TryReadDetail();
        if (d != null)
        {
            s.AppendLine($"foreground.title={d.Value.Title}");
            s.AppendLine($"foreground.process={d.Value.ProcessName}");
            s.AppendLine($"foreground.class={d.Value.WindowClass}");
        }
        else
        {
            s.AppendLine("foreground=no window");
        }

        s.AppendLine($"allowedAppFamilies={settings.Safety.AllowedAppFamilies}");
        s.AppendLine($"provider={settings.Provider}");
        s.AppendLine($"safetyProfile={settings.Safety.Profile}");
        s.AppendLine($"axEnabled={settings.AxIntegrationEnabled}");
        s.AppendLine($"speak={settings.SpeakResponses}");
        var intent = forcedIntent ?? MissionIntentIntake.Parse(goal);
        s.AppendLine($"intent={intent.Kind}");
        if (!string.IsNullOrWhiteSpace(intent.ContextHint))
            s.AppendLine($"intentHint={intent.ContextHint}");
        return s.ToString().Trim();
    }
}

public static class MissionIntentIntake
{
    public static MissionIntent Parse(string prompt)
    {
        var raw = string.IsNullOrWhiteSpace(prompt) ? string.Empty : prompt.Trim();
        var low = raw.ToLowerInvariant();

        if (low.StartsWith("orbit", StringComparison.Ordinal))
            return new MissionIntent(MissionIntentKind.OrbitalBatch, BuildOrbitalGoal(raw, "orbit"), "batch:high priority routine");
        if (low.StartsWith("autonomy orbit", StringComparison.Ordinal) || low.Contains("heute"))
            return new MissionIntent(MissionIntentKind.OrbitalBatch, BuildOrbitalGoal(raw, "autonomy"), "daily batch mission");
        if (low.StartsWith("predictive", StringComparison.Ordinal))
            return new MissionIntent(MissionIntentKind.Predictive, BuildPredictiveGoal(raw), "low-risk auto suggestion");
        if (low.Contains("compliance") && low.Contains("audit"))
            return new MissionIntent(MissionIntentKind.ComplianceAudit, raw, "zero-click compliance run");

        return new MissionIntent(MissionIntentKind.Baseline, raw, null);
    }

    private static string BuildOrbitalGoal(string raw, string source)
    {
        var baseGoal = Trim(raw, source);
        if (string.IsNullOrWhiteSpace(baseGoal))
            baseGoal = "run critical tasks for the current context";
        return "Orbital mission: " + baseGoal;
    }

    private static string BuildPredictiveGoal(string raw)
    {
        var baseGoal = Trim(raw, "predictive");
        return string.IsNullOrWhiteSpace(baseGoal)
            ? "analyze context and propose immediate next mission steps"
            : baseGoal;
    }

    private static string Trim(string raw, string prefix)
    {
        if (raw.Length <= prefix.Length)
            return "";
        return raw[prefix.Length..].TrimStart(' ', '\t', ':', '-', '–', '—');
    }
}

public static class MissionOrbitalEngine
{
    public static MissionPlan EnhanceWithBatchFallback(MissionPlan mission)
    {
        var steps = mission.Steps.Count == 0
            ? new List<RecipeStep>()
            : mission.Steps.ToList();

        if (steps.Count >= 3)
            return mission;

        steps.Add(new RecipeStep
        {
            ActionType = "token",
            ActionArgument = "ax.read_context",
            Channel = "ui",
            WaitMs = 500,
            RetryCount = 1,
            RetryDelayMs = 300,
            GuardProcessContains = "ax2012",
            GuardWindowTitleContains = "",
            Checkpoint = true
        });
        steps.Add(new RecipeStep
        {
            ActionType = "token",
            ActionArgument = "ax.form_summary",
            Channel = "ui",
            WaitMs = 500,
            RetryCount = 1,
            RetryDelayMs = 300,
            GuardProcessContains = "ax2012",
            Checkpoint = true
        });
        steps.Add(new RecipeStep
        {
            ActionType = "browser.open",
            ActionArgument = "https://outlook.office.com/mail/",
            Channel = "browser",
            WaitMs = 300,
            Checkpoint = false
        });

        return mission with
        {
            PlanText = mission.PlanText + "\n[orbital fallback injected for batch mode]",
            Steps = steps,
            RiskLevel = "medium",
            GuardHints = mission.GuardHints.Concat(["Orbital fallback: read_context + form_summary + Outlook summary"]).ToList()
        };
    }
}

public static class MissionPredictiveEngine
{
    public static MissionPlan EnhancePredictiveBaseline(MissionPlan mission)
    {
        var steps = mission.Steps.ToList();
        steps.Insert(0, new RecipeStep
        {
            ActionType = "token",
            ActionArgument = "ax.read_context",
            Channel = "ui",
            WaitMs = 500,
            RetryCount = 1,
            RetryDelayMs = 300,
            Checkpoint = true
        });

        return mission with
        {
            Goal = mission.Goal + " (predictive)",
            PlanText = mission.PlanText + "\n[predictive context snapshot injected]",
            Steps = steps,
            RiskLevel = "low",
            GuardHints = mission.GuardHints.Concat(["Predictive mode: context-first preflight run"]).ToList()
        };
    }
}

public static class MissionDecisionGuard
{
    public sealed record DecisionResult(
        bool Allowed,
        bool RequiresApproval,
        List<string> Reasons);

    public static DecisionResult Evaluate(NexusSettings settings, MissionPlan plan)
    {
        var reasons = new List<string>();

        if (!string.Equals(settings.Safety.Profile, "power-user", StringComparison.OrdinalIgnoreCase))
            reasons.Add("Safety profile is not power-user.");

        if (plan.Steps.Count == 0)
            reasons.Add("Plan has no executable steps.");

        if (!PlanGuard.IsForegroundFamilyAllowed(settings))
            reasons.Add("Foreground app family is blocked by allowedAppFamilies.");

        var risk = (plan.RiskLevel ?? "medium").ToLowerInvariant();
        if ((risk is "high" || plan.RiskLevel == "high") && plan.RequiresManualApproval)
            reasons.Add("High-risk mission requires approval.");

        return new DecisionResult(reasons.Count == 0, reasons.Count > 0, reasons);
    }
}

public static class MissionRecoveryEngine
{
    public static Func<bool>? IsPaused { get; set; }

    public static async Task<MissionExecutionReport> RunAsync(
        IReadOnlyList<RecipeStep> steps,
        NexusSettings settings,
        bool dryRun,
        CancellationToken ct)
    {
        var lines = new List<LessonEntry>();
        var status = "started";
        var success = 0;
        var fail = 0;
        var blocked = 0;
        var sb = new StringBuilder();

        for (var i = 0; i < steps.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            while (IsPaused?.Invoke() == true)
                await Task.Delay(250, ct).ConfigureAwait(false);

            var step = steps[i];
            if (!dryRun && !RecipeStepGuardEvaluator.TryPassGuards(step, out var guardDetail))
            {
                var blockedLine = $"[step {i + 1}] guard failed: {guardDetail}";
                lines.Add(new LessonEntry(i + 1, step.ActionArgument, "[BLOCKED] " + guardDetail, 0, false));
                blocked++;
                sb.AppendLine(blockedLine);
                if (step.GuardStopRunOnMismatch)
                {
                    status = "blocked";
                    break;
                }
                continue;
            }

            if (dryRun)
            {
                var dline = $"[DRY-RUN] {i + 1}/{steps.Count}: {step.ActionArgument}";
                lines.Add(new LessonEntry(i + 1, step.ActionArgument, dline, 0, false));
                sb.AppendLine(dline);
                continue;
            }

            if (!string.Equals(settings.Safety.Profile, "power-user", StringComparison.OrdinalIgnoreCase))
            {
                var dline = $"[SIM] {i + 1}/{steps.Count}: {step.ActionArgument}";
                lines.Add(new LessonEntry(i + 1, step.ActionArgument, dline, 0, false));
                sb.AppendLine(dline);
                continue;
            }

            var runResult = await RunOneWithRecoveryAsync(step, settings, ct).ConfigureAwait(false);
            lines.Add(new LessonEntry(i + 1, step.ActionArgument, runResult.Result, runResult.Attempts, runResult.Recovered));
            sb.AppendLine(runResult.Result);

            if (runResult.Failed)
            {
                fail++;
                if (string.Equals(step.OnFailure, "skip", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(step.OnFailure, "continue", StringComparison.OrdinalIgnoreCase))
                {
                    if (step.WaitMs > 0)
                        await Task.Delay(step.WaitMs, ct).ConfigureAwait(false);
                    continue;
                }

                status = "blocked";
                break;
            }

            success++;
            if (step.WaitMs > 0)
                await Task.Delay(step.WaitMs, ct).ConfigureAwait(false);
        }

        if (status == "started")
            status = success == steps.Count ? "completed" : (steps.Count == 0 ? "noop" : "partial");

        var report = string.Join("\n", lines.Select(l =>
            $"[{l.StepIndex}] {(string.IsNullOrWhiteSpace(l.Result) ? "(empty)" : l.Result)}"));
        var transcript = sb.ToString().Trim();
        ActionHistoryService.AppendPlanRun(
            steps,
            dryRun,
            (report.Length > 4000 ? report[..4000] + "…" : report));

        return new MissionExecutionReport(
            status,
            lines,
            success,
            fail,
            blocked,
            transcript,
            status == "completed");
    }

    private sealed record StepExecutionResult(string Result, int Attempts, bool Recovered, bool Failed);

    private static async Task<StepExecutionResult> RunOneWithRecoveryAsync(RecipeStep step, NexusSettings settings, CancellationToken ct)
    {
        var routes = ResolveRecoveryRoutes(step.Channel);
        var failures = 0;
        var lastResult = string.Empty;
        for (var attempt = 0; attempt < routes.Count; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            var route = routes[attempt];
            var copy = CopyStep(step, route);
            lastResult = await InvokeStepAsync(copy, settings, ct).ConfigureAwait(false);
            var failed = IsHardFailure(lastResult);
            if (!failed)
                return new StepExecutionResult(lastResult, attempt + 1, attempt > 0, false);

            failures++;
            if (attempt + 1 < routes.Count)
                await Task.Delay(150, ct).ConfigureAwait(false);
        }

        return new StepExecutionResult(lastResult, failures, failures > 1, true);
    }

    private static async Task<string> InvokeStepAsync(RecipeStep step, NexusSettings settings, CancellationToken ct)
    {
        if (NexusContext.RunWin32StepOnUiThreadAsync != null)
        {
            return await NexusContext.RunWin32StepOnUiThreadAsync(() =>
                AutomationToolRouter.Execute(step, settings, step.Channel)).ConfigureAwait(false);
        }

        return await Task.FromResult(AutomationToolRouter.Execute(step, settings, step.Channel));
    }

    private static RecipeStep CopyStep(RecipeStep step, string channel)
    {
        return new RecipeStep
        {
            ActionType = step.ActionType,
            ActionArgument = step.ActionArgument,
            WaitMs = step.WaitMs,
            RetryCount = step.RetryCount,
            RetryDelayMs = step.RetryDelayMs,
            GuardProcessContains = step.GuardProcessContains,
            GuardWindowTitleContains = step.GuardWindowTitleContains,
            GuardStopRunOnMismatch = step.GuardStopRunOnMismatch,
            OnFailure = step.OnFailure,
            Checkpoint = step.Checkpoint,
            Channel = channel,
            JumpToStepIndexOnSuccess = step.JumpToStepIndexOnSuccess,
            JumpToStepIndexOnFailure = step.JumpToStepIndexOnFailure,
            FallbackCvTemplatePath = step.FallbackCvTemplatePath
        };
    }

    private static IReadOnlyList<string> ResolveRecoveryRoutes(string current)
    {
        return current?.ToLowerInvariant() switch
        {
            "script" => ["script", "api", "ui"],
            "api" => ["api", "script", "ui"],
            "uia" => ["ui", "script", "api"],
            "browser" => ["browser", "ui", "api", "script"],
            _ => ["ui", "script", "api"]
        };
    }

    private static bool IsHardFailure(string msg) =>
        msg.StartsWith("[ERR]", StringComparison.Ordinal) ||
        msg.StartsWith("[BLOCKED]", StringComparison.Ordinal) ||
        msg.StartsWith("[SANDBOX]", StringComparison.Ordinal);
}

public static class MissionNarrator
{
    public static string Summarize(MissionPlan plan, MissionExecutionReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Mission execution summary");
        sb.AppendLine($"goal: {plan.Goal}");
        sb.AppendLine($"risk: {plan.RiskLevel}");
        sb.AppendLine($"confidence: {plan.Confidence:0.00}");
        sb.AppendLine($"status: {report.Status}");
        sb.AppendLine($"steps: {report.StepLog.Count}");
        sb.AppendLine($"success: {report.SuccessCount}, failed: {report.FailureCount}, blocked: {report.BlockedCount}");
        sb.AppendLine($"decision: {(plan.RequiresManualApproval ? "requires approval" : "auto-approved")}");
        if (!string.IsNullOrWhiteSpace(plan.DecisionReason))
            sb.AppendLine($"reason: {plan.DecisionReason}");
        if (report.StepLog.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("top events:");
            foreach (var l in report.StepLog.TakeLast(Math.Min(4, report.StepLog.Count)))
                sb.AppendLine($"- {l.StepIndex}: {l.Result}");
        }

        return sb.ToString().TrimEnd();
    }
}

public sealed record MissionComplianceReport(
    string FilePath,
    string Risk,
    string Decision,
    bool BlockedByPolicy,
    string AuditSummary,
    DateTime CreatedUtc);

public static class MissionComplianceService
{
    public static MissionComplianceReport Evaluate(MissionPlan mission, NexusSettings settings, MissionExecutionReport report)
    {
        var blocked = string.Equals(mission.RiskLevel, "high", StringComparison.OrdinalIgnoreCase)
                      || report.BlockedCount > 0
                      || !string.IsNullOrWhiteSpace(report.Status) && report.Status.Equals("blocked", StringComparison.OrdinalIgnoreCase);
        var decision = blocked ? "requires human review" : "auto-approved within allowed policy";
        var summary =
            $"goal={mission.Goal}|risk={mission.RiskLevel}|steps={mission.Steps.Count}|success={report.SuccessCount}|blocked={report.BlockedCount}|failed={report.FailureCount}|profile={settings.Safety.Profile}|autoRun={mission.UsedAi}";

        return new MissionComplianceReport(
            Persist(mission.FilePath, mission, settings, report, summary),
            mission.RiskLevel,
            decision,
            blocked,
            summary,
            DateTime.UtcNow
        );
    }

    private static string Persist(string missionFilePath, MissionPlan mission, NexusSettings settings, MissionExecutionReport report, string summary)
    {
        var reportsDir = Path.Combine(AppPaths.RepoRoot, "docs");
        Directory.CreateDirectory(reportsDir);
        var file = Path.Combine(reportsDir, $"mission-compliance-{DateTime.UtcNow:yyyyMMdd-HHmmss}.md");
        var payload = new StringBuilder();
        payload.AppendLine("# Mission compliance report");
        payload.AppendLine($"Mission: {missionFilePath}");
        payload.AppendLine($"Time UTC: {DateTime.UtcNow:O}");
        payload.AppendLine($"Profile: {settings.Safety.Profile}");
        payload.AppendLine($"Decision: {(string.Equals(mission.RiskLevel, "high", StringComparison.OrdinalIgnoreCase) ? "high risk" : "low/medium risk")}");
        payload.AppendLine("Summary:");
        payload.AppendLine(summary);
        payload.AppendLine("Transcript:");
        payload.AppendLine(report.Transcript);
        File.WriteAllText(file, payload.ToString(), Encoding.UTF8);
        return file;
    }
}
