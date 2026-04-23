using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CarolusNexus.Models;

namespace CarolusNexus.Services;

public static class SimplePlanSimulator
{
    public static async Task<string> RunAsync(
        IReadOnlyList<RecipeStep> steps,
        bool dryRun,
        NexusSettings? safety,
        AutomationRecipe? recipe = null,
        CancellationToken ct = default)
    {
        var sb = new StringBuilder();
        var prefix = dryRun ? "[DRY-RUN]" : "[RUN]";
        var executeReal = !dryRun
                          && safety != null
                          && string.Equals(safety.Safety.Profile, "power-user",
                              StringComparison.OrdinalIgnoreCase)
                          && OperatingSystem.IsWindows();

        var runId = Guid.NewGuid().ToString("n");
        AgentRunStateStore.BeginRun(runId, recipe?.Name ?? "(plan)", steps.Count);
        FlowResumeStore.Begin(recipe, steps.Count);
        var maxAutonomy = recipe?.MaxAutonomySteps ?? 0;
        var autonomyStreak = 0;
        var completed = steps.Count == 0;
        var finalResult = "";

        try
        {
            var i = 0;
            while (i < steps.Count)
            {
                ct.ThrowIfCancellationRequested();
                var s = steps[i];
                AgentRunStateStore.SetProgress(i);

                if (maxAutonomy > 0 && !s.Checkpoint && autonomyStreak >= maxAutonomy)
                {
                    var line =
                        $"{prefix} step {i + 1}/{steps.Count}: [BLOCKED] max autonomy ({maxAutonomy}) — add checkpoint or raise limit.";
                    sb.AppendLine(line);
                    NexusShell.Log(line);
                    RitualStepAudit.Append(i + 1, steps.Count, s, dryRun, "[BLOCKED] max autonomy");
                    break;
                }

                var lineStart =
                    $"{prefix} step {i + 1}/{steps.Count}: {s.ActionType} · {s.ActionArgument} (wait {s.WaitMs}ms)";
                sb.AppendLine(lineStart);
                NexusShell.Log(lineStart);
                var readiness = AutomationTokenReadiness.Classify(s.ActionArgument, safety ?? new NexusSettings(), s.Channel);
                var readinessLine =
                    $"  → readiness: {readiness.Mode}/{readiness.Capability} · executable={readiness.IsExecutable} · {readiness.Reason}";
                sb.AppendLine(readinessLine);
                NexusShell.Log(readinessLine);

                if (!dryRun && safety != null && !PlanGuard.IsForegroundFamilyAllowed(safety))
                {
                    var blocked = "  → [BLOCKED] foreground app family not in allowedAppFamilies";
                    sb.AppendLine(blocked);
                    NexusShell.Log(blocked);
                    RitualStepAudit.Append(i + 1, steps.Count, s, dryRun, "[BLOCKED] app family");
                    break;
                }

                if (!RecipeStepGuardEvaluator.TryPassGuards(s, out var guardDetail))
                {
                    if (s.GuardStopRunOnMismatch)
                    {
                        var gmsg = "  → [BLOCKED] guard: " + guardDetail;
                        sb.AppendLine(gmsg);
                        NexusShell.Log(gmsg);
                        RitualStepAudit.Append(i + 1, steps.Count, s, dryRun, "[BLOCKED] guard: " + guardDetail);
                        if (HandleFailureBranching(s, sb, ref i, steps.Count, failure: true))
                            continue;
                        break;
                    }

                    var skip = "  → [SKIP] guard: " + guardDetail;
                    sb.AppendLine(skip);
                    NexusShell.Log(skip);
                    RitualStepAudit.Append(i + 1, steps.Count, s, dryRun, "[SKIP] guard");
                    i = NextIndexAfterSuccess(i, s, steps.Count);
                    continue;
                }

                string stepResult;
                if (dryRun)
                {
                    stepResult = "[DRY-RUN]";
                    sb.AppendLine("  → " + stepResult);
                }
                else if (executeReal && safety != null && ExecutionReliabilityGate.IsOpen(s, out var blockReason, out var blockFor))
                {
                    var wait = blockFor > TimeSpan.Zero
                        ? $" ({Math.Max(0, (int)Math.Ceiling(blockFor.TotalSeconds))}s remaining)"
                        : "";
                    stepResult = "[BLOCKED] reliability-gate open: " + blockReason + wait;
                    sb.AppendLine("  → " + stepResult);
                    NexusShell.Log("  → " + stepResult);
                }
                else if (executeReal && safety != null)
                {
                    if (!PlanGuard.IsAllowed(safety, s.ActionArgument))
                    {
                        sb.AppendLine("  → [BLOCKED] Safety-Policy");
                        NexusShell.Log("  → [BLOCKED] Safety-Policy");
                        stepResult = "[BLOCKED] Safety-Policy";
                    }
                    else
                    {
                        var retries = Math.Max(0, s.RetryCount);
                        var delay = Math.Max(0, s.RetryDelayMs);
                        stepResult = "[SIM]";
                        for (var attempt = 0; attempt <= retries; attempt++)
                        {
                            ct.ThrowIfCancellationRequested();
                            string msg;
                            if (NexusContext.RunWin32StepOnUiThreadAsync != null)
                            {
                                msg = await NexusContext.RunWin32StepOnUiThreadAsync(() =>
                                        AutomationToolRouter.Execute(s, safety))
                                    .ConfigureAwait(false);
                            }
                            else
                                msg = AutomationToolRouter.Execute(s, safety);

                            stepResult = msg;
                            if (!IsHardFailure(msg) || attempt >= retries)
                                break;
                            if (delay > 0)
                                await Task.Delay(delay, ct).ConfigureAwait(false);
                        }

                        sb.AppendLine("  → " + stepResult);
                        NexusShell.Log("  → " + stepResult);
                    }
                }
                else
                {
                    stepResult = !OperatingSystem.IsWindows()
                        ? "[SIM] not Windows"
                        : !string.Equals(safety?.Safety.Profile, "power-user", StringComparison.OrdinalIgnoreCase)
                            ? "[SIM] profile ≠ power-user — no Win32"
                            : "[SIM]";
                    sb.AppendLine("  → " + stepResult);
                }

                RitualStepAudit.Append(i + 1, steps.Count, s, dryRun, stepResult);
                ExecutionEvidenceService.Append(runId, i + 1, steps.Count, s, dryRun, readiness, stepResult);
                AdaptiveOperatorMemoryService.Record(
                    OperatingSystem.IsWindows() ? (ForegroundWindowInfo.TryReadDetail() is { } d ? OperatorAdapterRegistry.ResolveFamily(d.ProcessName, d.Title) : "generic") : "generic",
                    s.ActionArgument ?? "",
                    stepResult);
                FlowResumeStore.RecordStep(recipe, i, steps.Count, stepResult);
                finalResult = stepResult;

                if (s.WaitMs > 0)
                    await Task.Delay(s.WaitMs, ct).ConfigureAwait(false);

                var failed = IsHardFailure(stepResult);
                if (failed)
                    AgentRunStateStore.SetProgress(i, stepResult);

                if (!dryRun && executeReal && safety != null)
                {
                    ExecutionReliabilityGate.RecordResult(s, stepResult);
                    if (s.Checkpoint)
                        autonomyStreak = 0;
                    else if (!failed && !stepResult.StartsWith("[SKIP]", StringComparison.Ordinal))
                        autonomyStreak++;
                }

                if (failed)
                {
                    var recovery = RecoverySuggestionService.BuildSuggestion(s, stepResult, safety);
                    sb.AppendLine(recovery);
                    NexusShell.Log(recovery.Replace(Environment.NewLine, " | "));
                    if (HandleFailureBranching(s, sb, ref i, steps.Count, failure: true))
                        continue;
                    break;
                }

                i = NextIndexAfterSuccess(i, s, steps.Count);
                completed = i >= steps.Count;
            }
        }
        finally
        {
            FlowResumeStore.Finish(recipe, completed, finalResult);
            AgentRunStateStore.EndRun();
        }

        if (steps.Count == 0)
            sb.AppendLine("(no steps)");
        if (!dryRun && safety != null &&
            !string.Equals(safety.Safety.Profile, "power-user", StringComparison.OrdinalIgnoreCase))
            sb.AppendLine("(Note: real execution only with safety profile “power-user”.)");

        var logText = sb.ToString().TrimEnd();
        ActionHistoryService.AppendPlanRun(steps, dryRun, logText.Length > 4000 ? logText[..4000] + "…" : logText);

        return logText;
    }

