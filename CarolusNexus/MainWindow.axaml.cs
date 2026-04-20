using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia;
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
    private string? _lastWatchThumbHash;
    private LocalToolHost? _toolHost;
    private DateTime _lastProactiveUtc = DateTime.MinValue;
    private string _proactiveHintCache = "";
    private bool _proactiveBusy;
    private bool? _headerTilesNarrowMode;

    public MainWindow()
    {
        InitializeComponent();
        LayoutUpdated += (_, _) =>
        {
            ApplyHeaderTilesResponsive();
            RefreshLayoutBadge();
        };
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
        CompanionHub.StateChanged += OnCompanionVisualState;

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
                NexusShell.Log($"PTT: global hotkey (VK 0x{_pttVk:X}, MOD_NOREPEAT).");
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
        NexusShell.Log("PTT: fallback polling (hotkey registration failed).");
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
        NexusContext.RunWin32StepOnUiThreadAsync = async work =>
            await Dispatcher.UIThread.InvokeAsync(work);
        ThemeApplier.ApplyUiTheme(_settings.UiTheme);
        DotEnvStore.Invalidate();
        SetupPushToTalk();
        RefreshDashboard();
        RefreshHeaderBadges();
        RefreshLayoutBadge();
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

        NexusShell.Log("Carolus Nexus — tray icon; Close minimizes to tray. „power-user“: real plan steps.");
        _ = OperatorAdapterRegistry.Adapters;
        NexusShell.Log(OfflineEdgeCapabilities.Describe());
        ApplyLocalToolHost();
        ApplyHeaderTilesResponsive();
    }

    private void ApplyHeaderTilesResponsive()
    {
        var w = Bounds.Width;
        if (w <= 0)
            return;
        var narrow = w < ResponsiveLayout.NarrowMax;
        if (_headerTilesNarrowMode == narrow)
            return;
        _headerTilesNarrowMode = narrow;

        HeaderTilesGrid.ColumnDefinitions.Clear();
        HeaderTilesGrid.RowDefinitions.Clear();
        if (narrow)
        {
            HeaderTilesGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            HeaderTilesGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            HeaderTilesGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            HeaderTilesGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            Grid.SetColumn(HeaderTileMemory, 0);
            Grid.SetRow(HeaderTileMemory, 0);
            Grid.SetColumn(HeaderTileLive, 0);
            Grid.SetRow(HeaderTileLive, 1);
            Grid.SetColumn(HeaderTileEnv, 0);
            Grid.SetRow(HeaderTileEnv, 2);
        }
        else
        {
            HeaderTilesGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            HeaderTilesGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            HeaderTilesGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            HeaderTilesGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            Grid.SetColumn(HeaderTileMemory, 0);
            Grid.SetRow(HeaderTileMemory, 0);
            Grid.SetColumn(HeaderTileLive, 1);
            Grid.SetRow(HeaderTileLive, 0);
            Grid.SetColumn(HeaderTileEnv, 2);
            Grid.SetRow(HeaderTileEnv, 0);
        }
    }

    public async Task AskFromClipboardAsync()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();

        var board = Clipboard;
        if (board == null)
        {
            NexusShell.Log("Tray Ask: clipboard not available.");
            MainTabs.SelectedIndex = 0;
            return;
        }

        var clipText = await board.GetTextAsync().ConfigureAwait(true);
        if (string.IsNullOrWhiteSpace(clipText))
        {
            NexusShell.Log("Tray Ask: clipboard empty or not text.");
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
            NexusShell.Log("Window sent to tray — Quit: tray menu „Quit“.");
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
        CompanionHub.StateChanged -= OnCompanionVisualState;
    }

    private void OnCompanionVisualState(CompanionVisualState state) =>
        _companion?.ApplyVisualState(state);

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
        ThemeApplier.ApplyUiTheme(_settings.UiTheme);
        RefreshDashboard();
        RefreshHeaderBadges();
        NexusShell.Log("refresh all — .env reloaded.");
        ApplyLocalToolHost();
    }

    private void OnSaveSettings(object? sender, RoutedEventArgs e)
    {
        _settings = TabSetup.Gather();
        _settingsStore.Save(_settings);
        TabAsk.SetSettingsProvider(() => _settings);
        NexusContext.GetSettings = () => _settings;
        ThemeApplier.ApplyUiTheme(_settings.UiTheme);
        NexusShell.Log("settings.json saved.");
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
        NexusShell.Log("reindex knowledge → index + chunks + local FTS5 (knowledge-fts.db); embeddings in background if OPENAI_API_KEY + RAG.");
    }

    private void OnRefreshApp(object? sender, RoutedEventArgs e)
    {
        if (!OperatingSystem.IsWindows())
        {
            NexusShell.Log("Active window: Windows only.");
            return;
        }

        var (title, proc) = ForegroundWindowInfo.TryRead();
        var fam = OperatorAdapterRegistry.ResolveFamily(proc, title);
        TileLive.Text = $"Active: {proc} · „{title}“ → adapter family: {fam} @ {DateTime.Now:T}";
        NexusShell.Log("Live Context: " + TileLive.Text);
    }

    private void RefreshLayoutBadge()
    {
        var w = Bounds.Width;
        if (w <= 0)
            return;
        BadgeLayout.Text = $"Layout: {ResponsiveLayout.GetBand(w)}";
    }

    private void RefreshHeaderBadges()
    {
        BadgeEnv.Text = $"Environment: {_settings.Provider} / {_settings.Mode}";
        BadgeSpeech.Text = DotEnvStore.HasProviderKey(_settings.Provider)
            ? "LLM: .env Key OK"
            : "LLM: key missing";
        BadgeKnow.Text = $"Knowledge: {(_settings.UseLocalKnowledge ? "on" : "off")}";
        BadgeAuto.Text = OperatingSystem.IsWindows() &&
                         string.Equals(_settings.Safety.Profile, "power-user", StringComparison.OrdinalIgnoreCase)
            ? "Automation: Win32 (power-user)"
            : "Automation: Simulation";
        TileEnv.Text = $"Provider {_settings.Provider}, model „{_settings.Model}“, safety {_settings.Safety.Profile}";
        var idx = File.Exists(AppPaths.KnowledgeIndex);
        var ch = File.Exists(AppPaths.KnowledgeChunks);
        var fts = File.Exists(AppPaths.KnowledgeFtsDb);
        var emb = File.Exists(AppPaths.KnowledgeEmbeddings);
        TileMemory.Text =
            $"Index: {(idx ? "yes" : "no")}, Chunks: {(ch ? "yes" : "no")}, FTS5: {(fts ? "yes" : "no")}, Embeddings: {(emb ? "yes" : "no")} — {AppPaths.DataDir}";
    }

    private void RefreshDashboard()
    {
        var knowCount = Directory.Exists(AppPaths.KnowledgeDir)
            ? Directory.GetFiles(AppPaths.KnowledgeDir).Length
            : 0;

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
                    ? $"Watch: snapshots every {Math.Clamp(_settings.WatchSnapshotIntervalSeconds, 15, 600)}s. Proactive LLM hint loading …"
                    : _proactiveHintCache;
            }
            else
            {
                proactive =
                    $"Watch: snapshots every {Math.Clamp(_settings.WatchSnapshotIntervalSeconds, 15, 600)}s → {Path.GetFileName(AppPaths.WatchSessions)}";
            }
        }
        else
        {
            var keyOk = DotEnvStore.HasProviderKey(_settings.Provider);
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
                $"Knowledge files: {knowCount} · Ritual recipes: {ritualCount} · Jobs pending: {pending}\n" +
                $"LLM .env: {(keyOk ? "key OK" : "key missing")} ({_settings.Provider})";
        }

        var logExcerpt = NexusShell.FormatRecentLogForDashboard();
        if (!string.IsNullOrEmpty(logExcerpt))
            proactive += "\n\n— App log (recent) —\n" + logExcerpt;

        TabDashboard.RefreshSummaries(
            env: $"Provider: {_settings.Provider}\nMode: {_settings.Mode}\nModel: {_settings.Model}\n.env: {(DotEnvSummary.FileExists ? "yes" : "missing")}",
            know: $"Files in knowledge\\: {knowCount}\nIndex: {(File.Exists(AppPaths.KnowledgeIndex) ? "yes" : "no")}\nChunks: {(File.Exists(AppPaths.KnowledgeChunks) ? "yes" : "no")}\nEmbeddings: {(File.Exists(AppPaths.KnowledgeEmbeddings) ? "yes" : "no")}",
            live: TileLive.Text ?? "—",
            proactive,
            gov:
            $"Profile: {_settings.Safety.Profile}\nPanic: {_settings.Safety.PanicStopEnabled}\nneverAutoSend: {_settings.Safety.NeverAutoSend}\n\n— Ritual jobs —\n{RitualJobQueueStore.FormatDashboardSummary()}",
            rituals: FormatRitualsDashboardCard(),
            watch: WatchSessionService.FormatDashboardSummary()
        );
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
                sb.Append("(none yet — Rituals tab)");
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
            return "(Rituals: " + ex.Message + ")";
        }
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

            var logCtx = NexusShell.FormatRecentLogForPrompt(6, 88);
            var prompt =
                "Watch work context. Reply with exactly one short helpful sentence in English (max. 220 characters, no greeting). " +
                $"Active app: {proc}. Window: {title}. Screen hash prefix: {hash}.";
            if (!string.IsNullOrEmpty(logCtx))
                prompt += "\nRecent app log: " + logCtx;

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
                _proactiveHintCache = "(Proactive LLM error: " + ex.Message + ")";
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
            NexusShell.Log("Local tool host: Windows only.");
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
                NexusShell.Log("Local tool host: no LOCAL_TOOL_TOKEN (localhost only).");
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

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Hide();

    private void OnOpenHandbookClick(object? sender, RoutedEventArgs e)
    {
        var md = Path.Combine(AppPaths.RepoRoot, "docs", "Carolus-Nexus-Benutzerhandbuch.md");
        if (!File.Exists(md))
        {
            NexusShell.Log($"Handbook not found: {md}");
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
            NexusShell.Log($"Open handbook failed: {ex.Message}");
        }
    }
}
