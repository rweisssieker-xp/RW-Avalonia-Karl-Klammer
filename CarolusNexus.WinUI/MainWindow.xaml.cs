using System;
using System.Collections.Generic;
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
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Markup;
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
    private readonly TextBlock _activePageBadgeText = new() { FontSize = 11 };
    private readonly TextBlock _tileMemory = new() { TextWrapping = TextWrapping.Wrap, FontSize = 11 };
    private readonly TextBlock _tileLive = new() { TextWrapping = TextWrapping.Wrap, FontSize = 11 };
    private readonly TextBlock _tileEnv = new() { TextWrapping = TextWrapping.Wrap, FontSize = 11 };
    private readonly TextBlock _statusLine = new() { Text = "Bereit" };
    private readonly TextBlock _titleSubText = new()
    {
        Text = "Windows operator desktop · Karl Klammer",
        Foreground = WinUiFluentChrome.SecondaryTextBrush,
        IsTextSelectionEnabled = false,
        TextWrapping = TextWrapping.Wrap
    };
    private readonly TextBlock _titleHintText = new()
    {
        Text = "Tray · Companion · PTT · Ask · Dashboard",
        Foreground = WinUiFluentChrome.TertiaryTextBrush,
        TextWrapping = TextWrapping.Wrap
    };
    private readonly TextBlock _quickHintText = new()
    {
        Text = "Shortcuts: Ctrl+K focus · Enter run · Esc clear · Ctrl+Shift+P palette",
        Foreground = WinUiFluentChrome.TertiaryTextBrush,
        FontSize = 10,
        Margin = new Thickness(0, 6, 0, 0)
    };
    private readonly StackPanel _quickJumpRow = new()
    {
        Orientation = Orientation.Horizontal,
        Spacing = 8,
        Margin = new Thickness(0, 6, 0, 0)
    };
    private readonly StackPanel _headerActionButtons = new()
    {
        Orientation = Orientation.Horizontal,
        Spacing = 12,
        VerticalAlignment = VerticalAlignment.Center
    };
    private readonly Button _compactActionMenu = new();
    private readonly StackPanel _headerBadgesRow = new() { Orientation = Orientation.Horizontal, Spacing = 8 };
    private readonly StackPanel _headerActionsRow = new() { Orientation = Orientation.Horizontal, Spacing = 10 };
    private readonly Grid _headerTilesRow = new() { HorizontalAlignment = HorizontalAlignment.Stretch, ColumnSpacing = 12 };
    private readonly Border _quickCommandSurface = new();
    private readonly AutoSuggestBox _quickCommand = new()
    {
        PlaceholderText = "Schnellbefehl suchen oder ausführen …",
        Height = 36,
        MinWidth = 280,
        FontSize = 12
    };
    private static readonly string[] QuickCommandSuggestions =
    {
        "ask",
        "dashboard",
        "knowledge",
        "setup",
        "operator flows",
        "history",
        "diagnostics",
        "console",
        "live context",
        "experiments",
        "ask now",
        "run plan",
        "refresh dashboard",
        "reindex knowledge",
        "refresh active app",
        "run live inspector",
        "keyboard shortcuts",
        "command palette",
        "go to command palette",
        "ghost"
    };
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
    private readonly ComboBox _themeModeCombo = new()
    {
        MinWidth = 170,
        HorizontalAlignment = HorizontalAlignment.Right,
        VerticalAlignment = VerticalAlignment.Center
    };
    private readonly ComboBox _densityCombo = new()
    {
        MinWidth = 116,
        HorizontalAlignment = HorizontalAlignment.Right,
        VerticalAlignment = VerticalAlignment.Center
    };
    private readonly ToggleSwitch _reduceMotionToggle = new()
    {
        Header = "Reduce motion",
        OnContent = "Reduce",
        OffContent = "Normal",
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
    private readonly LinkedList<string> _commandPaletteHistory = new();
    private const int CommandPaletteHistoryMax = 7;
    private readonly HashSet<string> _commandPalettePinned = new(StringComparer.OrdinalIgnoreCase);
    private readonly Queue<string> _ghostActionHistory = new();
    private readonly DispatcherQueueTimer _toastTimer;
    private readonly StackPanel _toastHost = new()
    {
        HorizontalAlignment = HorizontalAlignment.Right,
        VerticalAlignment = VerticalAlignment.Top,
        Margin = new Thickness(0, 12, 12, 0)
    };

    public MainWindow()
    {
        Title = "Carolus Nexus";
        WinUiFluentChrome.ApplyMicaBackdrop(this);
        RootGrid.Background = new SolidColorBrush(Colors.Transparent);
        Content = RootGrid;
        _activePageBadgeText.Text = "Active page: Dashboard";

        _nav.MenuItems.Add(Mk("Ask", typeof(AskShellPage), Symbol.Message));
        _nav.MenuItems.Add(Mk("Dashboard", typeof(DashboardShellPage), Symbol.Home));
        _nav.MenuItems.Add(Mk("Setup", typeof(SetupShellPage), Symbol.Setting));
        _nav.MenuItems.Add(Mk("Knowledge", typeof(KnowledgeShellPage), Symbol.Bookmarks));
        _nav.MenuItems.Add(Mk("Operator flows", typeof(RitualsShellPage), Symbol.AllApps));
        _nav.MenuItems.Add(Mk("History", typeof(HistoryShellPage), Symbol.Clock));
        _nav.MenuItems.Add(Mk("Diagnostics", typeof(DiagnosticsShellPage), Symbol.Remote));
        _nav.MenuItems.Add(Mk("USP Studio", typeof(UspStudioShellPage), Symbol.World));
        _nav.MenuItems.Add(Mk("Backend Coverage", typeof(BackendCoverageShellPage), Symbol.ReportHacked));
        _nav.MenuItems.Add(Mk("Excel + AX Check", typeof(ExcelAxCheckShellPage), Symbol.Document));
        _nav.MenuItems.Add(Mk("UI Lab", typeof(UiLabShellPage), Symbol.View));
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
        RootGrid.Children.Add(_toastHost);

        _toastTimer = DispatcherQueue.GetForCurrentThread().CreateTimer();
        _toastTimer.Interval = TimeSpan.FromMilliseconds(3000);
        _toastTimer.Tick += (_, _) => ClearStatusToasts();

        foreach (var id in CommandPaletteRecentStore.Load())
            _commandPaletteHistory.AddLast(id);
        foreach (var pinned in WinUiShellState.Settings.CommandPalettePinned ?? [])
            _commandPalettePinned.Add(NormalizeCommandHistoryId(pinned));
        SetThemeOptionsFromSettings();

        RootGrid.SizeChanged += (_, e) =>
        {
            var w = e.NewSize.Width;
            if (w > 0)
            {
                RefreshHeaderDensity(w);
                RefreshLayoutBadge(w);
            }
        };

        RootGrid.Loaded += (_, _) =>
        {
            RefreshHeaderDensity(RootGrid.ActualWidth);
            RefreshLayoutBadge(RootGrid.ActualWidth);
            RefreshHeaderBadges();
        };

        _themeModeCombo.SelectionChanged += (_, _) => ThemeModeCombo_SelectionChanged(null);
        _densityCombo.SelectionChanged += OnDensitySelectionChanged;
        _reduceMotionToggle.Toggled += (_, _) =>
        {
                WinUiShellState.Settings.ReduceMotion = _reduceMotionToggle.IsOn;
                WinUiShellState.SettingsStore.Save(WinUiShellState.Settings);
                ShowStatusToast(
                    $"Reduce motion {(_reduceMotionToggle.IsOn ? "enabled" : "disabled")}.",
                    InfoBarSeverity.Informational,
                    1400);
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
        var commandPaletteAccel = new KeyboardAccelerator
        {
            Key = VirtualKey.P,
            Modifiers = Windows.System.VirtualKeyModifiers.Control | Windows.System.VirtualKeyModifiers.Shift
        };
        commandPaletteAccel.Invoked += OnCommandPaletteAccelerator;
        _nav.KeyboardAccelerators.Add(commandPaletteAccel);
        var focusQuickCommandAccel = new KeyboardAccelerator
        {
            Key = VirtualKey.K,
            Modifiers = Windows.System.VirtualKeyModifiers.Control
        };
        focusQuickCommandAccel.Invoked += (_, args) =>
        {
            args.Handled = true;
            _quickCommand.Focus(FocusState.Programmatic);
        };
        _nav.KeyboardAccelerators.Add(focusQuickCommandAccel);
        var themeCycleAccel = new KeyboardAccelerator
        {
            Key = VirtualKey.T,
            Modifiers = Windows.System.VirtualKeyModifiers.Control | Windows.System.VirtualKeyModifiers.Shift
        };
        themeCycleAccel.Invoked += (_, args) =>
        {
            args.Handled = true;
            ThemeModeCombo_SelectionChanged(null);
        };
        _nav.KeyboardAccelerators.Add(themeCycleAccel);

        RootGrid.Loaded += OnMainShellLoaded;
    }

    private static string NormalizeCommandHistoryId(string id)
    {
        return (id ?? string.Empty)
            .Trim()
            .ToLowerInvariant()
            .Replace("  ", " ", StringComparison.OrdinalIgnoreCase)
            .Replace(" ", "-")
            .Replace("/", "-");
    }

    private static string BuildCommandId(string group, string label)
    {
        return $"{group}:{label}".Trim().ToLowerInvariant().Replace(" ", "-");
    }

    private void SetThemeOptionsFromSettings()
    {
        _themeModeCombo.Items.Clear();
        _themeModeCombo.Items.Add(new ComboBoxItem { Content = "System", Tag = "system" });
        _themeModeCombo.Items.Add(new ComboBoxItem { Content = "Light", Tag = "light" });
        _themeModeCombo.Items.Add(new ComboBoxItem { Content = "Dark", Tag = "dark" });
        _themeModeCombo.Items.Add(new ComboBoxItem { Content = "HighContrast", Tag = "highContrast" });

        _densityCombo.Items.Clear();
        _densityCombo.Items.Add(new ComboBoxItem { Content = "Komfort", Tag = "comfortable" });
        _densityCombo.Items.Add(new ComboBoxItem { Content = "Kompakt", Tag = "compact" });

        SetThemeModeSelection(WinUiShellState.Settings.UiThemeMode);
        SetDensitySelection(WinUiShellState.Settings.UiDensity);

        _reduceMotionToggle.IsOn = WinUiShellState.Settings.ReduceMotion;
        AutomationProperties.SetName(_themeModeCombo, "Theme mode");
        AutomationProperties.SetName(_densityCombo, "Density");
        AutomationProperties.SetName(_reduceMotionToggle, "Reduce motion");

        void SetThemeModeSelection(string value)
        {
            var wanted = NormalizeCommandHistoryId(value);
            foreach (ComboBoxItem item in _themeModeCombo.Items)
            {
                if (string.Equals((item.Tag as string) ?? string.Empty, wanted, StringComparison.OrdinalIgnoreCase))
                {
                    _themeModeCombo.SelectedItem = item;
                    return;
                }
            }

            _themeModeCombo.SelectedIndex = 0;
        }

        void SetDensitySelection(string value)
        {
            var wanted = NormalizeCommandHistoryId(value);
            foreach (ComboBoxItem item in _densityCombo.Items)
            {
                if (string.Equals((item.Tag as string) ?? string.Empty, wanted, StringComparison.OrdinalIgnoreCase))
                {
                    _densityCombo.SelectedItem = item;
                    return;
                }
            }

            _densityCombo.SelectedIndex = 0;
        }
    }

    private void ThemeModeCombo_SelectionChanged(object? _)
    {
        if (_themeModeCombo.SelectedItem is not ComboBoxItem item)
            return;

        var requestedMode = (item.Tag as string) ?? (item.Content as string) ?? "system";
        WinUiShellState.Settings.UiThemeMode = requestedMode;
        WinUiShellState.SettingsStore.Save(WinUiShellState.Settings);
        WinUiThemeApplier.Apply(WinUiShellState.Settings.UiThemeMode, WinUiShellState.Settings.UiTheme);
        ShowStatusToast($"Theme: {requestedMode}", InfoBarSeverity.Informational, 1600);
    }

    private void OnDensitySelectionChanged(object sender, SelectionChangedEventArgs args)
    {
        var item = _densityCombo.SelectedItem as ComboBoxItem;
        var selectedDensity = (item?.Tag as string) ?? (item?.Content as string) ?? "comfortable";
        WinUiShellState.Settings.UiDensity = selectedDensity;
        WinUiShellState.SettingsStore.Save(WinUiShellState.Settings);
        RefreshHeaderDensity(RootGrid.ActualWidth);
        ShowStatusToast($"Density: {selectedDensity}", InfoBarSeverity.Informational, 1200);
    }

    private static string? ToPlainText(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private void ShowStatusToast(
        string message,
        InfoBarSeverity severity = InfoBarSeverity.Informational,
        int autoCloseMs = 3000,
        Func<Task>? undoAction = null,
        string undoLabel = "Rückgängig")
    {
        if (_toastHost is null)
            return;

        void RemoveToast(Border target)
        {
            _toastHost.Children.Remove(target);
            if (_toastHost.Children.Count == 0 && _toastTimer.IsRunning)
                _toastTimer.Stop();
        }

        var toast = new Border
        {
            CornerRadius = new CornerRadius(10),
            BorderBrush = WinUiFluentChrome.SeparatorBrush,
            BorderThickness = new Thickness(1),
            Background = WinUiFluentChrome.LayerChromeBackground,
            Margin = new Thickness(0, 0, 0, 8),
            Padding = new Thickness(12)
        };

        toast.Tag = DateTime.UtcNow.AddMilliseconds(Math.Max(autoCloseMs, 1200));
        WinUiFluentChrome.ApplyCardElevation(toast, 6f);
        toast.Child = BuildToastContent(
            message,
            severity,
            undoAction,
            undoLabel,
            () => RemoveToast(toast));

        if (_toastHost.Children.Count > 0)
            _toastHost.Children.Insert(0, toast);
        else
            _toastHost.Children.Add(toast);

        if (!_toastTimer.IsRunning)
            _toastTimer.Start();

        while (_toastHost.Children.Count > 4)
            _toastHost.Children.RemoveAt(_toastHost.Children.Count - 1);

        UIElement BuildToastContent(string text, InfoBarSeverity sev, Func<Task>? undo, string label, Action onClose)
        {
            var bar = new Grid { ColumnSpacing = 10 };
            bar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            bar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            bar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var messageBlock = new TextBlock
            {
                Text = text,
                Foreground = WinUiFluentChrome.PrimaryTextBrush,
                TextWrapping = TextWrapping.Wrap,
                FontSize = 12,
                Margin = new Thickness(0, 0, 0, 0)
            };
            var closeButton = new Button
            {
                Content = "×",
                FontSize = 16,
                MinWidth = 20,
                Padding = new Thickness(4, 0, 4, 0)
            };
            WinUiFluentChrome.StyleActionButton(closeButton, compact: true);
            closeButton.Click += (_, _) => onClose();

            Grid.SetColumn(closeButton, 1);
            bar.Children.Add(messageBlock);
            bar.Children.Add(closeButton);

            if (undo is not null)
            {
                var undoButton = new Button { Content = label };
                WinUiFluentChrome.StyleActionButton(undoButton, compact: true);
                undoButton.Click += async (_, _) =>
                {
                    onClose();
                    await undo();
                };
                Grid.SetColumn(undoButton, 2);
                bar.Children.Add(undoButton);
                bar.Background = sev switch
                {
                    InfoBarSeverity.Warning or InfoBarSeverity.Error =>
                        WinUiFluentChrome.TryThemeBrush("AccentFillColorWarningBrush") ?? WinUiFluentChrome.CardSurfaceBackground,
                    InfoBarSeverity.Success =>
                        WinUiFluentChrome.TryThemeBrush("SystemFillColorSuccessBrush") ?? WinUiFluentChrome.CardSurfaceBackground,
                    _ => WinUiFluentChrome.CardSurfaceBackground
                };
            }

            return bar;
        }
    }

    private void ClearStatusToasts()
    {
        var now = DateTime.UtcNow;
        var toRemove = _toastHost.Children
            .Where(c => c is FrameworkElement fe && fe.Tag is DateTime expiry && expiry <= now)
            .ToList();
        foreach (var child in toRemove)
            _toastHost.Children.Remove(child);

        if (_toastHost.Children.Count == 0 && _toastTimer.IsRunning)
            _toastTimer.Stop();
    }

    private static int FuzzyScore(string value, string query)
    {
        if (string.IsNullOrWhiteSpace(value))
            return query.Length + 1;
        if (string.IsNullOrWhiteSpace(query))
            return 0;
        var a = value.ToLowerInvariant();
        var b = query.ToLowerInvariant();
        if (a.Contains(b, StringComparison.Ordinal))
            return Math.Max(0, b.Length - a.IndexOf(b, StringComparison.Ordinal));

        var m = a.Length + 1;
        var n = b.Length + 1;
        var dp = new int[m * n];
        for (var i = 0; i < m; i++) dp[i * n] = i;
        for (var j = 0; j < n; j++) dp[j] = j;
        for (var i = 1; i < m; i++)
        {
            for (var j = 1; j < n; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                var insertion = dp[(i - 1) * n + j] + 1;
                var deletion = dp[i * n + (j - 1)] + 1;
                var substitution = dp[(i - 1) * n + (j - 1)] + cost;
                var best = Math.Min(Math.Min(insertion, deletion), substitution);
                if (i > 1 && j > 1 && a[i - 1] == b[j - 2] && a[i - 2] == b[j - 1])
                    best = Math.Min(best, dp[(i - 2) * n + (j - 2)] + 1);
                dp[i * n + j] = best;
            }
        }

        return dp[(m - 1) * n + (n - 1)] + Math.Abs(a.Length - b.Length);
    }

    private UIElement BuildHeaderChrome()
    {
        var border = new Border
        {
            Padding = new Thickness(20, 16, 20, 16),
            BorderThickness = new Thickness(0, 0, 0, 1),
            BorderBrush = WinUiFluentChrome.SeparatorBrush,
            Background = WinUiFluentChrome.HeaderChromeBackground
        };
        WinUiFluentChrome.ApplyCardElevation(border, 4f);

        var stack = new StackPanel { Spacing = 16 };

        var titleRow = new Grid();
        titleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        titleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
        titleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var titleBlock = new StackPanel { Spacing = 6 };
        var titleMain = new TextBlock { Text = "Carolus Nexus", TextWrapping = TextWrapping.Wrap };
        WinUiFluentChrome.ApplyTitleTextStyle(titleMain);
        titleBlock.Children.Add(titleMain);
        WinUiFluentChrome.ApplySubtitleTextStyle(_titleSubText);
        titleBlock.Children.Add(_titleSubText);
        WinUiFluentChrome.ApplyCaptionTextStyle(_titleHintText);
        var statusRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        statusRow.Children.Add(_globalStatusBusyBar);
        _statusLine.Foreground = WinUiFluentChrome.SecondaryTextBrush;
        WinUiFluentChrome.ApplyCaptionTextStyle(_statusLine);
        statusRow.Children.Add(_statusLine);
        titleBlock.Children.Add(statusRow);
        Grid.SetColumn(titleBlock, 0);
        titleRow.Children.Add(titleBlock);

        var quickGrid = new Grid();
        quickGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        quickGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        quickGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        quickGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var quickLabel = new TextBlock
        {
            Text = "Quick command",
            Foreground = WinUiFluentChrome.SecondaryTextBrush,
            FontSize = 11
        };
        Grid.SetRow(quickLabel, 0);
        quickGrid.Children.Add(quickLabel);
        _quickCommand.QuerySubmitted += (sender, e) =>
        {
            if (string.IsNullOrWhiteSpace(e.QueryText))
            {
                _ = ShowCommandPaletteAsync();
                return;
            }
            _ = ExecuteQuickCommandAsync(e.QueryText);
        };
        _quickCommand.SuggestionChosen += (sender, e) =>
        {
            if (e.SelectedItem is string selected)
                _ = ExecuteQuickCommandAsync(selected);
        };
        _quickCommand.TextChanged += (_, e) =>
        {
            if (e.Reason != AutoSuggestionBoxTextChangeReason.UserInput)
                return;
            var q = _quickCommand.Text?.Trim();
            if (string.IsNullOrWhiteSpace(q))
            {
                _quickCommand.ItemsSource = QuickCommandSuggestions;
                return;
            }

            _quickCommand.ItemsSource = QuickCommandSuggestions
                .Select(x => new { Text = x, Score = FuzzyScore(x, q) })
                .Where(x => x.Score < 10)
                .OrderBy(x => x.Score)
                .Select(x => x.Text)
                .ToArray();
        };
        _quickCommand.KeyDown += (_, e) =>
        {
            if (e.Key == VirtualKey.Escape)
            {
                _quickCommand.Text = string.Empty;
                e.Handled = true;
            }
        };
        _quickCommand.ItemsSource = QuickCommandSuggestions;
        _quickCommand.Margin = new Thickness(0, 4, 0, 0);
        quickGrid.Children.Add(_quickCommand);
        Grid.SetRow(_quickCommand, 1);
        WinUiFluentChrome.ApplyCaptionTextStyle(_quickHintText);
        Grid.SetRow(_quickHintText, 2);
        quickGrid.Children.Add(_quickHintText);

        Button MakeQuickChip(string label, string glyph, string command)
        {
            var chip = WinUiFluentChrome.MakeCommandChip(label, glyph, (_, _) => _ = ExecuteQuickCommandAsync(command));
            chip.Padding = new Thickness(10, 6, 10, 6);
            chip.FontSize = 11;
            return chip;
        }

        _quickJumpRow.Children.Add(MakeQuickChip("Ask", "\uE8A7", "ask"));
        _quickJumpRow.Children.Add(MakeQuickChip("Dashboard", "\uE8F0", "dashboard"));
        _quickJumpRow.Children.Add(MakeQuickChip("Knowledge", "\uE8C9", "knowledge"));
        _quickJumpRow.Children.Add(MakeQuickChip("Experiments", "\uE9A9", "experiments"));
        _quickJumpRow.Children.Add(MakeQuickChip("Settings", "\uE115", "setup"));
        Grid.SetRow(_quickJumpRow, 3);
        quickGrid.Children.Add(_quickJumpRow);

        quickGrid.HorizontalAlignment = HorizontalAlignment.Stretch;
        quickGrid.VerticalAlignment = VerticalAlignment.Center;
        quickGrid.Margin = new Thickness(16, 0, 16, 0);
        _quickCommandSurface.Child = quickGrid;
        _quickCommandSurface.Background = WinUiFluentChrome.CardSurfaceBackground;
        _quickCommandSurface.BorderBrush = WinUiFluentChrome.CardBorderBrush;
        _quickCommandSurface.BorderThickness = new Thickness(1);
        _quickCommandSurface.CornerRadius = new CornerRadius(12);
        _quickCommandSurface.Padding = new Thickness(10);
        WinUiFluentChrome.ApplyCardElevation(_quickCommandSurface, 6f);
        Grid.SetColumn(_quickCommandSurface, 1);
        titleRow.Children.Add(_quickCommandSurface);

        _headerActionButtons.Children.Add(_companionToggle);
        _headerActionButtons.Children.Add(_themeModeCombo);
        _headerActionButtons.Children.Add(_densityCombo);
        _headerActionButtons.Children.Add(_reduceMotionToggle);
        var quickCommandRun = MkHeaderIconButton("Run", "\uE8A7", compact: true, accent: true);
        ToolTipService.SetToolTip(quickCommandRun, "Command palette / quick run");
        quickCommandRun.Click += async (_, _) =>
        {
            await ShowCommandPaletteAsync();
            _quickCommand.Text = "";
        };
        _headerActionButtons.Children.Add(quickCommandRun);
        var closeBtn = MkHeaderIconButton("Minimize to tray", "\uE8BB", compact: true, accent: false, iconOnly: false);
        closeBtn.Click += OnHeaderCloseClick;
        _headerActionButtons.Children.Add(closeBtn);
        var handbookBtn = MkHeaderIconButton(string.Empty, "\uE8A5", compact: true);
        ToolTipService.SetToolTip(handbookBtn, "Open handbook");
        handbookBtn.Click += OnHandbookClick;
        _headerActionButtons.Children.Add(handbookBtn);
        var shortcuts = MkHeaderIconButton(string.Empty, "\uE765", compact: true);
        ToolTipService.SetToolTip(shortcuts, "Keyboard shortcuts");
        shortcuts.Click += async (_, _) => await ShowShortcutHelpAsync();
        _headerActionButtons.Children.Add(shortcuts);
        var ghost = MkHeaderIconButton("Ghost", "\uE752", compact: true);
        WinUiFluentChrome.AddShortcut(ghost, VirtualKey.G, Windows.System.VirtualKeyModifiers.Control, "Ctrl+G");
        ghost.Click += (_, _) => ToggleGhostPanel();
        _headerActionButtons.Children.Add(ghost);
        _compactActionMenu.Content = new FontIcon { Glyph = "\uE700", FontFamily = new FontFamily("Segoe Fluent Icons") };
        _compactActionMenu.VerticalAlignment = VerticalAlignment.Center;
        WinUiFluentChrome.StyleActionButton(_compactActionMenu, compact: true);
        ToolTipService.SetToolTip(_compactActionMenu, "Weitere Aktionen");
        var compactActions = new MenuFlyout();
        var mRefreshAll = new MenuFlyoutItem { Text = "Refresh all" };
        mRefreshAll.Click += (_, _) => OnRefreshAll();
        var mSave = new MenuFlyoutItem { Text = "Save settings" };
        mSave.Click += (_, _) => OnSaveSettings();
        var mReindex = new MenuFlyoutItem { Text = "Reindex knowledge" };
        mReindex.Click += (_, _) => OnReindex();
        var mRefreshApp = new MenuFlyoutItem { Text = "Refresh active app" };
        mRefreshApp.Click += (_, _) => OnRefreshActiveApp();
        var mExport = new MenuFlyoutItem { Text = "Export diagnostics" };
        mExport.Click += (_, _) => OnExportDiagnostics();
        compactActions.Items.Add(mRefreshAll);
        compactActions.Items.Add(mSave);
        compactActions.Items.Add(mReindex);
        compactActions.Items.Add(mRefreshApp);
        compactActions.Items.Add(mExport);
        _compactActionMenu.Flyout = compactActions;
        _headerActionButtons.Children.Add(_compactActionMenu);
        Grid.SetColumn(_headerActionButtons, 2);
        titleRow.Children.Add(_headerActionButtons);
        titleBlock.Children.Add(_titleHintText);
        stack.Children.Add(titleRow);

        _headerBadgesRow.Children.Clear();
        var activePagePill = WinUiFluentChrome.CreateStatusPill(_activePageBadgeText.Text ?? "Active page: Dashboard");
        activePagePill.Child = _activePageBadgeText;
        activePagePill.Opacity = 0.95;
        _headerBadgesRow.Children.Add(activePagePill);
        foreach (var tb in new[] { _badgeLayout, _badgeEnv, _badgeSpeech, _badgeAuto, _badgeKnow })
        {
            tb.Foreground = WinUiFluentChrome.PrimaryTextBrush;
            var pill = WinUiFluentChrome.CreateStatusPill(tb.Text ?? string.Empty);
            pill.Margin = new Thickness(0, 0, 0, 0);
            pill.Child = tb;
            _headerBadgesRow.Children.Add(pill);
        }

        stack.Children.Add(_headerBadgesRow);

        _headerTilesRow.Children.Clear();
        _headerTilesRow.ColumnSpacing = 12;
        _headerTilesRow.HorizontalAlignment = HorizontalAlignment.Stretch;
        var tiles = new Grid { HorizontalAlignment = HorizontalAlignment.Stretch, ColumnSpacing = 12 };
        tiles.RowDefinitions.Clear();
        tiles.Children.Clear();
        tiles.ColumnDefinitions.Clear();
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
        _headerTilesRow.Children.Clear();
        _headerTilesRow.Children.Add(tiles);
        stack.Children.Add(_headerTilesRow);

        _headerActionsRow.Children.Clear();
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
        _headerActionsRow.Children.Add(bRefresh);
        _headerActionsRow.Children.Add(bSave);
        _headerActionsRow.Children.Add(bReindex);
        _headerActionsRow.Children.Add(bApp);
        _headerActionsRow.Children.Add(bExport);
        stack.Children.Add(_headerActionsRow);

        border.Child = stack;
        return border;
    }

    private static Button MkHeaderActionButton(string label, bool accent = false, bool compact = false, string? icon = null)
    {
        var b = new Button { Margin = new Thickness(0, 0, 0, 0) };
        WinUiFluentChrome.StyleActionButton(b, accent, compact);
        if (string.IsNullOrWhiteSpace(icon))
            b.Content = label;
        else
            WinUiFluentChrome.SetIconButton(b, label, icon);
        return b;
    }

    private static Button MkHeaderIconButton(string label, string? icon, bool compact, bool accent = false, bool iconOnly = true)
    {
        var b = new Button { Margin = new Thickness(0, 0, 0, 0) };
        WinUiFluentChrome.StyleActionButton(b, accent, compact);
        if (!string.IsNullOrWhiteSpace(icon))
        {
            if (iconOnly)
            {
                b.Content = new FontIcon
                {
                    Glyph = icon,
                    FontFamily = new FontFamily("Segoe Fluent Icons"),
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
                };
                ToolTipService.SetToolTip(b, string.IsNullOrWhiteSpace(label) ? "Action" : label);
            }
            else
            {
                WinUiFluentChrome.SetIconButton(b, label, icon);
            }
        }
        else
        {
            b.Content = label;
        }

        return b;
    }

        private async Task ExecuteQuickCommandAsync(string? text)
        {
        var query = text?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(query))
        {
            await ShowCommandPaletteAsync();
            return;
        }

        static bool Has(string input, string term) => input.Contains(term, StringComparison.OrdinalIgnoreCase);
        if (Has(query, "ask now"))
        {
            await RunAskFromPaletteAsync();
            RecordPaletteCommandInHistory(BuildCommandId("Action", "Ask now"));
        }
        else if (Has(query, "run plan"))
        {
            await RunPlanFromPaletteAsync();
            RecordPaletteCommandInHistory(BuildCommandId("Action", "Run plan"));
        }
        else if (Has(query, "refresh dashboard") || Has(query, "refresh overview"))
        {
            await RefreshDashboardFromPaletteAsync();
            RecordPaletteCommandInHistory(BuildCommandId("Action", "Refresh dashboard"));
        }
        else if (Has(query, "reindex") || Has(query, "reindex knowledge"))
        {
            await ReindexFromPaletteAsync();
            RecordPaletteCommandInHistory(BuildCommandId("Action", "Reindex knowledge"));
        }
        else if (Has(query, "refresh active app") || Has(query, "active app"))
        {
            await RefreshActiveAppFromPaletteAsync();
            RecordPaletteCommandInHistory(BuildCommandId("Action", "Refresh active app"));
        }
        else if (Has(query, "live inspector") || Has(query, "inspector"))
        {
            await RunLiveInspectorFromPaletteAsync();
            RecordPaletteCommandInHistory(BuildCommandId("Action", "Run live inspector"));
        }
        else if (Has(query, "shortcuts") || Has(query, "help"))
        {
            await ShowShortcutHelpAsync();
            RecordPaletteCommandInHistory(BuildCommandId("Help", "Keyboard shortcuts"));
        }
        else if (Has(query, "command palette") || Has(query, "palette"))
        {
            await ShowCommandPaletteAsync();
        }
        else if (Has(query, "ghost"))
        {
            ToggleGhostPanel();
            _statusLine.Text = "Ghost panel toggled.";
            RecordPaletteCommandInHistory(BuildCommandId("Action", "Ghost"));
        }
        else if (Has(query, "ask"))
            await NavigateFromPaletteAsync(typeof(AskShellPage));
        else if (Has(query, "dashboard"))
            await NavigateFromPaletteAsync(typeof(DashboardShellPage));
        else if (Has(query, "setup"))
            await NavigateFromPaletteAsync(typeof(SetupShellPage));
        else if (Has(query, "knowledge"))
            await NavigateFromPaletteAsync(typeof(KnowledgeShellPage));
        else if (Has(query, "operator") || Has(query, "flow") || Has(query, "ritual"))
            await NavigateFromPaletteAsync(typeof(RitualsShellPage));
        else if (Has(query, "history"))
            await NavigateFromPaletteAsync(typeof(HistoryShellPage));
        else if (Has(query, "diagn"))
            await NavigateFromPaletteAsync(typeof(DiagnosticsShellPage));
        else if (Has(query, "console") || Has(query, "cli"))
            await NavigateFromPaletteAsync(typeof(ConsoleShellPage));
        else if (Has(query, "live"))
            await NavigateFromPaletteAsync(typeof(LiveContextShellPage));
        else if (Has(query, "tier") || Has(query, "experiment") || Has(query, "c"))
            await NavigateFromPaletteAsync(typeof(ExperimentsShellPage));
        else if (Has(query, "run") || Has(query, "ask now") || Has(query, "reindex") || Has(query, "refresh"))
            await ShowCommandPaletteAsync();
        else
            await ShowCommandPaletteAsync();

        _quickCommand.Text = "";
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
        var close = MkHeaderActionButton("Close", compact: true, icon: "\uE8BB");
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

    private void RefreshHeaderDensity(double width)
    {
        if (width <= 0)
            return;

        var isNarrow = ResponsiveLayout.GetBand(width) == ResponsiveBand.Narrow;
        var isCompact = ResponsiveLayout.GetBand(width) == ResponsiveBand.Medium;

        _quickCommandSurface.CornerRadius = isNarrow ? new CornerRadius(10) : new CornerRadius(12);
        _quickCommandSurface.Padding = isNarrow ? new Thickness(8) : isCompact ? new Thickness(10) : new Thickness(12);

        _titleSubText.Visibility = isNarrow ? Microsoft.UI.Xaml.Visibility.Collapsed : Microsoft.UI.Xaml.Visibility.Visible;
        _titleHintText.Visibility = isNarrow ? Microsoft.UI.Xaml.Visibility.Collapsed : Microsoft.UI.Xaml.Visibility.Visible;
        _quickHintText.Visibility = isNarrow ? Microsoft.UI.Xaml.Visibility.Collapsed : Microsoft.UI.Xaml.Visibility.Visible;
        _quickJumpRow.Visibility = isCompact || isNarrow ? Microsoft.UI.Xaml.Visibility.Collapsed : Microsoft.UI.Xaml.Visibility.Visible;
        _headerTilesRow.Visibility = isNarrow ? Microsoft.UI.Xaml.Visibility.Collapsed : Microsoft.UI.Xaml.Visibility.Visible;
        _headerBadgesRow.Visibility = isNarrow ? Microsoft.UI.Xaml.Visibility.Collapsed : Microsoft.UI.Xaml.Visibility.Visible;
        _headerActionsRow.Visibility = isNarrow ? Microsoft.UI.Xaml.Visibility.Collapsed : Microsoft.UI.Xaml.Visibility.Visible;
        _compactActionMenu.Visibility = isNarrow ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;
        _quickCommand.MinWidth = isNarrow ? 240 : isCompact ? 260 : 300;
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
        WinUiThemeApplier.Apply(WinUiShellState.Settings.UiThemeMode, WinUiShellState.Settings.UiTheme);
        SetThemeOptionsFromSettings();
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
        WinUiThemeApplier.Apply(s.UiThemeMode, s.UiTheme);
        SetThemeOptionsFromSettings();
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
            ("Shell", "Ctrl+K", "Focus quick command"),
            ("Shell", "Ctrl+Shift+T", "Toggle light / dark"),
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

    private void RecordPaletteCommandInHistory(string commandId)
    {
        commandId = NormalizeCommandHistoryId(commandId);
        if (string.IsNullOrWhiteSpace(commandId))
            return;

        while (true)
        {
            var existing = _commandPaletteHistory.FirstOrDefault(c => string.Equals(c, commandId, StringComparison.OrdinalIgnoreCase));
            if (existing is null)
                break;
            _commandPaletteHistory.Remove(existing);
        }

        _commandPaletteHistory.AddFirst(commandId);
        CommandPaletteRecentStore.Touch(commandId);
        while (_commandPaletteHistory.Count > CommandPaletteHistoryMax)
            _commandPaletteHistory.RemoveLast();
    }

    private void ShowShellPage(Type pageType)
    {
        try
        {
            _activePageBadgeText.Text = $"Active page: {GetFriendlyPageTitle(pageType)}";
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

    private static string GetFriendlyPageTitle(Type pageType) => pageType.Name switch
    {
        "AskShellPage" => "Ask",
        "DashboardShellPage" => "Dashboard",
        "SetupShellPage" => "Setup",
        "KnowledgeShellPage" => "Knowledge",
        "RitualsShellPage" => "Operator flows",
        "HistoryShellPage" => "History",
        "DiagnosticsShellPage" => "Diagnostics",
        "UspStudioShellPage" => "USP Studio",
        "BackendCoverageShellPage" => "Backend Coverage",
        "ExcelAxCheckShellPage" => "Excel + AX Check",
        "UiLabShellPage" => "UI Lab",
        "ConsoleShellPage" => "Console",
        "LiveContextShellPage" => "Live Context",
        "ExperimentsShellPage" => "Experiments",
        _ => "Page"
    };

    private async Task ShowCommandPaletteAsync()
    {
        Func<string, string, string?, string, Func<Task>, PaletteCommand> command = (group, label, shortcut, glyph, run) =>
        {
            var id = BuildCommandId(group, label);
            return new(id, label, group, shortcut, glyph, WrapPaletteRunForHistory(id, run, label));
        };

        var commands = new PaletteCommand[]
        {
            command("Page", "Open Ask", "Ctrl+1", "\uE8A7", () => NavigateFromPaletteAsync(typeof(AskShellPage))),
            command("Page", "Open Dashboard", "Ctrl+2", "\uE8F0", () => NavigateFromPaletteAsync(typeof(DashboardShellPage))),
            command("Page", "Open Setup", null, "\uE115", () => NavigateFromPaletteAsync(typeof(SetupShellPage))),
            command("Page", "Open Knowledge", "Ctrl+K", "\uE8C9", () => NavigateFromPaletteAsync(typeof(KnowledgeShellPage))),
            command("Page", "Open Operator flows", null, "\uE8FD", () => NavigateFromPaletteAsync(typeof(RitualsShellPage))),
            command("Page", "Open History", null, "\uE8C0", () => NavigateFromPaletteAsync(typeof(HistoryShellPage))),
            command("Page", "Open Diagnostics", null, "\uE8C8", () => NavigateFromPaletteAsync(typeof(DiagnosticsShellPage))),
            command("Page", "Open Backend Coverage", null, "\uE9F9", () => NavigateFromPaletteAsync(typeof(BackendCoverageShellPage))),
            command("Page", "Open Excel + AX Check", null, "\uE8A5", () => NavigateFromPaletteAsync(typeof(ExcelAxCheckShellPage))),
            command("Page", "Open UI Lab", null, "\uE890", () => NavigateFromPaletteAsync(typeof(UiLabShellPage))),
            command("Page", "Open Console", "Ctrl+`", "\uE756", () => NavigateFromPaletteAsync(typeof(ConsoleShellPage))),
            command("Page", "Open Live Context", null, "\uE8A7", () => NavigateFromPaletteAsync(typeof(LiveContextShellPage))),
            command("Page", "Open Experiments (Tier C)", null, "\uE9A9", () => NavigateFromPaletteAsync(typeof(ExperimentsShellPage))),
            command("Action", "Ask now", "Ctrl+Enter", "\uE8F4", RunAskFromPaletteAsync),
            command("Action", "Run plan", "F9", "\uE8E5", RunPlanFromPaletteAsync),
            command("Action", "Refresh dashboard", "F5", "\uE72C", RefreshDashboardFromPaletteAsync),
            command("Action", "Reindex knowledge", "Ctrl+R", "\uE8FB", ReindexFromPaletteAsync),
            command("Action", "Refresh active app", null, "\uE8B3", RefreshActiveAppFromPaletteAsync),
            command("Action", "Run live inspector", null, "\uE8A5", RunLiveInspectorFromPaletteAsync),
            command("Help", "Keyboard shortcuts", "Ctrl+H", "\uE82D", async () => await ShowShortcutHelpAsync())
        };

        var commandById = commands.ToDictionary(c => NormalizeCommandHistoryId(c.Id), c => c, StringComparer.OrdinalIgnoreCase);

        Func<PaletteCommand, string, bool> Matches = (entry, queryText) =>
        {
            if (string.IsNullOrWhiteSpace(queryText))
                return true;
            var labelScore = FuzzyScore(entry.Label, queryText);
            var groupScore = FuzzyScore(entry.Group, queryText);
            var shortcutScore = string.IsNullOrWhiteSpace(entry.Shortcut)
                ? int.MaxValue
                : FuzzyScore(entry.Shortcut!, queryText);
            return labelScore < 12 || groupScore < 12 || shortcutScore < 12;
        };

        int RankForQuery(PaletteCommand c, string queryText)
        {
            if (string.IsNullOrWhiteSpace(queryText))
                return 0;
            var labelMatch = FuzzyScore(c.Label, queryText);
            var groupMatch = FuzzyScore(c.Group, queryText);
            var shortcutMatch = string.IsNullOrWhiteSpace(c.Shortcut)
                ? int.MaxValue
                : FuzzyScore(c.Shortcut!, queryText);
            return Math.Min(labelMatch, Math.Min(groupMatch, shortcutMatch));
        }

        IEnumerable<(PaletteCommand Command, int Score, int Recency)> GetRecentHistory(string? query, string? groupFilter)
        {
            var queryText = (query ?? string.Empty).Trim();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var recency = 0;
            foreach (var historyId in _commandPaletteHistory)
            {
                recency++;
                var normalized = NormalizeCommandHistoryId(historyId);
                if (!seen.Add(normalized))
                    continue;
                if (!commandById.TryGetValue(normalized, out var cmd))
                    continue;
                if (!string.IsNullOrWhiteSpace(groupFilter) && !string.Equals(groupFilter, "Recent", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(cmd.Group, groupFilter, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (!Matches(cmd, queryText))
                    continue;

                yield return (cmd, RankForQuery(cmd, queryText), recency);
            }
        }

        Func<Task> WrapPaletteRunForHistory(string id, Func<Task> run, string label)
        {
            async Task Wrapped()
            {
                await run();
                RecordPaletteCommandInHistory(id);
                ShowStatusToast($"Ausgeführt: {label}", InfoBarSeverity.Success, 1200);
            }

            return Wrapped;
        }

        var list = new ListView
        {
            SelectionMode = ListViewSelectionMode.Single,
            IsItemClickEnabled = true,
            MinWidth = 780,
            MinHeight = 280,
            MaxHeight = 420,
            Background = new SolidColorBrush(Colors.Transparent),
            BorderThickness = new Thickness(0),
            Padding = new Thickness(2),
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Top,
            IsTabStop = false,
            ItemContainerStyle = new Style(typeof(ListViewItem))
            {
                Setters =
                {
                    new Setter(FrameworkElement.MarginProperty, new Thickness(0)),
                    new Setter(Control.PaddingProperty, new Thickness(2)),
                    new Setter(Control.BackgroundProperty, new SolidColorBrush(Colors.Transparent)),
                    new Setter(Control.HorizontalAlignmentProperty, HorizontalAlignment.Stretch),
                    new Setter(ListViewItem.BorderThicknessProperty, new Thickness(0)),
                    new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch)
                }
            }
        };

        string? activeGroupFilter = null;

        PaletteRow[] BuildRows(string? query, string? groupFilter)
        {
            var queryText = (query ?? string.Empty).Trim();
            var normalizedGroupFilter = string.IsNullOrWhiteSpace(groupFilter) ? null : NormalizeCommandHistoryId(groupFilter);
            var includeRecent = string.IsNullOrWhiteSpace(normalizedGroupFilter) || string.Equals(normalizedGroupFilter, "recent", StringComparison.OrdinalIgnoreCase);
            var includePinned = string.IsNullOrWhiteSpace(normalizedGroupFilter) || string.Equals(normalizedGroupFilter, "pinned", StringComparison.OrdinalIgnoreCase);
            var recentSet = includeRecent
                ? GetRecentHistory(queryText, groupFilter)
                    .Select(x => x.Command.Id)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase)
                : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var source = commands.AsEnumerable();
            if (!string.IsNullOrWhiteSpace(normalizedGroupFilter) &&
                !string.Equals(normalizedGroupFilter, "recent", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(normalizedGroupFilter, "pinned", StringComparison.OrdinalIgnoreCase))
            {
                source = source.Where(c => string.Equals(c.Group, groupFilter, StringComparison.OrdinalIgnoreCase));
            }

            var scored = source
                .Where(c => Matches(c, queryText))
                .Select(c => new
                {
                    Command = c,
                    Score = RankForQuery(c, queryText),
                    IsPinned = _commandPalettePinned.Contains(NormalizeCommandHistoryId(c.Id))
                });

            if (string.IsNullOrWhiteSpace(queryText))
                scored = scored.OrderBy(x => x.IsPinned ? 0 : 1).ThenBy(x => x.Command.Group).ThenBy(x => x.Command.Label);
            else
                scored = scored.OrderBy(x => x.Score).ThenBy(x => x.IsPinned ? 0 : 1).ThenBy(x => x.Command.Label);

            var rowsOut = new List<PaletteRow>();
            if (includePinned)
            {
                var pinnedRows = commands
                    .Where(c => _commandPalettePinned.Contains(NormalizeCommandHistoryId(c.Id)))
                    .Where(c => Matches(c, queryText))
                    .Where(c => string.IsNullOrWhiteSpace(groupFilter) ||
                                string.Equals(groupFilter, "Pinned", StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(c.Group, groupFilter, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(c => c.Group)
                    .ThenBy(c => c.Label)
                    .ToArray();
                if (pinnedRows.Length > 0)
                {
                    rowsOut.Add(new PaletteRow("Pinned", "Pinned", "Persistente Schnellzugriffe", null, "\uE8A5", null, Visibility.Visible, Visibility.Collapsed));
                    for (var index = 0; index < pinnedRows.Length; index++)
                    {
                        var cmd = pinnedRows[index];
                        rowsOut.Add(ToRow(cmd, "Pinned", index == 0, $"Pin aktiv · Shortcut: {cmd.Shortcut ?? "n/a"}"));
                    }
                }
            }

            if (includeRecent)
            {
                var recentRows = GetRecentHistory(queryText, groupFilter)
                    .OrderBy(x => x.Score)
                    .ThenBy(x => x.Recency)
                    .Take(CommandPaletteHistoryMax)
                    .ToArray();
                if (recentRows.Length > 0)
                {
                    rowsOut.Add(new PaletteRow("Recently used", "Recent", "Auto-vorherige Ausführung", null, "\uE81C", null, Visibility.Visible, Visibility.Collapsed));
                    for (var index = 0; index < recentRows.Length; index++)
                    {
                        var entry = recentRows[index];
                        var cmd = entry.Command;
                        rowsOut.Add(ToRow(cmd, "Recent", index == 0, $"Zuletzt genutzt · Lauf {index + 1}"));
                    }
                }
            }

            IEnumerable<PaletteCommand> remaining = scored.Select(x => x.Command)
                .Where(c => !(includePinned && _commandPalettePinned.Contains(NormalizeCommandHistoryId(c.Id)))
                            && !recentSet.Contains(c.Id));

            var grouped = remaining
                .GroupBy(r => r.Group)
                .OrderBy(g => g.Key switch
                {
                    "Page" => 0,
                    "Action" => 1,
                    "Help" => 2,
                    _ => 99
                })
                .ThenBy(g => g.Key);
            foreach (var g in grouped)
            {
                rowsOut.Add(new PaletteRow(
                    $"{g.Key}  —  quick actions",
                    g.Key,
                    null,
                    null,
                    "\uE7C3",
                    null,
                    Visibility.Visible,
                    Visibility.Collapsed));

                var entries = g.OrderBy(r => r.Label).ToArray();
                for (var index = 0; index < entries.Length; index++)
                {
                    var command = entries[index];
                    rowsOut.Add(ToRow(command, g.Key, index == 0, $"{command.Group}  ·  {command.Label}"));
                }
            }

            if (rowsOut.Count == 0)
            {
                if (string.IsNullOrWhiteSpace(queryText))
                {
                    return new[]
                    {
                        new PaletteRow(
                            "Noch nichts gefunden",
                            null,
                            "Tippe z. B. „dashboard“, „ask now“, „Ctrl+P“ oder „open“.",
                            null,
                            null,
                            null,
                            Visibility.Visible,
                            Visibility.Collapsed)
                    };
                }

                return new[]
                {
                    new PaletteRow(
                        "No matches found",
                        null,
                        $"Kein Treffer für „{queryText}“. Versuche z. B. dashboard, ask now, shortcut.",
                        null,
                        null,
                        null,
                        Visibility.Visible,
                        Visibility.Collapsed)
                };
            }

            return rowsOut.ToArray();
        }

        PaletteRow ToRow(PaletteCommand cmd, string section, bool firstInSection, string? tooltip)
        {
            var hotKeyBadge = string.IsNullOrWhiteSpace(cmd.Shortcut) ? Visibility.Collapsed : Visibility.Visible;
            var sectionTag = section switch
            {
                "Recent" => "Recent",
                "Pinned" => "Pinned",
                _ => cmd.Group
            };
            return new PaletteRow(
                cmd.Label,
                sectionTag,
                $"{cmd.Group}  ·  {cmd.Label}",
                cmd.Shortcut,
                cmd.Glyph,
                cmd.Run,
                Visibility.Collapsed,
                Visibility.Visible,
                firstInSection ? Visibility.Visible : Visibility.Collapsed,
                hotKeyBadge,
                tooltip);
        }

        list.ItemTemplate = (DataTemplate)XamlReader.Load(
            @"<DataTemplate xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
                           xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml"">
              <Grid Padding=""2"" Background=""{ThemeResource LayerFillColorDefaultBrush}"" ToolTipService.ToolTip=""{Binding Tooltip}"">
                <Border Visibility=""{Binding HeaderVisibility}""
                        Margin=""6,4,0,4""
                        Padding=""10,8,10,8""
                        Background=""{ThemeResource SubtleFillColorSecondaryBrush}""
                        BorderBrush=""{ThemeResource AccentFillColorDefaultBrush}""
                        BorderThickness=""1""
                        CornerRadius=""10"">
                  <TextBlock
                    Text=""{Binding Label}""
                    FontSize=""11""
                    FontWeight=""SemiBold""
                    Foreground=""{ThemeResource TextFillColorPrimaryBrush}""/>
                </Border>

                <Grid x:Name=""CommandRow"" Visibility=""{Binding CommandRowVisibility}""
                      Padding=""4,0,4,0"">
                  <Grid.RowDefinitions>
                    <RowDefinition Height=""Auto""/>
                  </Grid.RowDefinitions>
                  <Grid.ColumnDefinitions>
                    <ColumnDefinition Width=""Auto""/>
                    <ColumnDefinition Width=""*""/>
                  </Grid.ColumnDefinitions>

                  <Border
                    Grid.ColumnSpan=""2""
                    Height=""1""
                    Margin=""8,0,0,10""
                    Visibility=""{Binding SeparatorVisibility}""
                    Background=""{ThemeResource DividerStrokeColorDefaultBrush}"" />

                  <Border
                    Width=""30""
                    Height=""30""
                    CornerRadius=""15""
                    HorizontalAlignment=""Left""
                    VerticalAlignment=""Top""
                    Padding=""7,4,0,0""
                    Background=""{ThemeResource AccentFillColorDefaultBrush}"">
                    <TextBlock
                      Text=""{Binding Glyph}""
                      FontFamily=""Segoe Fluent Icons""
                      FontSize=""13""
                      TextAlignment=""Center""
                      Foreground=""{ThemeResource TextFillColorPrimaryBrush}""/>
                  </Border>
                  <Border
                    Grid.Column=""1""
                    Margin=""12,0,0,4""
                    Padding=""10,10,10,10""
                    CornerRadius=""10""
                    BorderThickness=""1""
                    BorderBrush=""{ThemeResource SubtleFillColorSecondaryBrush}""
                    Background=""{ThemeResource ControlFillColorSecondaryBrush}"">
                    <Grid>
                      <Grid.RowDefinitions>
                        <RowDefinition Height=""Auto""/>
                      </Grid.RowDefinitions>
                      <Grid.ColumnDefinitions>
                        <ColumnDefinition Width=""*""/>
                        <ColumnDefinition Width=""Auto""/>
                      </Grid.ColumnDefinitions>
                      <StackPanel Grid.Row=""0"" Grid.Column=""0"" Spacing=""2"">
                        <TextBlock
                          Text=""{Binding Label}""
                          FontWeight=""SemiBold""
                          FontSize=""14""
                          TextWrapping=""Wrap""
                          Foreground=""{ThemeResource TextFillColorPrimaryBrush}""/>
                        <TextBlock
                          Text=""{Binding Subtitle}""
                          FontSize=""11""
                          Foreground=""{ThemeResource TextFillColorSecondaryBrush}""
                          TextWrapping=""Wrap""/>
                      </StackPanel>
                      <Border Grid.Row=""0"" Grid.Column=""1""
                              Margin=""10,0,0,0""
                              Padding=""8,3,8,3""
                              BorderThickness=""1""
                              BorderBrush=""{ThemeResource AccentStrokeColorDefaultBrush}""
                              Background=""{ThemeResource SubtleFillColorSecondaryBrush}""
                              CornerRadius=""999""
                              Visibility=""{Binding ShortcutVisibility}""
                              HorizontalAlignment=""Right""
                              VerticalAlignment=""Top"">
                        <TextBlock Text=""{Binding Shortcut}""
                                   FontSize=""11""
                                   FontFamily=""Consolas""
                                   Foreground=""{ThemeResource TextFillColorSecondaryBrush}""
                                   TextWrapping=""NoWrap""/>
                      </Border>
                    </Grid>
                  </Border>
                </Grid>
              </Grid>
            </DataTemplate>");

        var search = new AutoSuggestBox
        {
            PlaceholderText = "Befehl suchen (z. B. dashboard, ctrl+r, live)",
            Margin = new Thickness(0, 0, 0, 10),
            Height = 38,
            FontSize = 13,
            FontFamily = new FontFamily("Segoe UI"),
            QueryIcon = new FontIcon
            {
                FontFamily = new FontFamily("Segoe Fluent Icons"),
                Glyph = "\uE721"
            },
            Text = string.Empty
        };

        var footer = new TextBlock
        {
            FontSize = 11,
            Foreground = WinUiFluentChrome.TertiaryTextBrush,
            FontFamily = new FontFamily("Consolas")
        };

        var paletteHint = new InfoBar
        {
            IsOpen = true,
            IsTabStop = false,
            IsClosable = false,
            Severity = InfoBarSeverity.Informational,
            Title = "Befehlspalette",
            Message = "Type to filter · Enter or click to run · Esc to close",
            Margin = new Thickness(0, 0, 0, 10)
        };

        PaletteRow[] ApplyFilter(string query)
        {
            var q = query?.Trim();
            return BuildRows(q, activeGroupFilter);
        }

        var filterState = string.Empty;
        var filterAll = new Button();
        var filterPage = new Button();
        var filterAction = new Button();
        var filterHelp = new Button();
        var filterRecent = new Button();
        var filterPinned = new Button();
        var filterClearHistory = new Button();

        void SetFilter(string? group)
        {
            activeGroupFilter = group;
            filterState = string.IsNullOrWhiteSpace(group) ? "all" : group;
            foreach (var b in new[] { filterAll, filterPage, filterAction, filterHelp, filterRecent, filterPinned })
            {
                b.Style = null;
                if (Application.Current?.Resources.TryGetValue("ModernCommandChipStyle", out var style) == true && style is Style st)
                    b.Style = st;
                b.Opacity = 0.9;
                b.Background = WinUiFluentChrome.BadgeBackground;
            }

            filterAll.Content = "All";
            filterPage.Content = "Page";
            filterAction.Content = "Action";
            filterHelp.Content = "Help";
            filterRecent.Content = "Recent";
            filterPinned.Content = "Pinned";

            Button? activeButton = group switch
            {
                "Page" => filterPage,
                "Action" => filterAction,
                "Help" => filterHelp,
                "Recent" => filterRecent,
                "Pinned" => filterPinned,
                _ => filterAll
            };
            if (activeButton != null)
            {
                activeButton.Opacity = 1.0;
                activeButton.Background = WinUiFluentChrome.TryThemeBrush("AccentFillColorDefaultBrush") ?? WinUiFluentChrome.BadgeBackground;
                activeButton.Foreground = WinUiFluentChrome.PrimaryTextBrush;
            }

            RenderPalette(search.Text);
        }

        void MakePaletteFilterChip(Button b, string label, string? group)
        {
            b.Content = label;
            WinUiFluentChrome.StyleActionButton(b, compact: true);
            if (Application.Current?.Resources.TryGetValue("ModernCommandChipStyle", out var style) == true && style is Style st)
                b.Style = st;
            b.MinWidth = 60;
            b.Click += (_, _) => SetFilter(group);
            b.Padding = new Thickness(8, 6, 8, 6);
            b.FontSize = 11;
        }

        MakePaletteFilterChip(filterAll, "All", null);
        MakePaletteFilterChip(filterPage, "Page", "Page");
        MakePaletteFilterChip(filterAction, "Action", "Action");
        MakePaletteFilterChip(filterHelp, "Help", "Help");
        MakePaletteFilterChip(filterRecent, "Recent", "Recent");
        MakePaletteFilterChip(filterPinned, "Pinned", "Pinned");
        WinUiFluentChrome.StyleActionButton(filterClearHistory, compact: true);
        if (Application.Current?.Resources.TryGetValue("ModernCommandChipStyle", out var clearStyle) == true && clearStyle is Style clearSt)
            filterClearHistory.Style = clearSt;
        filterClearHistory.Click += (_, _) =>
        {
            activeGroupFilter = null;
            filterState = "all";
            _commandPaletteHistory.Clear();
            CommandPaletteRecentStore.Clear();
            RenderPalette(string.Empty);
        };
        filterClearHistory.Content = "Clear history";
        filterClearHistory.MinWidth = 100;
        filterClearHistory.Padding = new Thickness(8, 6, 8, 6);
        filterClearHistory.FontSize = 11;
        SetFilter(null);

        var filters = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8
        };
        filters.Children.Add(filterAll);
        filters.Children.Add(filterPage);
        filters.Children.Add(filterAction);
        filters.Children.Add(filterHelp);
        filters.Children.Add(filterRecent);
        filters.Children.Add(filterPinned);
        filters.Children.Add(filterClearHistory);
        var filterScroller = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden,
            HorizontalScrollMode = ScrollMode.Disabled,
            VerticalScrollMode = ScrollMode.Disabled,
            Content = filters
        };
        RenderPalette(string.Empty);

        void RenderPalette(string query)
        {
            var rows = ApplyFilter(query);
            list.ItemsSource = rows;
            var commandRows = rows.Where(r => r.Run is not null).ToArray();
            var allCount = commandRows.Length;
            var pageCount = commandRows.Count(r => string.Equals(r.Group, "Page", StringComparison.OrdinalIgnoreCase));
            var actionCount = commandRows.Count(r => string.Equals(r.Group, "Action", StringComparison.OrdinalIgnoreCase));
            var helpCount = commandRows.Count(r => string.Equals(r.Group, "Help", StringComparison.OrdinalIgnoreCase));
            var recentCount = commandRows.Count(r => string.Equals(r.Group, "Recent", StringComparison.OrdinalIgnoreCase));
            filterAll.Content = allCount > 0 ? $"All ({allCount})" : "All";
            filterPage.Content = pageCount > 0 ? $"Page ({pageCount})" : "Page";
            filterAction.Content = actionCount > 0 ? $"Action ({actionCount})" : "Action";
            filterHelp.Content = helpCount > 0 ? $"Help ({helpCount})" : "Help";
            filterRecent.Content = recentCount > 0 ? $"Recent ({recentCount})" : "Recent";
            filterClearHistory.IsEnabled = _commandPaletteHistory.Count > 0;
            ToolTipService.SetToolTip(filterClearHistory, _commandPaletteHistory.Count > 0 ? "Letzte Befehle löschen" : "Keine Historie vorhanden");
            var firstCommandIndex = -1;

            if (rows.Length > 0)
            {
                for (var i = 0; i < rows.Length; i++)
                {
                    if (rows[i].Run is not null)
                    {
                        firstCommandIndex = i;
                        break;
                    }
                }
            }

            list.SelectedIndex = firstCommandIndex;
            if (rows.Length > 0)
                footer.Text = $"{allCount} Treffer · Filter: {(filterState == string.Empty ? "All" : filterState)} · Enter = ausführen, ⬆⬇ = Navigation, Esc = schließen";
            else
                footer.Text = $"Keine Treffer{(filterState == string.Empty ? string.Empty : $" im Filter '{filterState}'")}";
            footer.FontFamily = new FontFamily("Consolas");
            paletteHint.Message = $"Filter: {(filterState == string.Empty ? "All" : filterState)} · {allCount} Treffer · Esc schließen";
        }

        void SelectNextCommandable(int delta, int start)
        {
            if (list.Items.Count == 0)
                return;

            for (var idx = start; delta > 0 ? idx < list.Items.Count : idx >= 0; idx += delta)
            {
                if (list.Items[idx] is PaletteRow row && row.Run is not null)
                {
                    list.SelectedIndex = idx;
                    break;
                }
            }
        }

        ContentDialog? dlg = null;

        async void RunPalette(PaletteRow? row)
        {
            if (row?.Run is null)
                return;
            if (dlg is not null)
                dlg.Hide();
            await row.Run();
        }

        list.ItemClick += (_, args) =>
        {
            if (args.ClickedItem is PaletteRow row)
                RunPalette(row);
        };
        list.KeyDown += (_, e) =>
        {
            if (e.Key == VirtualKey.Enter && list.SelectedItem is PaletteRow row)
            {
                RunPalette(row);
                e.Handled = true;
            }
            else if (e.Key == VirtualKey.Escape && dlg is not null)
            {
                dlg.Hide();
                e.Handled = true;
            }
            else if (e.Key == VirtualKey.Down)
            {
                if (list.Items.Count > 0)
                {
                    list.Focus(FocusState.Programmatic);
                    SelectNextCommandable(1, list.SelectedIndex + 1);
                    e.Handled = true;
                }
            }
            else if (e.Key == VirtualKey.Up)
            {
                if (list.Items.Count > 0)
                {
                    list.Focus(FocusState.Programmatic);
                    SelectNextCommandable(-1, Math.Max(list.SelectedIndex - 1, 0));
                    e.Handled = true;
                }
            }
        };

        search.TextChanged += (_, args) =>
        {
            if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput)
                return;
            RenderPalette(search.Text);
        };
        search.KeyDown += (_, e) =>
        {
            if (e.Key == VirtualKey.Down)
            {
                if (list.Items.Count > 0)
                {
                    list.Focus(FocusState.Programmatic);
                    var start = list.SelectedIndex < 0 ? 0 : list.SelectedIndex + 1;
                    SelectNextCommandable(1, start);
                }
                e.Handled = true;
            }
            else if (e.Key == VirtualKey.Up)
            {
                if (list.Items.Count > 0)
                {
                    list.Focus(FocusState.Programmatic);
                    var start = list.SelectedIndex > 0 ? list.SelectedIndex - 1 : 0;
                    SelectNextCommandable(-1, start);
                }
                e.Handled = true;
            }
            else if (e.Key == VirtualKey.Enter)
            {
                if (list.SelectedItem is PaletteRow row)
                    RunPalette(row);
                else if (list.Items.Count > 0 && list.Items[0] is PaletteRow first)
                    RunPalette(first);
                e.Handled = true;
            }
            else if (e.Key == VirtualKey.Escape && dlg is not null)
            {
                dlg.Hide();
                e.Handled = true;
            }
        };

        search.QuerySubmitted += (_, _) =>
        {
            if (list.SelectedItem is PaletteRow row)
                RunPalette(row);
            else if (list.Items.Count > 0 && list.Items[0] is PaletteRow first)
                RunPalette(first);
        };

        list.SelectionChanged += (_, _) =>
        {
            if (list.SelectedItem is PaletteRow row && row.Run is not null)
                footer.Text = $"Auswahl: {row.Label} · Enter zum Ausführen";
        };

        RenderPalette(string.Empty);

        dlg = new ContentDialog
        {
            Title = "Befehlspalette",
            RequestedTheme = ElementTheme.Dark,
            Content = new Border
            {
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                BorderBrush = WinUiFluentChrome.SeparatorBrush,
                Background = WinUiFluentChrome.LayerChromeBackground,
                Padding = new Thickness(12),
                Child = new StackPanel
                {
                    Spacing = 8,
                    Children = { search, paletteHint, filterScroller, list, footer },
                }
            },
            CornerRadius = new CornerRadius(12),
            PrimaryButtonText = "Run",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = Content.XamlRoot
        };
        dlg.Loaded += (_, _) =>
        {
            RenderPalette(string.Empty);
            _ = search.Focus(FocusState.Programmatic);
        };

        var r = await dlg.ShowAsync();
        if (r == ContentDialogResult.Primary)
        {
            if (list.SelectedItem is PaletteRow row)
                await (row.Run?.Invoke() ?? Task.CompletedTask);
            else if (list.Items.Count > 0 && list.Items[0] is PaletteRow first)
                await (first.Run?.Invoke() ?? Task.CompletedTask);
        }
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

    private sealed record PaletteCommand(string Id, string Label, string Group, string? Shortcut, string Glyph, Func<Task> Run);
    private sealed record PaletteRow(
        string Label,
        string? Group,
        string? Subtitle,
        string? Shortcut,
        string? Glyph,
        Func<Task>? Run,
        Visibility HeaderVisibility,
        Visibility CommandRowVisibility,
        Visibility SeparatorVisibility = Visibility.Collapsed,
        Visibility ShortcutVisibility = Visibility.Collapsed,
        string? Tooltip = null);
}

