using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CarolusNexus;
using CarolusNexus.Services;
using CarolusNexus_WinUI.Pages;
using Microsoft.UI.Dispatching;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using VirtualKey = Windows.System.VirtualKey;

namespace CarolusNexus_WinUI;

public sealed partial class MainWindow : Window
{
    private readonly Grid RootGrid = new();

    private readonly Frame _frame = new();
    private readonly NavigationView _nav = new()
    {
        IsBackButtonVisible = NavigationViewBackButtonVisible.Collapsed,
        IsSettingsVisible = false,
        PaneDisplayMode = NavigationViewPaneDisplayMode.Auto,
        OpenPaneLength = 280,
        CompactPaneLength = 48,
        ExpandedModeThresholdWidth = 1000,
        PaneTitle = "Carolus Nexus",
        OverflowLabelMode = NavigationViewOverflowLabelMode.MoreLabel,
        Background = new SolidColorBrush(Colors.Transparent)
    };

    private readonly TextBlock _badgeLayout = new() { FontSize = 11 };
    private readonly TextBlock _badgeEnv = new() { FontSize = 11 };
    private readonly TextBlock _badgeSpeech = new() { FontSize = 11 };
    private readonly TextBlock _badgeAuto = new() { FontSize = 11 };
    private readonly TextBlock _badgeKnow = new() { FontSize = 11 };
    private readonly TextBlock _tileMemory = new() { TextWrapping = TextWrapping.Wrap, FontSize = 11 };
    private readonly TextBlock _tileLive = new() { TextWrapping = TextWrapping.Wrap, FontSize = 11 };
    private readonly TextBlock _tileEnv = new() { TextWrapping = TextWrapping.Wrap, FontSize = 11 };
    private readonly TextBlock _statusLine = new() { Text = "Bereit" };
    private readonly ProgressBar _globalStatusBusyBar = new()
    {
        Width = 56,
        Height = 4,
        VerticalAlignment = VerticalAlignment.Center,
        IsIndeterminate = true,
        Visibility = Microsoft.UI.Xaml.Visibility.Collapsed
    };
    private readonly ToggleSwitch _companionToggle = new()
    {
        Header = "Companion",
        OnContent = "Follow cursor",
        OffContent = "Off",
        IsOn = false,
        MinWidth = 0,
        VerticalAlignment = VerticalAlignment.Center
    };
    private readonly GhostOperatorService _ghostOperator = new();
    private readonly GhostOperatorState _ghostState = new();
    private readonly Border _ghostPanel = new()
    {
        Width = 380,
        HorizontalAlignment = HorizontalAlignment.Right,
        VerticalAlignment = VerticalAlignment.Stretch,
        Visibility = Microsoft.UI.Xaml.Visibility.Collapsed,
        Margin = new Thickness(0, 0, 18, 18)
    };
    private readonly StackPanel _ghostPanelBody = new() { Spacing = 12 };
    private GhostOperatorSuggestion? _currentGhostSuggestion;

    public MainWindow()
    {
        Title = "Carolus Nexus";
        WinUiFluentChrome.ApplyMicaBackdrop(this);
        RootGrid.Background = new SolidColorBrush(Colors.Transparent);
        Content = RootGrid;

        _nav.MenuItems.Add(Mk("Ask", typeof(AskShellPage), Symbol.Message));
        _nav.MenuItems.Add(Mk("Dashboard", typeof(DashboardShellPage), Symbol.Home));
        _nav.MenuItems.Add(Mk("Setup", typeof(SetupShellPage), Symbol.Setting));
        _nav.MenuItems.Add(Mk("Knowledge", typeof(KnowledgeShellPage), Symbol.Bookmarks));
        _nav.MenuItems.Add(Mk("Operator flows", typeof(RitualsShellPage), Symbol.AllApps));
        _nav.MenuItems.Add(Mk("History", typeof(HistoryShellPage), Symbol.Clock));
        _nav.MenuItems.Add(Mk("Diagnostics", typeof(DiagnosticsShellPage), Symbol.Remote));
        _nav.MenuItems.Add(Mk("Console", typeof(ConsoleShellPage), Symbol.Keyboard));
        _nav.MenuItems.Add(Mk("Live Context", typeof(LiveContextShellPage), Symbol.View));
        _nav.MenuItems.Add(Mk("Experiments (Tier C)", typeof(ExperimentsShellPage), Symbol.Important));

        _nav.FooterMenuItems.Add(MkFooter("Command palette  ·  Ctrl+P"));

        _nav.Content = _frame;
        _nav.ItemInvoked += NavOnItemInvoked;
        _nav.Loaded += (_, _) =>
        {
            _nav.SelectedItem = _nav.MenuItems.OfType<NavigationViewItem>()
                .FirstOrDefault(i => i.Tag is Type t && t == typeof(DashboardShellPage))
                ?? _nav.MenuItems.OfType<NavigationViewItem>().FirstOrDefault();
            this.DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () =>
            {
                if (_nav.SelectedItem is NavigationViewItem first && first.Tag is Type t)
                    ShowShellPage(t);
            });
        };

