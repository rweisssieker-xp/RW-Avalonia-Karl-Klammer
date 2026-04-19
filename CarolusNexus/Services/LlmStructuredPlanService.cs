using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CarolusNexus.Models;

namespace CarolusNexus.Services;

public static class LlmStructuredPlanService
{
    /// <summary>Ein Utility-LLM-Aufruf: liefert nur JSON mit <c>steps</c>-Array.</summary>
    public static async Task<List<RecipeStep>?> TryExtractStepsJsonAsync(
        NexusSettings settings,
        string assistantMarkdown,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(assistantMarkdown))
            return null;

        const string sys =
            "Du extrahierst aus der folgenden Assistenten-Antwort ausführbare Automations-Schritte. " +
            "Antworte ausschließlich mit einem JSON-Objekt genau in dieser Form, ohne Markdown: " +
            "{\"steps\":[{\"actionType\":\"token\",\"actionArgument\":\"…\",\"waitMs\":0}]} . " +
            "actionArgument: exakt ein Token oder eine Zeile aus der Antwort (z. B. [ACTION:…], browser.*, ax.*). " +
            "Wenn nichts Passendes: {\"steps\":[]}.";

        var clip = assistantMarkdown.Length > 12_000 ? assistantMarkdown[..12_000] + "…" : assistantMarkdown;
        var json = await LlmChatService.CompleteUtilityAsync(settings, sys, clip, ct).ConfigureAwait(false);
        if (LooksLikeError(json) || !PlanJsonParser.TryParseRecipeStepsFromText(json, out var steps))
            return null;
        return steps.Count > 0 ? steps : null;
    }

    private static bool LooksLikeError(string text)
    {
        var t = text.TrimStart();
        return t.StartsWith("Fehlt ", StringComparison.Ordinal)
               || t.StartsWith("Unbekannter Provider", StringComparison.Ordinal)
               || t.StartsWith("Anthropic HTTP", StringComparison.Ordinal)
               || t.StartsWith("OpenAI", StringComparison.Ordinal);
    }
}
