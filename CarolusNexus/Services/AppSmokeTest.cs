using System;
using System.IO;
using System.Threading;
using CarolusNexus.Services;

namespace CarolusNexus;

/// <summary>Headless-Checks ohne Avalonia-UI (z. B. <c>dotnet run -- --smoke</c>).</summary>
public static class AppSmokeTest
{
    public static int Run()
    {
        try
        {
            try
            {
                Console.OutputEncoding = System.Text.Encoding.UTF8;
                Console.InputEncoding = System.Text.Encoding.UTF8;
            }
            catch
            {
                /* ältere Konsolen */
            }

            LogOk("smoke · start");
            AppPaths.DiscoverRepoRoot();
            AppPaths.EnsureDataTree();
            LogOk($"smoke · RepoRoot={AppPaths.RepoRoot}");
            LogOk($"smoke · DataDir={AppPaths.DataDir}");

            var settings = new SettingsStore().LoadOrDefault();
            LogOk($"smoke · settings provider={settings.Provider} mode={settings.Mode}");

            DotEnvStore.Invalidate();
            var env = DotEnvStore.Load();
            LogOk($"smoke · .env keys={env.Count}");

            _ = KnowledgeSnippetService.BuildContext(null, 400);
            LogOk("smoke · KnowledgeSnippetService.BuildContext");

            _ = ActionHistoryService.Load();
            LogOk("smoke · ActionHistoryService.Load");

            _ = RitualRecipeStore.LoadAll();
            LogOk("smoke · RitualRecipeStore.LoadAll");

            _ = WatchSessionService.LoadOrEmpty();
            LogOk("smoke · WatchSessionService.LoadOrEmpty");

            var embReady = EmbeddingRagService.IsEmbeddingIndexReady();
            LogOk($"smoke · EmbeddingRAG index ready={embReady}");

            var preview = KnowledgeIndexService.ReadDocumentForPreview(Path.Combine(AppPaths.KnowledgeDir, "__nonexistent__"));
            if (!preview.StartsWith("(Datei fehlt", StringComparison.Ordinal))
                throw new InvalidOperationException("ReadDocumentForPreview sollte fehlende Datei melden.");

            LogOk("smoke · KnowledgeIndexService.ReadDocumentForPreview (edge)");

            if (DotEnvStore.HasProviderKey(settings.Provider))
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));
                var text = LlmChatService.SmokeAsync(settings, cts.Token).GetAwaiter().GetResult();
                if (string.IsNullOrWhiteSpace(text))
                    throw new InvalidOperationException("LLM smoke: leere Antwort.");
                if (text.StartsWith("Fehlt ", StringComparison.Ordinal) ||
                    text.StartsWith("Unbekannter Provider", StringComparison.Ordinal))
                    throw new InvalidOperationException("LLM smoke: " + text);

                LogOk($"smoke · LLM provider OK ({text.Trim().Length} Zeichen)");
            }
            else
            {
                LogOk("smoke · LLM übersprungen (kein Provider-Key in .env)");
            }

            Console.WriteLine();
            Console.WriteLine("SMOKE OK — alle Checks bestanden.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine();
            Console.Error.WriteLine("SMOKE FAIL: " + ex.Message);
            Console.Error.WriteLine(ex);
            return 1;
        }
    }

    private static void LogOk(string msg) => Console.WriteLine("[ok] " + msg);
}