        var header = BuildHeaderChrome();
        var root = new Grid { Background = new SolidColorBrush(Colors.Transparent) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.Children.Add(header);
        root.Children.Add(_nav);
        Grid.SetRow(_nav, 1);

        RootGrid.Children.Add(root);
        RootGrid.Children.Add(BuildGhostSidePanel());

        RootGrid.SizeChanged += (_, e) =>
        {
            var w = e.NewSize.Width;
            if (w > 0)
                RefreshLayoutBadge(w);
        };

        RootGrid.Loaded += (_, _) =>
        {
            RefreshLayoutBadge(RootGrid.ActualWidth);
            RefreshHeaderBadges();
        };

        var paletteAccel = new KeyboardAccelerator
        {
            Key = VirtualKey.P,
            Modifiers = Windows.System.VirtualKeyModifiers.Control
        };
        paletteAccel.Invoked += OnCommandPaletteAccelerator;
        _nav.KeyboardAccelerators.Add(paletteAccel);
        var shortcutHelpAccel = new KeyboardAccelerator
        {
            Key = VirtualKey.H,
            Modifiers = Windows.System.VirtualKeyModifiers.Control
        };
        shortcutHelpAccel.Invoked += async (_, args) =>
        {
            args.Handled = true;
            await ShowShortcutHelpAsync();
        };
        _nav.KeyboardAccelerators.Add(shortcutHelpAccel);
        var handbookAccel = new KeyboardAccelerator { Key = VirtualKey.F1 };
        handbookAccel.Invoked += (_, args) =>
        {
            args.Handled = true;
            OnHandbookClick(this, new RoutedEventArgs());
        };
        _nav.KeyboardAccelerators.Add(handbookAccel);

        RootGrid.Loaded += OnMainShellLoaded;
    }

    private UIElement BuildHeaderChrome()
    {
        var border = new Border
        {
            Padding = new Thickness(24, 18, 24, 18),
            BorderThickness = new Thickness(0, 0, 0, 1),
            BorderBrush = WinUiFluentChrome.SeparatorBrush,
            Background = WinUiFluentChrome.HeaderChromeBackground
        };
        WinUiFluentChrome.ApplyCardElevation(border, 4f);

        var stack = new StackPanel { Spacing = 16 };

        var titleRow = new Grid();
        titleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        titleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var titleBlock = new StackPanel { Spacing = 6 };
        var titleMain = new TextBlock { Text = "Carolus Nexus", TextWrapping = TextWrapping.Wrap };
        WinUiFluentChrome.ApplyTitleTextStyle(titleMain);
        titleBlock.Children.Add(titleMain);
        var titleSub = new TextBlock
        {
            Text = "Windows operator desktop · Karl Klammer",
            Foreground = WinUiFluentChrome.SecondaryTextBrush,
            IsTextSelectionEnabled = false,
            TextWrapping = TextWrapping.Wrap
        };
        WinUiFluentChrome.ApplySubtitleTextStyle(titleSub);
        titleBlock.Children.Add(titleSub);
        var titleHint = new TextBlock
        {
            Text = "Tray · Companion · PTT · Ask · Dashboard",
            Foreground = WinUiFluentChrome.TertiaryTextBrush,
            TextWrapping = TextWrapping.Wrap
        };
        WinUiFluentChrome.ApplyCaptionTextStyle(titleHint);
        var statusRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        statusRow.Children.Add(_globalStatusBusyBar);
        _statusLine.Foreground = WinUiFluentChrome.SecondaryTextBrush;
        WinUiFluentChrome.ApplyCaptionTextStyle(_statusLine);
        statusRow.Children.Add(_statusLine);
        titleBlock.Children.Add(statusRow);
        Grid.SetColumn(titleBlock, 0);
        titleRow.Children.Add(titleBlock);

        var titleBtns = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, VerticalAlignment = VerticalAlignment.Center };
        titleBtns.Children.Add(_companionToggle);
        var closeBtn = new Button { Content = "Minimize to tray", Padding = new Thickness(16, 10, 16, 10), CornerRadius = new CornerRadius(8) };
        closeBtn.Click += OnHeaderCloseClick;
        titleBtns.Children.Add(closeBtn);
        var handbook = new HyperlinkButton { Content = "Handbook", VerticalAlignment = VerticalAlignment.Center };
        handbook.Click += OnHandbookClick;
        titleBtns.Children.Add(handbook);
        var shortcuts = new HyperlinkButton { Content = "Shortcuts", VerticalAlignment = VerticalAlignment.Center };
        shortcuts.Click += async (_, _) => await ShowShortcutHelpAsync();
        titleBtns.Children.Add(shortcuts);
        var ghost = new Button { Content = "Ghost", Padding = new Thickness(14, 8, 14, 8), CornerRadius = new CornerRadius(8) };
        WinUiFluentChrome.AddShortcut(ghost, VirtualKey.G, Windows.System.VirtualKeyModifiers.Control, "Ctrl+G");
        ghost.Click += (_, _) => ToggleGhostPanel();
        titleBtns.Children.Add(ghost);
        Grid.SetColumn(titleBtns, 1);
        titleRow.Children.Add(titleBtns);
        stack.Children.Add(titleRow);

