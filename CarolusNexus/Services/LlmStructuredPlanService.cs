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
            "From the following assistant reply, extract executable automation steps. " +
            "Reply only with a JSON object exactly in this form, no markdown: " +
            "{\"steps\":[{\"actionType\":\"token\",\"actionArgument\":\"…\",\"waitMs\":0}]} . " +
            "actionArgument: exactly one token or line from the reply (e.g. [ACTION:…], browser.*, ax.*). " +
            "If nothing fits: {\"steps\":[]}.";

        var clip = assistantMarkdown.Length > 12_000 ? assistantMarkdown[..12_000] + "…" : assistantMarkdown;
        var json = await LlmChatService.CompleteUtilityAsync(settings, sys, clip, ct).ConfigureAwait(false);
        if (LooksLikeError(json) || !PlanJsonParser.TryParseRecipeStepsFromText(json, out var steps))
            return null;
        return steps.Count > 0 ? steps : null;
    }

    private static bool LooksLikeError(string text)
    {
        var t = text.TrimStart();
        return t.StartsWith("Missing ", StringComparison.Ordinal)
               || t.StartsWith("Unknown provider", StringComparison.Ordinal)
               || t.StartsWith("Anthropic HTTP", StringComparison.Ordinal)
               || t.StartsWith("OpenAI", StringComparison.Ordinal);
    }
}
