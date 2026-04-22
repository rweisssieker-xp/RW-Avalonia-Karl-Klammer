using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CarolusNexus;
using CarolusNexus.Models;
using CarolusNexus.Services;

namespace CarolusNexus_WinUI;

/// <summary>Dashboard card text — aligned with <c>MainWindow.RefreshDashboard</c> / Avalonia.</summary>
public static class WinUiDashboardData
{
    private static DateTime _lastWatchUtc = DateTime.MinValue;
    private static string? _lastWatchThumbHash;
    private static string _proactiveHintCache = "";
    private static DateTime _lastProactiveUtc = DateTime.MinValue;
    private static bool _proactiveBusy;

    public static void FillCards(
        Action<string> setEnv,
        Action<string> setKnow,
        Action<string> setLive,
        Action<string> setProactive,
        Action<string> setGov,
        Action<string> setRituals,
        Action<string> setWatch,
        Action<string> setUsp,
        string tileLiveText,
        NexusSettings settings)
    {
        MaybeAppendWatchSnapshot(settings);

        if (!string.Equals(settings.Mode, "watch", StringComparison.OrdinalIgnoreCase) ||
            !settings.ProactiveDashboardLlm)
            _proactiveHintCache = "";

        if (settings.ProactiveDashboardLlm
            && string.Equals(settings.Mode, "watch", StringComparison.OrdinalIgnoreCase)
            && DotEnvStore.HasProviderKey(settings.Provider))
            _ = TryProactiveHintAsync(settings);

        var knowCount = Directory.Exists(AppPaths.KnowledgeDir)
            ? Directory.GetFiles(AppPaths.KnowledgeDir).Length
            : 0;

        string proactive;
        if (string.Equals(settings.Mode, "watch", StringComparison.OrdinalIgnoreCase))
        {
            if (settings.ProactiveDashboardLlm && DotEnvStore.HasProviderKey(settings.Provider))
            {
                proactive = string.IsNullOrWhiteSpace(_proactiveHintCache)
                    ? $"Watch: snapshots every {Math.Clamp(settings.WatchSnapshotIntervalSeconds, 15, 600)}s. Proactive LLM hint loading …"
                    : _proactiveHintCache;
            }
            else
            {
                proactive =
                    $"Watch: snapshots every {Math.Clamp(settings.WatchSnapshotIntervalSeconds, 15, 600)}s → {Path.GetFileName(AppPaths.WatchSessions)}";
            }
        }
        else
        {
            var keyOk = DotEnvStore.HasProviderKey(settings.Provider);
            var pending = RitualJobQueueStore.GetPendingCount();
            var ritualCount = 0;
            try
            {
                ritualCount = RitualRecipeStore.LoadAll().Count;
            }
            catch
            {
                // ignore
            }

            proactive =
                "Not in watch mode — live snapshot.\n" +
                $"Knowledge files: {knowCount} · Operator flows: {ritualCount} · Jobs pending: {pending}\n" +
                $"LLM .env: {(keyOk ? "key OK" : "key missing")} ({settings.Provider})";
        }

        var logExcerpt = NexusShell.FormatRecentLogForDashboard();
        if (!string.IsNullOrEmpty(logExcerpt))
            proactive += "\n\n— App log (recent) —\n" + logExcerpt;

        setEnv(
            $"Provider: {settings.Provider}\nMode: {settings.Mode}\nModel: {settings.Model}\n.env: {(DotEnvSummary.FileExists ? "yes" : "missing")}");
        setKnow(
            $"Files in knowledge\\: {knowCount}\nIndex: {(File.Exists(AppPaths.KnowledgeIndex) ? "yes" : "no")}\nChunks: {(File.Exists(AppPaths.KnowledgeChunks) ? "yes" : "no")}\nEmbeddings: {(File.Exists(AppPaths.KnowledgeEmbeddings) ? "yes" : "no")}");
        setLive(string.IsNullOrWhiteSpace(tileLiveText) ? "—" : tileLiveText);
        setProactive(proactive);
        setGov(
            $"Profile: {settings.Safety.Profile}\nPanic: {settings.Safety.PanicStopEnabled}\nneverAutoSend: {settings.Safety.NeverAutoSend}\n\n— Flow jobs —\n{RitualJobQueueStore.FormatDashboardSummary()}\n\n— Resume —\n{FlowResumeStore.FormatSummary()}");
        setRituals(FormatRitualsDashboardCard());
        setWatch(WatchSessionService.FormatDashboardSummary());
        setUsp(FormatAiGuiUspRadar(settings, knowCount));
    }