        var badges = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        foreach (var tb in new[] { _badgeLayout, _badgeEnv, _badgeSpeech, _badgeAuto, _badgeKnow })
        {
            tb.Foreground = WinUiFluentChrome.PrimaryTextBrush;
            badges.Children.Add(new Border
            {
                Margin = new Thickness(0, 0, 0, 0),
                Padding = new Thickness(12, 7, 12, 7),
                CornerRadius = new CornerRadius(WinUiFluentChrome.PillCornerRadius),
                BorderThickness = new Thickness(1),
                BorderBrush = WinUiFluentChrome.CardBorderBrush,
                Background = WinUiFluentChrome.BadgeBackground,
                Child = tb
            });
        }

        stack.Children.Add(badges);

        var tiles = new Grid { HorizontalAlignment = HorizontalAlignment.Stretch, ColumnSpacing = 12 };
        tiles.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        tiles.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        tiles.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var tile0 = MkTile("Operator memory", _tileMemory);
        var tile1 = MkTile("Live context", _tileLive);
        var tile2 = MkTile("Environment", _tileEnv);
        Grid.SetColumn(tile0, 0);
        Grid.SetColumn(tile1, 1);
        Grid.SetColumn(tile2, 2);
        tiles.Children.Add(tile0);
        tiles.Children.Add(tile1);
        tiles.Children.Add(tile2);
        stack.Children.Add(tiles);

        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        var bRefresh = MkHeaderActionButton("Refresh all");
        bRefresh.Click += (_, _) => OnRefreshAll();
        var bSave = MkHeaderActionButton("Save settings", accent: true);
        bSave.Click += (_, _) => OnSaveSettings();
        var bReindex = MkHeaderActionButton("Reindex knowledge");
        bReindex.Click += (_, _) => OnReindex();
        var bApp = MkHeaderActionButton("Refresh active app");
        bApp.Click += (_, _) => OnRefreshActiveApp();
        var bExport = MkHeaderActionButton("Export diagnostics");
        bExport.Click += (_, _) => OnExportDiagnostics();
        actions.Children.Add(bRefresh);
        actions.Children.Add(bSave);
        actions.Children.Add(bReindex);
        actions.Children.Add(bApp);
        actions.Children.Add(bExport);
        stack.Children.Add(actions);

