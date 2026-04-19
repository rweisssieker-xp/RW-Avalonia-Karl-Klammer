using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CarolusNexus.Models;
using CarolusNexus.Services;
using CarolusNexus.Views;

namespace CarolusNexus;

public partial class MainWindow : Window
{
    private KarlCompanionWindow? _companion;
    private readonly SettingsStore _settingsStore = new();
    private NexusSettings _settings = new();
    private readonly DispatcherTimer _dashboardTimer;
    private readonly DispatcherTimer _releasePollTimer;
    private readonly DispatcherTimer _pollFallbackTimer;
    private PushToTalkHotkeyWindow? _hotkeyWindow;
    private bool _pollFallbackDown;
    private int _pttVk = PushToTalkKey.DefaultVirtualKey;
    private DateTime _lastWatchUtc = DateTime.MinValue;
    private LocalToolHost? _toolHost;
    private DateTime _lastProactiveUtc = DateTime.MinValue;
    private string _proactiveHintCache = "";
    private bool _proactiveBusy;

    public MainWindow()
    {
        InitializeComponent();
        AppPaths.DiscoverRepoRoot();
        AppPaths.EnsureDataTree();

        NexusShell.AppendGlobalLog = line => TabDiagnostics.Append(line);

        _dashboardTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
        _dashboardTimer.Tick += (_, _) => RefreshDashboard();
        _releasePollTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(60) };
        _releasePollTimer.Tick += OnReleasePollTick;
        _pollFallbackTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(120) };
        _pollFallbackTimer.Tick += OnPollFallbackTick;

        Loaded += OnLoaded;
        Closing += OnWindowClosing;

        BtnRefreshAll.Click += OnRefreshAll;
        BtnSaveSettings.Click += OnSaveSettings;
        BtnReindex.Click += OnReindex;
        BtnRefreshApp.Click += OnRefreshApp;
        BtnExportDiag.Click += (_, _) => TabDiagnostics.ExportDiagnostics();

        _dashboardTimer.Start();
        SetupPushToTalk();
    }

    private void RefreshPushToTalkKey() =>
        _pttVk = PushToTalkKey.ResolveVirtualKey(DotEnvStore.Get("PUSH_TO_TALK_KEY"));

    private void SetupPushToTalk()
    {
        _releasePollTimer.Stop();
        _pollFallbackTimer.Stop();
        if (!OperatingSystem.IsWindows())
            return;

        RefreshPushToTalkKey();
        try
        {
            _hotkeyWindow?.Dispose();
            _hotkeyWindow = new PushToTalkHotkeyWindow(() =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    TabAsk.NotifyGlobalPushToTalkPressed();
                    if (TabAsk.AwaitsGlobalHotkeyRelease)
                        _releasePollTimer.Start();
                });
            });

            if (_hotkeyWindow.TryRegister(_pttVk))
            {
                NexusShell.Log($"PTT: globaler Hotkey (VK 0x{_pttVk:X}, MOD_NOREPEAT).");
                return;
            }
        }
        catch (Exception ex)
        {
            NexusShell.Log("PTT Hotkey: " + ex.Message);
        }

        _hotkeyWindow?.Dispose();
        _hotkeyWindow = null;
        _pollFallbackTimer.Start();
        NexusShell.Log("PTT: Fallback-Polling (Hotkey konnte nicht registriert werden).");
    }

    private void OnReleasePollTick(object? sender, EventArgs e)
    {
        if (!OperatingSystem.IsWindows())
            return;
        if (!TabAsk.AwaitsGlobalHotkeyRelease)
        {
            _releasePollTimer.Stop();
            return;
        }

        if ((Win32AsyncKey.GetAsyncKeyState(_pttVk) & 0x8000) == 0)
        {
            _releasePollTimer.Stop();
            _ = TabAsk.NotifyGlobalPushToTalkReleasedAsync();
        }
    }

    private void OnPollFallbackTick(object? sender, EventArgs e)
    {
        if (!OperatingSystem.IsWindows())
            return;
        var down = (Win32AsyncKey.GetAsyncKeyState(_pttVk) & 0x8000) != 0;
        if (down && !_pollFallbackDown)
            TabAsk.NotifyGlobalPushToTalkPressed();

        if (!down && _pollFallbackDown)
            _ = TabAsk.NotifyGlobalPushToTalkReleasedAsync();
        _pollFallbackDown = down;
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        _settings = _settingsStore.LoadOrDefault();
        TabSetup.Apply(_settings);
        TabSetup.RefreshEnvSummary();
        TabAsk.SetSettingsProvider(() => _settings);
        NexusContext.GetSettings = () => _settings;
        DotEnvStore.Invalidate();
        SetupPushToTalk();
        RefreshDashboard();
        RefreshHeaderBadges();
        ApplyKarlCursor();

        if (!OperatingSystem.IsWindows())
        {
            CompanionToggle.IsChecked = false;
            CompanionToggle.IsEnabled = false;
        }
        else
        {
            _companion = new KarlCompanionWindow();
            CompanionToggle.IsCheckedChanged += OnCompanionToggleChanged;
            if (CompanionToggle.IsChecked == true)
                _companion.Show();
        }

        NexusShell.Log("Carolus Nexus — Tray-Icon; Schließen minimiert ins Tray. „power-user“: echte Plan-Schritte.");
        ApplyLocalToolHost();
    }

    public async Task AskFromClipboardAsync()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();

        var board = Clipboard;
        if (board == null)
        {
            NexusShell.Log("Tray Ask: Zwischenablage nicht verfügbar.");
            MainTabs.SelectedIndex = 0;
            return;
        }

        var clipText = await board.GetTextAsync().ConfigureAwait(true);
        if (string.IsNullOrWhiteSpace(clipText))
        {
            NexusShell.Log("Tray Ask: Zwischenablage leer oder kein Text.");
            MainTabs.SelectedIndex = 0;
            return;
        }

        MainTabs.SelectedIndex = 0;
        await TabAsk.RunAskFromExternalAsync(clipText.Trim()).ConfigureAwait(true);
    }

    private void OnWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        if (!AppLifecycle.UserRequestedExit)
        {
            e.Cancel = true;
            Hide();
            NexusShell.Log("Fenster ins Tray — Beenden: Tray-Menü „Beenden“.");
            return;
        }

        _companion?.Close();
        _companion = null;
        _dashboardTimer.Stop();
        _releasePollTimer.Stop();
        _pollFallbackTimer.Stop();
        _hotkeyWindow?.Dispose();
        _hotkeyWindow = null;
        _toolHost?.Dispose();
        _toolHost = null;
    }

    private void OnCompanionToggleChanged(object? sender, RoutedEventArgs e)
    {
        if (_companion == null)
            return;
        if (CompanionToggle.IsChecked == true)
            _companion.Show();
        else
            _companion.Hide();
    }

    private void ApplyKarlCursor()
    {
        var karl = KarlCursorFactory.Create();
        Cursor = karl;
        foreach (var button in this.GetVisualDescendants().OfType<Button>())
            button.Cursor = karl;
        foreach (var scene in this.GetVisualDescendants().OfType<OfficeScene3D>())
            scene.Cursor = karl;
    }

    private void OnRefreshAll(object? sender, RoutedEventArgs e)
    {
        _settings = _settingsStore.LoadOrDefault();
        TabSetup.Apply(_settings);
        TabSetup.RefreshEnvSummary();
        TabKnowledge.RefreshList();
        TabHistory.Refresh();
        TabRituals.ReloadLibrary();
        DotEnvStore.Invalidate();
        SetupPushToTalk();
        TabAsk.SetSettingsProvider(() => _settings);
        NexusContext.GetSettings = () => _settings;
        RefreshDashboard();
        RefreshHeaderBadges();
        NexusShell.Log("refresh all — .env neu eingelesen.");
        ApplyLocalToolHost();
    }

    private void OnSaveSettings(object? sender, RoutedEventArgs e)
    {
        _settings = TabSetup.Gather();
        _settingsStore.Save(_settings);
        TabAsk.SetSettingsProvider(() => _settings);
        NexusContext.GetSettings = () => _settings;
        NexusShell.Log("settings.json gespeichert.");
        RefreshHeaderBadges();
        ApplyLocalToolHost();
    }

    private void OnReindex(object? sender, RoutedEventArgs e)
    {
        KnowledgeIndexService.Rebuild();
        TabKnowledge.RefreshList();
        TabRituals.ReloadLibrary();
        RefreshHeaderBadges();
        _ = EmbeddingRagService.RebuildIfConfiguredAsync(default);
        NexusShell.Log("reindex knowledge → Index + Chunks; Embeddings werden im Hintergrund gebaut (OPENAI_API_KEY + RAG).");
    }

    private void OnRefreshApp(object? sender, RoutedEventArgs e)
    {
        if (!OperatingSystem.IsWindows())
        {
            NexusShell.Log("Aktives Fenster: nur unter Windows.");
            return;
        }

        var (title, proc) = ForegroundWindowInfo.TryRead();
        var fam = OperatorAdapterRegistry.ResolveFamily(proc, title);
        TileLive.Text = $"Aktiv: {proc} · „{title}“ → Adapter-Familie: {fam} @ {DateTime.Now:T}";
        NexusShell.Log("Live Context: " + TileLive.Text);
    }

    private void RefreshHeaderBadges()
    {
        BadgeEnv.Text = $"Environment: {_settings.Provider} / {_settings.Mode}";
        BadgeSpeech.Text = DotEnvStore.HasProviderKey(_settings.Provider)
            ? "LLM: .env Key OK"
            : "LLM: Key fehlt";
        BadgeKnow.Text = $"Knowledge: {(_settings.UseLocalKnowledge ? "ein" : "aus")}";
        BadgeAuto.Text = OperatingSystem.IsWindows() &&
                         string.Equals(_settings.Safety.Profile, "power-user", StringComparison.OrdinalIgnoreCase)
            ? "Automation: Win32 (power-user)"
            : "Automation: Simulation";
        TileEnv.Text = $"Provider {_settings.Provider}, Modell „{_settings.Model}“, Safety {_settings.Safety.Profile}";
        var idx = File.Exists(AppPaths.KnowledgeIndex);
        var ch = File.Exists(AppPaths.KnowledgeChunks);
        var emb = File.Exists(AppPaths.KnowledgeEmbeddings);
        TileMemory.Text =
            $"Index: {(idx ? "ja" : "nein")}, Chunks: {(ch ? "ja" : "nein")}, Embeddings: {(emb ? "ja" : "nein")} — {AppPaths.DataDir}";
    }

    private void RefreshDashboard()
    {
        var knowCount = Directory.Exists(AppPaths.KnowledgeDir)
            ? Directory.GetFiles(AppPaths.KnowledgeDir).Length
            : 0;
        static string Cap(string? s, int max = 3500)
        {
            if (string.IsNullOrEmpty(s)) return "(leer)";
            return s.Length <= max ? s : s[..max] + "\n…";
        }

        MaybeAppendWatchSnapshot();

        if (!string.Equals(_settings.Mode, "watch", StringComparison.OrdinalIgnoreCase) ||
            !_settings.ProactiveDashboardLlm)
            _proactiveHintCache = "";

        if (_settings.ProactiveDashboardLlm
            && string.Equals(_settings.Mode, "watch", StringComparison.OrdinalIgnoreCase)
            && DotEnvStore.HasProviderKey(_settings.Provider))
            _ = TryProactiveHintAsync();

        string proactive;
        if (string.Equals(_settings.Mode, "watch", StringComparison.OrdinalIgnoreCase))
        {
            if (_settings.ProactiveDashboardLlm && DotEnvStore.HasProviderKey(_settings.Provider))
            {
                proactive = string.IsNullOrWhiteSpace(_proactiveHintCache)
                    ? $"Watch: Snapshots alle {Math.Clamp(_settings.WatchSnapshotIntervalSeconds, 15, 600)}s. Proaktiver LLM-Hinweis wird geladen …"
                    : _proactiveHintCache;
            }
            else
            {
                proactive =
                    $"Watch: Snapshots alle {Math.Clamp(_settings.WatchSnapshotIntervalSeconds, 15, 600)}s → {Path.GetFileName(AppPaths.WatchSessions)}";
            }
        }
        else
        {
            proactive =
                "Modus „watch“ + optional proaktiver LLM-Hinweis in Setup für Dashboard-Tipps.";
        }

        TabDashboard.RefreshSummaries(
            env: $"Provider: {_settings.Provider}\nModus: {_settings.Mode}\nModell: {_settings.Model}\n.env: {(DotEnvSummary.FileExists ? "ja" : "fehlt")}",
            know: $"Dateien in knowledge\\: {knowCount}\nIndex: {(File.Exists(AppPaths.KnowledgeIndex) ? "ja" : "nein")}\nChunks: {(File.Exists(AppPaths.KnowledgeChunks) ? "ja" : "nein")}\nEmbeddings: {(File.Exists(AppPaths.KnowledgeEmbeddings) ? "ja" : "nein")}",
            live: TileLive.Text ?? "—",
            proactive,
            gov: $"Profil: {_settings.Safety.Profile}\nPanic: {_settings.Safety.PanicStopEnabled}",
            rituals: Cap(File.Exists(AppPaths.AutomationRecipes) ? File.ReadAllText(AppPaths.AutomationRecipes) : null),
            watch: Cap(File.Exists(AppPaths.WatchSessions) ? File.ReadAllText(AppPaths.WatchSessions) : null)
        );
    }

    private async Task TryProactiveHintAsync()
    {
        if (_proactiveBusy)
            return;
        var minSec = Math.Clamp(_settings.ProactiveLlmMinIntervalSeconds, 60, 3600);
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

            var prompt =
                "Watch-Arbeitskontext. Antworte mit genau einem knappen, hilfreichen Satz auf Deutsch (max. 220 Zeichen, keine Begrüßung). " +
                $"Aktive App: {proc}. Fenster: {title}. Bildschirm-Hash-Präfix: {hash}.";

            var text = await LlmChatService.CompleteAsync(_settings, prompt, false, false, default)
                .ConfigureAwait(false);

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                _proactiveHintCache = text.Trim();
                _lastProactiveUtc = DateTime.UtcNow;
            });
        }
        catch (Exception ex)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                _proactiveHintCache = "(Proaktiv-LLM Fehler: " + ex.Message + ")";
                _lastProactiveUtc = DateTime.UtcNow;
            });
        }
        finally
        {
            _proactiveBusy = false;
        }
    }

    private void ApplyLocalToolHost()
    {
        _toolHost?.Dispose();
        _toolHost = null;
        if (!_settings.EnableLocalToolHost)
            return;
        if (!OperatingSystem.IsWindows())
        {
            NexusShell.Log("Local tool host: nur unter Windows.");
            return;
        }

        try
        {
            var port = Math.Clamp(_settings.LocalToolHostPort, 1024, 65535);
            DotEnvStore.Invalidate();
            var tok = DotEnvStore.Get("LOCAL_TOOL_TOKEN")?.Trim();
            var h = new LocalToolHost();
            h.Start(port, string.IsNullOrEmpty(tok) ? null : tok);
            _toolHost = h;
            if (string.IsNullOrEmpty(tok))
                NexusShell.Log("Local tool host: ohne LOCAL_TOOL_TOKEN (nur localhost).");
        }
        catch (Exception ex)
        {
            NexusShell.Log("Local tool host Start: " + ex.Message);
        }
    }

    private void MaybeAppendWatchSnapshot()
    {
        if (!OperatingSystem.IsWindows())
            return;
        if (!string.Equals(_settings.Mode, "watch", StringComparison.OrdinalIgnoreCase))
            return;

        var now = DateTime.UtcNow;
        var interval = Math.Clamp(_settings.WatchSnapshotIntervalSeconds, 15, 600);
        if ((now - _lastWatchUtc).TotalSeconds < interval)
            return;
        _lastWatchUtc = now;

        try
        {
            var hash = ScreenCaptureWin.PrimaryMonitorSha256Prefix16();
            WatchSessionService.AppendSnapshot($"watch · Dashboard {DateTime.Now:T}", hash);
            NexusShell.Log("watch · Snapshot (primärer Monitor-Hash) protokolliert.");
        }
        catch (Exception ex)
        {
            NexusShell.Log("watch · Snapshot fehlgeschlagen: " + ex.Message);
        }
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Hide();

    private void OnOpenHandbookClick(object? sender, RoutedEventArgs e)
    {
        var md = Path.Combine(AppPaths.RepoRoot, "docs", "Carolus-Nexus-Benutzerhandbuch.md");
        if (!File.Exists(md))
        {
            NexusShell.Log($"Handbuch nicht gefunden: {md}");
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = md,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            NexusShell.Log($"Handbuch öffnen fehlgeschlagen: {ex.Message}");
        }
    }
}
