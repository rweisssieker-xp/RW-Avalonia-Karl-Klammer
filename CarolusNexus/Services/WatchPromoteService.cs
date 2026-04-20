using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CarolusNexus.Models;
using CarolusNexus;

namespace CarolusNexus.Services;

/// <summary>Watch-Einträge in Ritual-Schritte verwandeln (LLM + Fallback).</summary>
public static class WatchPromoteService
{
    private const string LlmSystem =
        "You convert a chronological desktop watch log into automation recipe steps. " +
        "Reply with ONLY valid JSON: an array of objects with keys \"actionType\" (string), \"actionArgument\" (string), " +
        "\"waitMs\" (number, optional, default 0). Use actionType \"token\" and short actionArgument text " +
        "describing context or implied user actions. No markdown, no prose, at most 28 steps.";

    public static List<RecipeStep> BuildFallbackSteps(IReadOnlyList<WatchSessionService.WatchEntry> entries) =>
        entries.Select(e => new RecipeStep
        {
            ActionType = "token",
            ActionArgument = FormatWatchToken(e),
            WaitMs = 0
        }).ToList();

    public static async Task<List<RecipeStep>> BuildPromotedStepsAsync(
        NexusSettings settings,
        WatchSessionService.WatchDocument doc,
        CancellationToken ct = default)
    {
        if (doc.Entries.Count == 0)
            return new List<RecipeStep>();

        if (DotEnvStore.HasProviderKey(settings.Provider))
        {
            try
            {
                var n = Math.Min(40, doc.Entries.Count);
                var recent = doc.Entries.Skip(doc.Entries.Count - n).ToList();
                var blob = string.Join("\n", recent.Select(FormatLineForLlm));
                var shell = NexusShell.FormatRecentLogForPrompt(5, 80);
                if (!string.IsNullOrEmpty(shell))
                    blob = "Shell log (hints):\n" + shell + "\n---\n" + blob;

                var text = await LlmChatService
                    .CompleteUtilityAsync(settings, LlmSystem, blob, ct)
                    .ConfigureAwait(false);

                if (PlanJsonParser.TryParseRecipeStepsFromText(text, out var parsed) && parsed.Count > 0)
                    return parsed;
            }
            catch
            {
                // Fallback unten
            }
        }

        return BuildFallbackSteps(doc.Entries);
    }

    private static string FormatLineForLlm(WatchSessionService.WatchEntry e)
    {
        var local = e.UtcAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
        var title = e.WindowTitle ?? "";
        if (title.Length > 100)
            title = title[..100] + "…";
        return $"{local} | proc={e.ProcessName ?? "?"} | title={title} | adapter={e.AdapterFamily ?? "?"} | " +
               $"hash={e.ScreenHash ?? "-"} | note={e.Note ?? ""}";
    }

    private static string FormatWatchToken(WatchSessionService.WatchEntry e)
    {
        var parts = new List<string> { "watch" };
        if (!string.IsNullOrWhiteSpace(e.ProcessName))
            parts.Add(e.ProcessName.Trim());
        if (!string.IsNullOrWhiteSpace(e.WindowTitle))
        {
            var t = e.WindowTitle.Trim();
            if (t.Length > 96)
                t = t[..96] + "…";
            parts.Add(t);
        }

        if (!string.IsNullOrWhiteSpace(e.AdapterFamily))
            parts.Add($"fam:{e.AdapterFamily.Trim()}");
        if (!string.IsNullOrWhiteSpace(e.ScreenHash))
            parts.Add($"screen:{e.ScreenHash.Trim()}");
        if (!string.IsNullOrWhiteSpace(e.Note))
            parts.Add(e.Note.Trim());

        return string.Join("|", parts);
    }
}