    private static void MaybeAppendWatchSnapshot(NexusSettings settings)
    {
        if (!OperatingSystem.IsWindows())
            return;
        if (!string.Equals(settings.Mode, "watch", StringComparison.OrdinalIgnoreCase))
            return;

        var now = DateTime.UtcNow;
        var interval = Math.Clamp(settings.WatchSnapshotIntervalSeconds, 15, 600);
        if ((now - _lastWatchUtc).TotalSeconds < interval)
            return;
        _lastWatchUtc = now;

        try
        {
            var hash = ScreenCaptureWin.PrimaryMonitorSha256Prefix16();
            var (title, proc) = ForegroundWindowInfo.TryRead();
            var fam = OperatorAdapterRegistry.ResolveFamily(proc, title);
            string? thumbRel = null;
            if (!string.IsNullOrEmpty(hash) && !string.Equals(hash, _lastWatchThumbHash, StringComparison.Ordinal))
            {
                try
                {
                    Directory.CreateDirectory(AppPaths.WatchThumbnailsDir);
                    var fn = $"{DateTime.UtcNow:yyyyMMdd_HHmmss}_{hash}.jpg";
                    var full = Path.Combine(AppPaths.WatchThumbnailsDir, fn);
                    ScreenCaptureWin.SaveWatchThumbnailJpeg(full, maxEdge: 480);
                    thumbRel = Path.Combine("watch-thumbnails", fn);
                    WatchSessionService.PruneThumbnails(200);
                    _lastWatchThumbHash = hash;
                }
                catch (Exception ex)
                {
                    NexusShell.Log("watch · thumbnail: " + ex.Message);
                }
            }

            var note = title.Trim();
            if (note.Length > 160)
                note = note[..160] + "…";
            if (string.IsNullOrEmpty(note))
                note = $"snapshot {DateTime.Now:T}";

            WatchSessionService.AppendSnapshot(
                note,
                hash,
                string.IsNullOrWhiteSpace(proc) ? null : proc.Trim(),
                string.IsNullOrWhiteSpace(title) ? null : title.Trim(),
                fam,
                thumbRel,
                "dashboard");

            NexusShell.Log($"watch · snapshot logged ({proc} · hash {hash ?? "?"})");
        }
        catch (Exception ex)
        {
            NexusShell.Log("watch · snapshot failed: " + ex.Message);
        }
    }

    private static async Task TryProactiveHintAsync(NexusSettings settings)
    {
        if (_proactiveBusy)
            return;
        var minSec = Math.Clamp(settings.ProactiveLlmMinIntervalSeconds, 60, 3600);
        if ((DateTime.UtcNow - _lastProactiveUtc).TotalSeconds < minSec)
            return;

        _proactiveBusy = true;
        try
        {
            string hash;
            try
            {
                hash = OperatingSystem.IsWindows()
                    ? ScreenCaptureWin.PrimaryMonitorSha256Prefix16() ?? "?"
                    : "?";
            }
            catch
            {
                hash = "?";
            }

            var (title, proc) = OperatingSystem.IsWindows()
                ? ForegroundWindowInfo.TryRead()
                : ("", "");

            var logCtx = NexusShell.FormatRecentLogForPrompt(6, 88);
            var prompt =
                "Watch work context. Reply with exactly one short helpful sentence in English (max. 220 characters, no greeting). " +
                $"Active app: {proc}. Window: {title}. Screen hash prefix: {hash}.";
            if (!string.IsNullOrEmpty(logCtx))
                prompt += "\nRecent app log: " + logCtx;

            var text = await LlmChatService.CompleteAsync(settings, prompt, false, false, default)
                .ConfigureAwait(false);

            var dq = WinUiShellState.UiDispatcher;
            if (dq != null)
            {
                if (!dq.TryEnqueue(() =>
                    {
                        _proactiveHintCache = text.Trim();
                        _lastProactiveUtc = DateTime.UtcNow;
                    }))
                {
                    _proactiveHintCache = text.Trim();
                    _lastProactiveUtc = DateTime.UtcNow;
                }
            }
            else
            {
                _proactiveHintCache = text.Trim();
                _lastProactiveUtc = DateTime.UtcNow;
            }
        }
        catch (Exception ex)
        {
            var dq = WinUiShellState.UiDispatcher;
            var msg = "(Proactive LLM error: " + ex.Message + ")";
            if (dq != null)
            {
                _ = dq.TryEnqueue(() =>
                {
                    _proactiveHintCache = msg;
                    _lastProactiveUtc = DateTime.UtcNow;
                });
            }
            else
            {
                _proactiveHintCache = msg;
                _lastProactiveUtc = DateTime.UtcNow;
            }
        }
        finally
        {
            _proactiveBusy = false;
        }
    }

