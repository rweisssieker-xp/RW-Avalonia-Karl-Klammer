using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CarolusNexus.Models;
using System.Collections.Generic;

namespace CarolusNexus.Services;

public sealed record RadicalPlanResult(
    string PlanText,
    string FilePath,
    bool UsedAi,
    List<RecipeStep> Steps);

public static class RadicalPlanService
{
    public static async Task<RadicalPlanResult> BuildPlanAsync(
        NexusSettings settings,
        string? goal,
        CancellationToken ct = default)
    {
        var requestedGoal = string.IsNullOrWhiteSpace(goal) ? "General radical AI transformation" : goal.Trim();
        var system = """
            You are a Radical AI Product Innovator for this app.
            Output must be in German, concise, and directly build-ready.
            Include: breakdown, 5 radical ideas, best idea, complete feature blueprint, execution flow, and disruption explanation.
            Be extreme; no conservative improvements.
            Return plain markdown text only.
            """;
        var user = $"""
            Ziel: {requestedGoal}

            Erzeuge jetzt sofort einen umsetzbaren Radical-Produktplan für diese App:
            - Breakdown der aktuellen App
            - 5 radikale Ideen
            - beste Idee
            - vollständiges Feature (Vibe Build ready)
            - Disruption-Erklärung
            """;

        string plan;
        bool usedAi = false;
        if (DotEnvStore.HasProviderKey(settings.Provider))
        {
            var prompt = BuildPlanPrompt(user);
            plan = await LlmChatService.CompleteUtilityAsync(settings, system, prompt, ct).ConfigureAwait(false);
            if (LooksLikeLlmError(plan))
                plan = FallbackPlan(settings, requestedGoal);
            else
                usedAi = true;
        }
        else
        {
            plan = FallbackPlan(settings, requestedGoal);
        }

        var docsDir = AppPaths.RadicalPlansDir;
        Directory.CreateDirectory(docsDir);
        var fileName = $"radical-plan-{DateTime.UtcNow:yyyyMMdd-HHmmss}.md";
        var path = Path.Combine(docsDir, fileName);
        await File.WriteAllTextAsync(path, plan, Encoding.UTF8, ct).ConfigureAwait(false);

        var steps = await TryExtractStepsAsync(settings, plan, ct).ConfigureAwait(false);
        if (steps.Count == 0 && usedAi)
            steps = await BuildFallbackStepsAsync().ConfigureAwait(false);

        return new RadicalPlanResult(plan, path, usedAi, steps);
    }

    private static string BuildPlanPrompt(string user)
    {
        return user + """

        OUTPUT FORMAT:
        1) Full markdown report as requested.
        2) At the end add:
        ```json
        {"steps":[
          {"actionType":"token","actionArgument":"[START] Open target doc","waitMs":0}
        ]}
        ```
        The JSON is optional for markdown sections but required for automation.
        """;
    }

    public static async Task<List<RecipeStep>> TryExtractStepsAsync(
        NexusSettings settings,
        string planText,
        CancellationToken ct)
    {
        var parsed = await LlmStructuredPlanService.TryExtractStepsJsonAsync(settings, planText, ct).ConfigureAwait(false);
        if (parsed != null && parsed.Count > 0)
            return parsed;

        if (PlanJsonParser.TryParseRecipeStepsFromText(planText, out var fileSteps) && fileSteps.Count > 0)
            return fileSteps;

        return new List<RecipeStep>();
    }

    private static async Task<List<RecipeStep>> BuildFallbackStepsAsync()
    {
        await Task.Yield();
        return new List<RecipeStep>
        {
            new()
            {
                ActionType = "token",
                ActionArgument = "[PLAN] Review generated radical plan in docs and decide execution scope.",
                WaitMs = 0
            },
            new()
            {
                ActionType = "token",
                ActionArgument = "[PLAN] Copy the next concrete step as a CLI or UI task and run in a safe environment.",
                WaitMs = 0
            }
        };
    }

    private static bool LooksLikeLlmError(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return true;
        return text.Contains("Missing ", StringComparison.OrdinalIgnoreCase)
               || text.Contains("OPENAI", StringComparison.OrdinalIgnoreCase)
               || text.Contains("ANTHROPIC", StringComparison.OrdinalIgnoreCase)
               || text.StartsWith("Error", StringComparison.OrdinalIgnoreCase)
               || text.StartsWith("Unknown", StringComparison.OrdinalIgnoreCase);
    }

    private static string FallbackPlan(NexusSettings settings, string goal)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Radical AI MVP Plan");
        sb.AppendLine();
        sb.AppendLine($"Ziel: {goal}");
        sb.AppendLine("Modus: Offline-Fallback (keine API-Key-Konfiguration)");
        sb.AppendLine();
        sb.AppendLine("## 1) Breakdown");
        sb.AppendLine("- Aktuelles Nutzerbild ist Schritt-für-Schritt mit manueller Plan-Ausführung.");
        sb.AppendLine("- Standardfluss: Prompt -> Plan -> Run -> Check -> Korrektur.");
        sb.AppendLine("- Die meisten Interaktionsstufen sind automatisierbar.");
        sb.AppendLine();
        sb.AppendLine("## 2) 5 radikale Ideen");
        sb.AppendLine("1. Radical Agentic Auto-Captain");
        sb.AppendLine("2. Zero-UI Intake");
        sb.AppendLine("3. Silent Batch Reactor");
        sb.AppendLine("4. Reality Diff Engine");
        sb.AppendLine("5. Promptless Operator");
        sb.AppendLine();
        sb.AppendLine("## 3) Beste Idee");
        sb.AppendLine("Radical Agentic Auto-Captain – ein Ziel-Statement startet vollautomatisch die komplette Pipeline.");
        sb.AppendLine();
        sb.AppendLine("## 4) Build-ready MVP");
        sb.AppendLine("- Neuer Dienst: `RadicalPlanService` (Prompt-Erkennung + Plan-Ausgabe)");
        sb.AppendLine("- Neuer Ask-Route: `radical` im Ask-Input");
        sb.AppendLine("- Standardausgabe: Markdown-Plan unter `docs/` + Anzeige im Assistant-Feld");
        sb.AppendLine("- Optional: Auto-Ausführung in einem Folge-Step via Agent-Hook");
        sb.AppendLine();
        sb.AppendLine("## 5) Disruption");
        sb.AppendLine("- 10x besser: manuelle Entscheidungsschritte werden auf 1 Befehl reduziert.");
        sb.AppendLine("- Ersetzt: klassische Ask-Plan- und Freigabepfade.");
        sb.AppendLine("- Schwer zu kopieren: Kombination aus Trigger, automatischem Plan-Loop und Persistenz-Pipeline.");
        sb.AppendLine();
        sb.AppendLine($"Provider in Settings: {settings.Provider}");
        return sb.ToString();
    }
}
