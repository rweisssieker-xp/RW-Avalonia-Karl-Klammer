using System;
using System.Diagnostics;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Media;
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
    private readonly DispatcherTimer _statusActivityTimer;
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
        NexusShell.SetGlobalStatusLine = t => GlobalStatusBar.Text = t;
        NexusShell.SetGlobalBusyIndicator = v => GlobalStatusBusyBar.IsVisible = v;

        _dashboardTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
        _dashboardTimer.Tick += (_, _) => RefreshDashboard();
        _releasePollTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(60) };
        _releasePollTimer.Tick += OnReleasePollTick;
        _pollFallbackTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(120) };
        _pollFallbackTimer.Tick += OnPollFallbackTick;

        _statusActivityTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _statusActivityTimer.Tick += (_, _) => ActivityStatusHub.RefreshFromStores();

        Loaded += OnLoaded;
        Closing += OnWindowClosing;
        CompanionHub.StateChanged += OnCompanionVisualState;
        KeyDown += OnMainWindowKeyDown;

        BtnRefreshAll.Click += OnRefreshAll;
        BtnSaveSettings.Click += OnSaveSettings;
        BtnReindex.Click += OnReindex;
        BtnRefreshApp.Click += OnRefreshApp;
        BtnExportDiag.Click += (_, _) => TabDiagnostics.ExportDiagnostics();

        _dashboardTimer.Start();
        SetupPushToTalk();
    }

    private void OnMainWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.Key == Key.P)
        {
            e.Handled = true;
            ShowCommandPalette();
        }
    }

    private sealed class PaletteRow
    {
        public required string Id { get; init; }
        public required string Title { get; init; }
        public string Subtitle { get; init; } = "";
        public required Action Activate { get; init; }

        public string SearchBlob => $"{Title} {Subtitle} {Id}".ToLowerInvariant();
    }

    private async void ShowCommandPalette()
    {
        var muted = Application.Current?.FindResource("NexusMutedForegroundBrush") as IBrush
                    ?? Brushes.Gray;

        List<PaletteRow> BuildAll()
        {
            var rows = new List<PaletteRow>
            {
                new()
                {
                    Id = "tab:0", Title = "Ask", Subtitle = "Page · vision, plan, voice",
                    Activate = () =>
                    {
                        MainTabs.SelectedIndex = 0;
                        NexusShell.Log("Command palette: Ask");
                    }
                },
                new()
                {
                    Id = "tab:1", Title = "Dashboard", Subtitle = "Page · watch, 3D, cards",
                    Activate = () =>
                    {
                        MainTabs.SelectedIndex = 1;
                        NexusShell.Log("Command palette: Dashboard");
                    }
                },
                new()
                {
                    Id = "tab:2", Title = "Setup", Subtitle = "Page · provider, safety",
                    Activate = () =>
                    {
                        MainTabs.SelectedIndex = 2;
                        NexusShell.Log("Command palette: Setup");
                    }
                },
                new()
                {
                    Id = "tab:3", Title = "Knowledge", Subtitle = "Page · RAG, documents",
                    Activate = () =>
                    {
                        MainTabs.SelectedIndex = 3;
                        NexusShell.Log("Command palette: Knowledge");
                    }
                },
                new()
                {
                    Id = "tab:4", Title = "Operator flows", Subtitle = "Page · rituals, queue",
                    Activate = () =>
                    {
                        MainTabs.SelectedIndex = 4;
                        NexusShell.Log("Command palette: Operator flows");
                    }
                },
                new()
                {
                    Id = "tab:5", Title = "History", Subtitle = "Page · plan runs",
                    Activate = () =>
                    {
                        MainTabs.SelectedIndex = 5;
                        NexusShell.Log("Command palette: History");
                    }
                },
                new()
                {
                    Id = "tab:6", Title = "Diagnostics", Subtitle = "Page · logs",
                    Activate = () =>
                    {
                        MainTabs.SelectedIndex = 6;
                        NexusShell.Log("Command palette: Diagnostics");
                    }
                },
                new()
                {
                    Id = "tab:7", Title = "Console", Subtitle = "Page · CLI agents",
                    Activate = () =>
                    {
                        MainTabs.SelectedIndex = 7;
                        NexusShell.Log("Command palette: Console");
                    }
                },
                new()
                {
                    Id = "tab:8", Title = "Live Context", Subtitle = "Page · inspector",
                    Activate = () =>
                    {
                        MainTabs.SelectedIndex = 8;
                        NexusShell.Log("Command palette: Live Context");
                    }
                },
                new()
                {
                    Id = "tab:9", Title = "Experiments", Subtitle = "Page · tier C",
                    Activate = () =>
                    {
                        MainTabs.SelectedIndex = 9;
                        NexusShell.Log("Command palette: Experiments");
                    }
                },
                new()
                {
                    Id = "action:refresh_all", Title = "Refresh all", Subtitle = "Action · reload .env, tabs, theme",
                    Activate = () => OnRefreshAll(this, new RoutedEventArgs())
                },
                new()
                {
                    Id = "action:save_settings", Title = "Save settings", Subtitle = "Action · write settings.json",
                    Activate = () => OnSaveSettings(this, new RoutedEventArgs())
                },
                new()
                {
                    Id = "action:reindex", Title = "Reindex knowledge", Subtitle = "Action · chunks + FTS (+ embeddings if configured)",
                    Activate = () => OnReindex(this, new RoutedEventArgs())
                },
                new()
                {
                    Id = "action:refresh_app", Title = "Refresh active app", Subtitle = "Action · Live Context tile",
                    Activate = () => OnRefreshApp(this, new RoutedEventArgs())
                },
                new()
                {
                    Id = "action:export_diag", Title = "Export diagnostics", Subtitle = "Action · windows/data log",
                    Activate = () => TabDiagnostics.ExportDiagnostics()
                },
                new()
                {
                    Id = "action:handbook", Title = "Open handbook", Subtitle = "Action · user manual .md",
                    Activate = () => OnOpenHandbookClick(this, new RoutedEventArgs())
                },
                new()
                {
                    Id = "action:panic", Title = "Panic stop", Subtitle = "Action · cancel Ask / plan work",
                    Activate = () =>
                    {
                        MainTabs.SelectedIndex = 0;
                        TabAsk.RequestPanicStop();
                    }
                }
            };
            return rows;
        }

        static bool MatchesFilter(string query, PaletteRow r)
        {
            var q = query.Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(q))
                return true;
            var parts = q.Split([' '], StringSplitOptions.RemoveEmptyEntries);
            var blob = r.SearchBlob;
            return parts.All(p => blob.Contains(p, StringComparison.Ordinal));
        }

        var all = BuildAll();
        var recent = CommandPaletteRecentStore.Load().ToList();

        var filterBox = new TextBox
        {
            Watermark = "Search pages and actions…",
            Margin = new Thickness(0, 0, 0, 8)
        };
        var list = new ListBox
        {
            Margin = new Thickness(0, 4, 0, 0),
            MinHeight = 320,
            MaxHeight = 400
        };
        list.ItemTemplate = new FuncDataTemplate<PaletteRow>((value, _) =>
        {
            if (value is null)
                return new TextBlock();
            var sp = new StackPanel { Spacing = 2 };
            sp.Children.Add(new TextBlock
            {
                Text = value.Title,
                FontWeight = FontWeight.SemiBold
            });
            if (!string.IsNullOrEmpty(value.Subtitle))
            {
                sp.Children.Add(new TextBlock
                {
                    Text = value.Subtitle,
                    FontSize = 11,
                    Foreground = muted,
                    TextWrapping = TextWrapping.Wrap
                });
            }

            return sp;
        }, supportsRecycling: false);

        void ApplyFilter()
        {
            var q = filterBox.Text ?? "";
            var filtered = all.Where(r => MatchesFilter(q, r)).ToList();
            var ordered = filtered
                .OrderBy(r =>
                {
                    var i = recent.IndexOf(r.Id);
                    return i < 0 ? 999 : i;
                })
                .ThenBy(r => r.Title, StringComparer.OrdinalIgnoreCase)
                .ToList();
            list.ItemsSource = ordered;
            list.SelectedIndex = ordered.Count > 0 ? 0 : -1;
        }

        var dlg = new Window
        {
            Title = "Command palette",
            Width = 520,
            Height = 480,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new StackPanel
            {
                Margin = new Thickness(12),
                Spacing = 4,
                Children =
                {
                    new TextBlock
                    {
                        Text = "Ctrl+P · type to filter · ↑↓ navigate · Enter run · Esc close",
                        TextWrapping = TextWrapping.Wrap,
                        FontSize = 11,
                        Foreground = muted
                    },
                    filterBox,
                    list
                }
            }
        };

        filterBox.TextChanged += (_, _) => ApplyFilter();
        ApplyFilter();

        void Go()
        {
            if (list.SelectedItem is not PaletteRow row)
                return;
            CommandPaletteRecentStore.Touch(row.Id);
            try
            {
                row.Activate();
            }
            catch (Exception ex)
            {
                NexusShell.Log("Command palette action: " + ex.Message);
            }

            dlg.Close();
        }

        list.DoubleTapped += (_, _) => Go();
        list.KeyDown += (_, ke) =>
        {
            if (ke.Key == Key.Enter)
            {
                ke.Handled = true;
                Go();
            }
        };

        filterBox.KeyDown += (_, ke) =>
        {
            if (ke.Key == Key.Down && list.ItemCount > 0)
            {
                ke.Handled = true;
                list.Focus();
                list.SelectedIndex = 0;
            }
            else if (ke.Key == Key.Enter && list.SelectedItem is PaletteRow)
            {
                ke.Handled = true;
                Go();
            }
        };

        dlg.KeyDown += (_, ke) =>
        {
            if (ke.Key == Key.Escape)
            {
                ke.Handled = true;
                dlg.Close();
            }
        };

        dlg.Opened += (_, _) =>
        {
            filterBox.Focus();
            filterBox.SelectAll();
        };

        await dlg.ShowDialog(this);
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
        ApplyShellChromeFromSettings();
        DotEnvStore.Invalidate();
        SetupPushToTalk();
        TabAsk.RefreshPaneLayoutFromSettings();
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
        ActivityStatusHub.RefreshFromStores();
        _statusActivityTimer.Start();
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
        _statusActivityTimer.Stop();
        NexusShell.SetGlobalStatusLine = null;
        NexusShell.SetGlobalBusyIndicator = null;
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
        ApplyShellChromeFromSettings();
        RefreshDashboard();
        RefreshHeaderBadges();
        NexusShell.Log("refresh all — .env reloaded.");
        ApplyLocalToolHost();
        TabAsk.RefreshPaneLayoutFromSettings();
    }

    private void OnSaveSettings(object? sender, RoutedEventArgs e)
    {
        _settings = TabSetup.Gather();
        TabAsk.CaptureAskPaneWeightsInto(_settings);
        _settings.ShellHeaderDetailsExpanded = HeaderDetailsExpander.IsExpanded;
        _settingsStore.Save(_settings);
        TabAsk.SetSettingsProvider(() => _settings);
        NexusContext.GetSettings = () => _settings;
        ThemeApplier.ApplyUiTheme(_settings.UiTheme);
        ApplyShellChromeFromSettings();
        NexusShell.Log("settings.json saved.");
        RefreshHeaderBadges();
        ApplyLocalToolHost();
        TabAsk.RefreshPaneLayoutFromSettings();
        NotifyUiBrief("Settings saved · Ctrl+P palette");
    }

    private void OnReindex(object? sender, RoutedEventArgs e)
    {
        KnowledgeIndexService.Rebuild();
        TabKnowledge.RefreshList();
        TabRituals.ReloadLibrary();
        RefreshHeaderBadges();
        _ = EmbeddingRagService.RebuildIfConfiguredAsync(default);
        NexusShell.Log("reindex knowledge → index + chunks + local FTS5 (knowledge-fts.db); embeddings in background if OPENAI_API_KEY + RAG.");
        NotifyUiBrief("Knowledge reindexed");
    }

    private void ApplyShellChromeFromSettings()
    {
        MainTabs.TabStripPlacement = _settings.UseVerticalTabs ? Dock.Left : Dock.Top;
        HeaderDetailsExpander.IsExpanded = _settings.ShellHeaderDetailsExpanded;
    }

    private void NotifyUiBrief(string message)
    {
        NexusShell.SetGlobalStatus(message);
        _ = Dispatcher.UIThread.InvokeAsync(async () =>
        {
            await Task.Delay(2000).ConfigureAwait(true);
            ActivityStatusHub.RefreshFromStores();
        });
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
                $"Knowledge files: {knowCount} · Operator flows: {ritualCount} · Jobs pending: {pending}\n" +
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
            $"Profile: {_settings.Safety.Profile}\nPanic: {_settings.Safety.PanicStopEnabled}\nneverAutoSend: {_settings.Safety.NeverAutoSend}\n\n— Flow jobs —\n{RitualJobQueueStore.FormatDashboardSummary()}\n\n— Resume —\n{FlowResumeStore.FormatSummary()}",
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
                sb.Append("(none yet — Operator flows tab)");
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