    private static string FormatRitualsDashboardCard()
    {
        try
        {
            var list = RitualRecipeStore.LoadAll();
            var pending = RitualJobQueueStore.GetPendingCount();
            var sb = new StringBuilder();
            sb.AppendLine($"Recipes in library: {list.Count} · jobs pending: {pending}");
            if (list.Count == 0)
                sb.Append("(none yet — Operator flows)");
            else
            {
                sb.AppendLine("Excerpt:");
                foreach (var r in list.Take(6))
                    sb.AppendLine(" · " + r.ListCaption);
            }

            return sb.ToString().TrimEnd();
        }
        catch (Exception ex)
        {
            return "(Operator flows: " + ex.Message + ")";
        }
    }

    private static string FormatAiGuiUspRadar(NexusSettings settings, int knowCount)
    {
        var operatorRadar = OperatorUspPackService.BuildUspRadar(settings);
        var keyOk = DotEnvStore.HasProviderKey(settings.Provider);
        var hasKnowledge = knowCount > 0 || File.Exists(AppPaths.KnowledgeIndex) || File.Exists(AppPaths.KnowledgeChunks);
        var hasLiveContext = OperatingSystem.IsWindows();
        var automationArmed = string.Equals(settings.Safety.Profile, "power-user", StringComparison.OrdinalIgnoreCase);
        var watchArmed = string.Equals(settings.Mode, "watch", StringComparison.OrdinalIgnoreCase);
        var webUiReady = settings.EnableLocalToolHost;

        var ready = 0;
        ready += keyOk ? 1 : 0;
        ready += hasKnowledge ? 1 : 0;
        ready += hasLiveContext ? 1 : 0;
        ready += automationArmed ? 1 : 0;
        ready += watchArmed ? 1 : 0;
        ready += webUiReady ? 1 : 0;

        var usp = ready >= 5
            ? "USP candidate: governed AI operator cockpit with live Windows context, local knowledge, watch mode, and WebUI/tool-host bridge."
            : "USP candidate: privacy-aware AI desktop assistant that turns local knowledge and foreground context into governed operator flows.";

        var sb = new StringBuilder();
        sb.AppendLine(operatorRadar);
        sb.AppendLine();
        sb.AppendLine("Legacy readiness:");
        sb.AppendLine($"Readiness: {ready}/6");
        sb.AppendLine($"AI provider key: {(keyOk ? "ready" : "missing")} ({settings.Provider})");
        sb.AppendLine($"Local knowledge: {(hasKnowledge ? "ready" : "empty")}");
        sb.AppendLine($"GUI live context: {(hasLiveContext ? "Windows available" : "not available")}");
        sb.AppendLine($"Automation safety: {(automationArmed ? "power-user armed" : "simulation / guarded")}");
        sb.AppendLine($"Watch mode: {(watchArmed ? "active" : "off")}");
        sb.AppendLine($"WebUI bridge: {(webUiReady ? "local tool host enabled" : "disabled")}");
        sb.Append(usp);
        return sb.ToString();
    }
}