        border.Child = stack;
        return border;
    }

    private static Button MkHeaderActionButton(string label, bool accent = false)
    {
        var b = new Button
        {
            Content = label,
            Margin = new Thickness(0, 0, 0, 0),
            Padding = new Thickness(16, 10, 16, 10),
            CornerRadius = new CornerRadius(8)
        };
        if (accent && Application.Current.Resources.TryGetValue("AccentButtonStyle", out var st) && st is Style accentStyle)
            b.Style = accentStyle;
        return b;
    }

    private static Border MkTile(string title, TextBlock body)
    {
        body.Foreground = WinUiFluentChrome.SecondaryTextBrush;
        var b = new Border
        {
            Padding = new Thickness(16, 14, 16, 14),
            Margin = new Thickness(0),
            CornerRadius = new CornerRadius(WinUiFluentChrome.CardCornerRadius),
            BorderThickness = new Thickness(1),
            BorderBrush = WinUiFluentChrome.CardBorderBrush,
            Background = WinUiFluentChrome.CardSurfaceBackground,
            Child = new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    new TextBlock
                    {
                        Text = title,
                        FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                        FontSize = 13,
                        Foreground = WinUiFluentChrome.PrimaryTextBrush
                    },
                    body
                }
            }
        };
        WinUiFluentChrome.ApplyCardElevation(b, 3f);
        return b;
    }

    private Border BuildGhostSidePanel()
    {
        _ghostPanel.CornerRadius = new CornerRadius(WinUiFluentChrome.CardCornerRadius);
        _ghostPanel.BorderThickness = new Thickness(1);
        _ghostPanel.BorderBrush = WinUiFluentChrome.CardBorderBrush;
        _ghostPanel.Background = WinUiFluentChrome.LayerChromeBackground;
        _ghostPanel.Padding = new Thickness(16);
        WinUiFluentChrome.ApplyCardElevation(_ghostPanel, 12f);

        var scroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = _ghostPanelBody
        };

        _ghostPanel.Child = scroll;
        RenderGhostPanel();
        return _ghostPanel;
    }

    private void ToggleGhostPanel()
    {
        if (_ghostPanel.Visibility == Microsoft.UI.Xaml.Visibility.Visible)
        {
            _ghostPanel.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
            return;
        }

        RefreshGhostSuggestion(force: true);
        _ghostPanel.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
    }

    private void RefreshGhostSuggestion(bool force = false)
    {
        _currentGhostSuggestion = _ghostOperator.TrySuggest(WinUiShellState.Settings, WinUiShellState.LiveContextLine, _ghostState);
        if (_currentGhostSuggestion != null)
            _ghostState.MarkShown(_currentGhostSuggestion);
        else if (force)
            _currentGhostSuggestion = new GhostOperatorSuggestion
            {
                Title = "No safe action detected",
                Situation = "Ghost Operator is watching local shell state, live context and safety settings.",
                ActionLabel = "Simulate",
                SecondaryLabel = "Open Ask",
                Why = "There is no confident, safe next action right now. Refresh active app or open Ask to create more context.",
                Intent = "ghost.empty",
                Risk = "idle",
                RequiresApproval = false,
                Confidence = 0.0
            };

        RenderGhostPanel();
    }

    private void RenderGhostPanel()
    {
        _ghostPanelBody.Children.Clear();

        var top = new Grid { ColumnSpacing = 10 };
        top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        top.Children.Add(new StackPanel
        {
            Spacing = 4,
            Children =
            {
                new TextBlock
                {
                    Text = "Ghost Operator",
                    FontSize = 20,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    Foreground = WinUiFluentChrome.PrimaryTextBrush
                },
                new TextBlock
                {
                    Text = "Small local AI cockpit · no autonomous execution",
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = WinUiFluentChrome.SecondaryTextBrush,
                    FontSize = 12
                }
            }
        });
        var close = new Button { Content = "Close", Padding = new Thickness(10, 6, 10, 6), CornerRadius = new CornerRadius(8) };
        close.Click += (_, _) => _ghostPanel.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
        Grid.SetColumn(close, 1);
        top.Children.Add(close);
        _ghostPanelBody.Children.Add(top);

        var refresh = new Button { Content = "Refresh suggestion", HorizontalAlignment = HorizontalAlignment.Stretch };
        WinUiFluentChrome.StyleActionButton(refresh, accent: true);
        refresh.Click += (_, _) => RefreshGhostSuggestion(force: true);
        _ghostPanelBody.Children.Add(refresh);

        _ghostPanelBody.Children.Add(WinUiFluentChrome.StatusTile("Runtime reality", "local side panel", "suggests actions; approve/run stays gated"));
        _ghostPanelBody.Children.Add(WinUiFluentChrome.StatusTile("Live context", string.IsNullOrWhiteSpace(WinUiShellState.LiveContextLine) ? "not refreshed" : "available", WinUiShellState.LiveContextLine));
        _ghostPanelBody.Children.Add(BuildGhostEvidenceCard());

        if (_currentGhostSuggestion == null)
        {
            _ghostPanelBody.Children.Add(WinUiFluentChrome.EmptyState(
                "No suggestion loaded",
                "Open Ghost again or refresh active app to let the local context scorer build a proposal.",
                "Ctrl+G toggles this panel."));
            return;
        }

        var simulate = new Button();
        var approve = new Button();
        var dismiss = new Button();
        var power = new Button();
        simulate.Click += (_, _) => SimulateGhostAction();
        approve.Click += (_, _) => ApproveGhostAction();
        dismiss.Click += (_, _) => DismissGhostAction();
        power.Click += async (_, _) => await OpenGhostPowerUserTargetAsync();

        _ghostPanelBody.Children.Add(WinUiFluentChrome.GhostOperatorCard(_currentGhostSuggestion, simulate, approve, dismiss, power));
        _ghostPanelBody.Children.Add(new InfoBar
        {
            IsOpen = true,
            Severity = _currentGhostSuggestion.RequiresApproval ? InfoBarSeverity.Warning : InfoBarSeverity.Informational,
            Title = _currentGhostSuggestion.RequiresApproval ? "Approval required" : "Safe preview",
            Message = _currentGhostSuggestion.RequiresApproval
                ? "This panel never executes risky operations silently. Use the PowerUser page for the full gate."
                : "Simulation shows the intended next step without touching external apps."
        });
    }

    private Border BuildGhostEvidenceCard()
    {
        var s = WinUiShellState.Settings;
        var knowledge = s.UseLocalKnowledge ? "local knowledge enabled" : "local knowledge off";
        var llm = DotEnvStore.HasProviderKey(s.Provider) ? ".env provider key OK" : ".env provider key missing";
        var automation = OperatingSystem.IsWindows() &&
                         string.Equals(s.Safety.Profile, "power-user", StringComparison.OrdinalIgnoreCase)
            ? "Win32 automation possible after approval"
            : "simulation / guarded automation";
        var live = string.IsNullOrWhiteSpace(WinUiShellState.LiveContextLine)
            ? "no active-app context captured"
            : WinUiShellState.LiveContextLine;

        var text = new TextBlock
        {
            Text =
                $"AI route: {s.Provider} / {s.Mode}\n" +
                $"Knowledge: {knowledge}\n" +
                $"LLM key: {llm}\n" +
                $"Automation: {automation}\n" +
                $"Live: {live}",
            TextWrapping = TextWrapping.Wrap,
            IsTextSelectionEnabled = true,
            Foreground = WinUiFluentChrome.SecondaryTextBrush,
            FontSize = 12
        };
        return WinUiFluentChrome.SectionCard("Evidence", "What Ghost used for this suggestion", text);
    }

    private void SimulateGhostAction()
    {
        if (_currentGhostSuggestion == null)
            return;
        NexusShell.Log($"Ghost Operator simulate → {_currentGhostSuggestion.Intent}: {_currentGhostSuggestion.ActionLabel}");
        _statusLine.Text = "Ghost simulation logged.";
    }

    private void ApproveGhostAction()
    {
        if (_currentGhostSuggestion == null)
            return;
        NexusShell.Log($"Ghost Operator approve requested → {_currentGhostSuggestion.Intent}. Routed to PowerUser gate, no silent execution.");
        _statusLine.Text = "Ghost approve routed to gated flow.";
        _ = OpenGhostPowerUserTargetAsync();
    }

    private void DismissGhostAction()
    {
        _ghostState.MarkIgnored();
        _currentGhostSuggestion = null;
        _statusLine.Text = "Ghost suggestion dismissed.";
        RenderGhostPanel();
    }

    private async Task OpenGhostPowerUserTargetAsync()
    {
        var intent = _currentGhostSuggestion?.Intent ?? "";
        if (intent.Contains("live", StringComparison.OrdinalIgnoreCase) || intent.Contains("ax", StringComparison.OrdinalIgnoreCase))
            await NavigateFromPaletteAsync(typeof(LiveContextShellPage));
        else if (intent.Contains("knowledge", StringComparison.OrdinalIgnoreCase))
            await NavigateFromPaletteAsync(typeof(KnowledgeShellPage));
        else if (intent.Contains("flow", StringComparison.OrdinalIgnoreCase) || intent.Contains("ritual", StringComparison.OrdinalIgnoreCase))
            await NavigateFromPaletteAsync(typeof(RitualsShellPage));
        else
            await NavigateFromPaletteAsync(typeof(AskShellPage));
    }

    private void RefreshLayoutBadge(double w)
    {
        if (w <= 0)
            return;
        _badgeLayout.Text = $"Layout: {ResponsiveLayout.GetBand(w)} ({(int)w}px)";
    }

    private void RefreshHeaderBadges()
    {
        var s = WinUiShellState.Settings;
        _badgeEnv.Text = $"Environment: {s.Provider} / {s.Mode}";
        _badgeSpeech.Text = DotEnvStore.HasProviderKey(s.Provider)
            ? "LLM: .env key OK"
            : "LLM: key missing";
        _badgeKnow.Text = $"Knowledge: {(s.UseLocalKnowledge ? "on" : "off")}";
        _badgeAuto.Text = OperatingSystem.IsWindows() &&
                          string.Equals(s.Safety.Profile, "power-user", StringComparison.OrdinalIgnoreCase)
            ? "Automation: Win32 (power-user)"
            : "Automation: Simulation";
        _tileEnv.Text = $"Provider {s.Provider}, model „{s.Model}“, safety {s.Safety.Profile}";
        var idx = File.Exists(AppPaths.KnowledgeIndex);
        var ch = File.Exists(AppPaths.KnowledgeChunks);
        var fts = File.Exists(AppPaths.KnowledgeFtsDb);
        var emb = File.Exists(AppPaths.KnowledgeEmbeddings);
        _tileMemory.Text =
            $"Index: {(idx ? "yes" : "no")}, Chunks: {(ch ? "yes" : "no")}, FTS5: {(fts ? "yes" : "no")}, Embeddings: {(emb ? "yes" : "no")}\n{AppPaths.DataDir}";
        if (!string.IsNullOrWhiteSpace(_tileLive.Text))
        {
            WinUiShellState.LiveContextLine = _tileLive.Text;
            return;
        }

        _tileLive.Text = "Click „refresh active app“ for foreground window + adapter.";
        WinUiShellState.LiveContextLine = _tileLive.Text;
    }

    private void OnRefreshAll()
    {
        WinUiShellState.Settings = WinUiShellState.SettingsStore.LoadOrDefault();
        DotEnvStore.Invalidate();
        WinUiShellState.TryApplySettingsToSetup?.Invoke(WinUiShellState.Settings);
        WinUiShellState.TryRefreshSetupEnvSummary?.Invoke();
        WinUiThemeApplier.Apply(WinUiShellState.Settings.UiTheme);
        RefreshHeaderBadges();
        RefreshPushToTalkKey();
        SetupPushToTalk();
        NexusShell.Log("refresh all — .env reloaded, settings applied to Setup if open.");
    }

    private void OnSaveSettings()
    {
        var s = WinUiShellState.TryGatherSettingsFromSetup?.Invoke() ?? WinUiShellState.Settings;
        WinUiShellState.SettingsStore.Save(s);
        WinUiShellState.Settings = s;
        WinUiThemeApplier.Apply(s.UiTheme);
        RefreshHeaderBadges();
        NexusShell.Log("settings.json saved.");
    }

    private void OnReindex()
    {
        KnowledgeIndexService.Rebuild();
        _ = EmbeddingRagService.RebuildIfConfiguredAsync(default);
        RefreshHeaderBadges();
        NexusShell.Log("reindex knowledge → index + chunks + FTS5 (knowledge-fts.db); embeddings in background if configured.");
    }

    private void OnRefreshActiveApp()
    {
        if (!OperatingSystem.IsWindows())
        {
            NexusShell.Log("Active window: Windows only.");
            return;
        }

        var (title, proc) = ForegroundWindowInfo.TryRead();
        var fam = OperatorAdapterRegistry.ResolveFamily(proc, title);
        _tileLive.Text = $"Active: {proc} · „{title}“ → adapter family: {fam} @ {DateTime.Now:T}";
        WinUiShellState.LiveContextLine = _tileLive.Text;
        NexusShell.Log("Live Context: " + _tileLive.Text);
    }

    private void OnExportDiagnostics()
    {
        try
        {
            Directory.CreateDirectory(AppPaths.DataDir);
            var name = Path.Combine(AppPaths.DataDir, $"diagnostics-{DateTime.Now:yyyyMMdd-HHmmss}.log");
            var header = AppBuildInfo.Summary + Environment.NewLine + new string('=', 60) + Environment.NewLine;
            var body = NexusShell.FormatRecentLogForDashboard(48, 120_000);
            File.WriteAllText(name, header + body);
            NexusShell.Log($"export diagnostics → {name}");
        }
        catch (Exception ex)
        {
            NexusShell.Log("export diagnostics failed: " + ex.Message);
        }
    }

    private void OnHandbookClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var path = Path.Combine(AppPaths.RepoRoot, "docs", "Carolus-Nexus-Benutzerhandbuch.md");
            if (!File.Exists(path))
            {
                NexusShell.Log("Handbook file missing: " + path);
                return;
            }

            Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            NexusShell.Log("Handbook: " + ex.Message);
        }
    }

    private void OnCommandPaletteAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        _ = ShowCommandPaletteAsync();
    }

    private async Task ShowShortcutHelpAsync()
    {
        var grid = new Grid
        {
            ColumnSpacing = 18,
            RowSpacing = 10,
            MaxWidth = 900
        };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var left = BuildShortcutColumn(
            ("Shell", "Ctrl+P", "Command palette"),
            ("Shell", "Ctrl+H", "Keyboard shortcuts"),
            ("Shell", "F1", "Open handbook"),
            ("Dashboard", "F5", "Refresh now"),
            ("Knowledge", "Ctrl+F", "Search"),
            ("Knowledge", "Ctrl+I", "Import"),
            ("Knowledge", "Del", "Remove selected"),
            ("Knowledge", "F5", "Reindex"),
            ("Knowledge", "Ctrl+G", "Suggest flow"));
        var right = BuildShortcutColumn(
            ("Ask", "Ctrl+Enter", "Ask now"),
            ("Ask", "Ctrl+T", "Smoke test"),
            ("Ask", "Ctrl+I", "Import audio"),
            ("Voice", "F6", "Start push-to-talk"),
            ("Voice", "Shift+F6", "Stop + ask"),
            ("Voice", "Ctrl+Esc", "Cancel recording"),
            ("Plan", "F9", "Run plan"),
            ("Plan", "Shift+F9", "Approve + run"),
            ("Plan", "F10", "Run next step"),
            ("Plan", "Esc", "Panic stop"),
            ("Flows", "Ctrl+S", "Save flow"),
            ("Flows", "F8/F9/F10", "Dry run / Run / Next step"));
        grid.Children.Add(left);
        Grid.SetColumn(right, 1);
        grid.Children.Add(right);

        var note = new TextBlock
        {
            Text = "Shortcuts wirken seitenbezogen auf die sichtbaren PowerUser-Buttons. Riskante Aktionen behalten die bestehenden Safety-Gates.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = WinUiFluentChrome.SecondaryTextBrush,
            Margin = new Thickness(0, 12, 0, 0)
        };
        WinUiFluentChrome.ApplyCaptionTextStyle(note);

        var content = new StackPanel { Spacing = 12 };
        content.Children.Add(grid);
        content.Children.Add(note);

        var dlg = new ContentDialog
        {
            Title = "Keyboard shortcuts",
            Content = content,
            CloseButtonText = "Close",
            XamlRoot = Content.XamlRoot
        };
        await dlg.ShowAsync();
    }

    private static StackPanel BuildShortcutColumn(params (string Area, string Shortcut, string Action)[] rows)
    {
        var stack = new StackPanel { Spacing = 6 };
        string? current = null;
        foreach (var row in rows)
        {
            if (!string.Equals(current, row.Area, StringComparison.Ordinal))
            {
                current = row.Area;
                stack.Children.Add(new TextBlock
                {
                    Text = current,
                    Foreground = WinUiFluentChrome.PrimaryTextBrush,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    Margin = new Thickness(0, 8, 0, 0)
                });
            }

            var line = new Grid { ColumnSpacing = 12 };
            line.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
            line.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            line.Children.Add(new TextBlock
            {
                Text = row.Shortcut,
                Foreground = WinUiFluentChrome.PrimaryTextBrush,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 12
            });
            var action = new TextBlock
            {
                Text = row.Action,
                TextWrapping = TextWrapping.Wrap,
                Foreground = WinUiFluentChrome.SecondaryTextBrush
            };
            Grid.SetColumn(action, 1);
            line.Children.Add(action);
            stack.Children.Add(line);
        }

        return stack;
    }

    private static NavigationViewItem Mk(string content, Type pageType, Symbol symbol) =>
        new()
        {
            Content = content,
            Icon = new SymbolIcon(symbol),
            Tag = pageType
        };

    private static NavigationViewItem MkFooter(string content) =>
        new()
        {
            Content = content,
            Icon = new FontIcon { Glyph = "\uE721", FontSize = 16 },
            Tag = "palette"
        };

    private void NavOnItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        if (args.InvokedItemContainer is not NavigationViewItem item || item.Tag is not { } tag)
            return;
        if (tag is string s && s == "palette")
        {
            _ = ShowCommandPaletteAsync();
            return;
        }

        if (tag is Type t)
            ShowShellPage(t);
    }

    private void ShowShellPage(Type pageType)
    {
        try
        {
            if (!_pageCache.TryGetValue(pageType, out var page))
            {
                page = Activator.CreateInstance(pageType) as Page;
                if (page == null)
                    return;
                _pageCache[pageType] = page;
            }

            _frame.Content = page;
            if (pageType == typeof(DashboardShellPage) && page is DashboardShellPage dash)
                dash.RefreshFull();
            if (pageType == typeof(KnowledgeShellPage) && page is KnowledgeShellPage know)
                know.RefreshList();
            if (pageType == typeof(RitualsShellPage) && page is RitualsShellPage rit)
                rit.ReloadLibrary();
            if (pageType == typeof(HistoryShellPage) && page is HistoryShellPage hist)
                hist.Refresh();
        }
        catch (Exception ex)
        {
            _frame.Content = new ScrollViewer
            {
                Content = new TextBlock
                {
                    Text = "Could not open page:\n" + ex,
                    TextWrapping = TextWrapping.Wrap,
                    IsTextSelectionEnabled = true
                }
            };
        }
    }

    private async Task ShowCommandPaletteAsync()
    {
        var commands = new PaletteCommand[]
        {
            new("Open Ask", "Page", () => NavigateFromPaletteAsync(typeof(AskShellPage))),
            new("Open Dashboard", "Page", () => NavigateFromPaletteAsync(typeof(DashboardShellPage))),
            new("Open Setup", "Page", () => NavigateFromPaletteAsync(typeof(SetupShellPage))),
            new("Open Knowledge", "Page", () => NavigateFromPaletteAsync(typeof(KnowledgeShellPage))),
            new("Open Operator flows", "Page", () => NavigateFromPaletteAsync(typeof(RitualsShellPage))),
            new("Open History", "Page", () => NavigateFromPaletteAsync(typeof(HistoryShellPage))),
            new("Open Diagnostics", "Page", () => NavigateFromPaletteAsync(typeof(DiagnosticsShellPage))),
            new("Open Console", "Page", () => NavigateFromPaletteAsync(typeof(ConsoleShellPage))),
            new("Open Live Context", "Page", () => NavigateFromPaletteAsync(typeof(LiveContextShellPage))),
            new("Open Experiments (Tier C)", "Page", () => NavigateFromPaletteAsync(typeof(ExperimentsShellPage))),
            new("Ask now", "Action", RunAskFromPaletteAsync),
            new("Run plan", "Action", RunPlanFromPaletteAsync),
            new("Refresh dashboard", "Action", RefreshDashboardFromPaletteAsync),
            new("Reindex knowledge", "Action", ReindexFromPaletteAsync),
            new("Refresh active app", "Action", RefreshActiveAppFromPaletteAsync),
            new("Run live inspector", "Action", RunLiveInspectorFromPaletteAsync),
            new("Keyboard shortcuts", "Help", async () => await ShowShortcutHelpAsync())
        };

        var list = new ListView
        {
            SelectionMode = ListViewSelectionMode.Single,
            ItemsSource = commands.Select(c => $"{c.Group}  ·  {c.Label}").ToList()
        };
        var dlg = new ContentDialog
        {
            Title = "Command palette",
            Content = list,
            PrimaryButtonText = "Run",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = Content.XamlRoot
        };

        list.DoubleTapped += async (_, _) =>
        {
            dlg.Hide();
            await RunPaletteSelectionAsync(list, commands);
        };

        var r = await dlg.ShowAsync();
        if (r == ContentDialogResult.Primary)
            await RunPaletteSelectionAsync(list, commands);
    }

    private Task RunPaletteSelectionAsync(ListView list, PaletteCommand[] commands)
    {
        if (list.SelectedIndex < 0)
            return Task.CompletedTask;
        return commands[list.SelectedIndex].Run();
    }

    private Task NavigateFromPaletteAsync(Type t)
    {
        ShowShellPage(t);
        foreach (NavigationViewItem? mi in _nav.MenuItems.OfType<NavigationViewItem>())
        {
            if (mi.Tag is Type mt && mt == t)
            {
                _nav.SelectedItem = mi;
                break;
            }
        }

        return Task.CompletedTask;
    }

    private async Task RunAskFromPaletteAsync()
    {
        await NavigateFromPaletteAsync(typeof(AskShellPage));
        if (_frame.Content is AskShellPage ask)
            await ask.PaletteAskNowAsync();
    }

    private async Task RunPlanFromPaletteAsync()
    {
        await NavigateFromPaletteAsync(typeof(AskShellPage));
        if (_frame.Content is AskShellPage ask)
            await ask.PaletteRunPlanAsync();
    }

    private Task RefreshDashboardFromPaletteAsync()
    {
        ShowShellPage(typeof(DashboardShellPage));
        if (_frame.Content is DashboardShellPage dash)
            dash.RefreshFull();
        NexusShell.Log("command palette: dashboard refreshed.");
        return Task.CompletedTask;
    }

    private Task ReindexFromPaletteAsync()
    {
        OnReindex();
        return Task.CompletedTask;
    }

    private Task RefreshActiveAppFromPaletteAsync()
    {
        OnRefreshActiveApp();
        return Task.CompletedTask;
    }

    private async Task RunLiveInspectorFromPaletteAsync()
    {
        await NavigateFromPaletteAsync(typeof(LiveContextShellPage));
        if (_frame.Content is LiveContextShellPage live)
            live.PaletteRunInspector();
    }

    private sealed record PaletteCommand(string Label, string Group, Func<Task> Run);
}