    private static bool IsHardFailure(string msg) =>
        msg.StartsWith("[ERR]", StringComparison.Ordinal) ||
        msg.StartsWith("[BLOCKED]", StringComparison.Ordinal);

    private static bool HandleFailureBranching(RecipeStep s, StringBuilder sb, ref int i, int totalSteps, bool failure)
    {
        if (!failure)
            return false;
        var mode = (s.OnFailure ?? "stop").Trim().ToLowerInvariant();
        if (mode is "skip" or "continue")
        {
            i++;
            return true;
        }

        if (s.JumpToStepIndexOnFailure is { } jf && jf >= 0 && jf < totalSteps)
        {
            NexusShell.Log($"  → branch on failure → step {jf + 1}");
            i = jf;
            return true;
        }

        return false;
    }

    private static int NextIndexAfterSuccess(int i, RecipeStep s, int total)
    {
        if (s.JumpToStepIndexOnSuccess is { } js && js >= 0 && js < total)
        {
            NexusShell.Log($"  → branch on success → step {js + 1}");
            return js;
        }

        return i + 1;
    }

    public static List<RecipeStep> ParsePlanPreviewLines(string planText)
    {
        var steps = new List<RecipeStep>();
        if (string.IsNullOrWhiteSpace(planText))
            return steps;

        foreach (var line in planText.Split('\n'))
        {
            var t = line.Trim();
            if (t.Length == 0)
                continue;
            var dot = t.IndexOf(". ");
            if (dot > 0 && int.TryParse(t[..dot], out _))
                t = t[(dot + 2)..].Trim();
            if (t.StartsWith('(') && (t.Contains("keine Action", StringComparison.Ordinal) || t.Contains("no action", StringComparison.OrdinalIgnoreCase)))
                continue;
            steps.Add(new RecipeStep { ActionType = "token", ActionArgument = t, WaitMs = 0 });
        }

        return steps;
    }
}
