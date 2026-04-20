using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CarolusNexus;
using CarolusNexus.Services;
using CarolusNexus_WinUI.Pages;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using VirtualKey = Windows.System.VirtualKey;

namespace CarolusNexus_WinUI;

public sealed partial class MainWindow : Window
{
    private readonly Frame _frame = new();
    private readonly NavigationView _nav = new()
    {
        IsBackButtonVisible = NavigationViewBackButtonVisible.Collapsed,
        IsSettingsVisible = false,
        PaneDisplayMode = NavigationViewPaneDisplayMode.Auto,
        OpenPaneLength = 280,
        PaneTitle = "Carolus Nexus"
    };

    private readonly TextBlock _badgeLayout = new() { FontSize = 11 };
    private readonly TextBlock _badgeEnv = new() { FontSize = 11 };
    private readonly TextBlock _badgeSpeech = new() { FontSize = 11 };
    private readonly TextBlock _badgeAuto = new() { FontSize = 11 };
    private readonly TextBlock _badgeKnow = new() { FontSize = 11 };
    private readonly TextBlock _tileMemory = new() { TextWrapping = TextWrapping.Wrap, FontSize = 11 };
    private readonly TextBlock _tileLive = new() { TextWrapping = TextWrapping.Wrap, FontSize = 11 };
    private readonly TextBlock _tileEnv = new() { TextWrapping = TextWrapping.Wrap, FontSize = 11 };

    public MainWindow()
    {
        InitializeComponent();

        _nav.MenuItems.Add(Mk("Ask", typeof(AskShellPage), Symbol.Message));
        _nav.MenuItems.Add(Mk("Dashboard", typeof(DashboardShellPage), Symbol.Home));
        _nav.MenuItems.Add(Mk("Setup", typeof(SetupShellPage), Symbol.Setting));
        _nav.MenuItems.Add(Mk("Knowledge", typeof(KnowledgeShellPage), Symbol.Bookmarks));
        _nav.MenuItems.Add(Mk("Rituals", typeof(RitualsShellPage), Symbol.AllApps));
        _nav.MenuItems.Add(Mk("History", typeof(HistoryShellPage), Symbol.Clock));
        _nav.MenuItems.Add(Mk("Diagnostics", typeof(DiagnosticsShellPage), Symbol.Remote));
        _nav.MenuItems.Add(Mk("Console", typeof(ConsoleShellPage), Symbol.Keyboard));
        _nav.MenuItems.Add(Mk("Live Context", typeof(LiveContextShellPage), Symbol.View));
        _nav.MenuItems.Add(Mk("Experiments (Tier C)", typeof(ExperimentsShellPage), Symbol.Important));

        _nav.FooterMenuItems.Add(MkFooter("Command palette (Ctrl+P)"));

        _nav.Content = _frame;
        _nav.ItemInvoked += NavOnItemInvoked;
        _nav.Loaded += (_, _) =>
        {
            _nav.SelectedItem = _nav.MenuItems.OfType<NavigationViewItem>().FirstOrDefault();
            DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () =>
            {
                if (_nav.SelectedItem is NavigationViewItem first && first.Tag is Type t)
                    ShowShellPage(t);
            });
        };

        var header = BuildHeaderChrome();
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.Children.Add(header);
        root.Children.Add(_nav);
        Grid.SetRow(_nav, 1);

        RootGrid.Children.Add(root);

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
    }

    private UIElement BuildHeaderChrome()
    {
        var border = new Border
        {
            Padding = new Thickness(14, 10, 14, 10),
            BorderThickness = new Thickness(0, 0, 0, 1),
            BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 72, 72, 78)),
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 28, 28, 30))
        };

        var stack = new StackPanel { Spacing = 10 };

        var titleRow = new Grid();
        titleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        titleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var titleBlock = new StackPanel { Spacing = 2 };
        titleBlock.Children.Add(new TextBlock
        {
            Text = "Carolus Nexus",
            FontSize = 22,
            FontWeight = Microsoft.UI.Text.FontWeights.Bold
        });
        titleBlock.Children.Add(new TextBlock
        {
            Text = "Windows operator desktop · persona: Karl Klammer",
            FontSize = 12,
            Opacity = 0.75
        });
        titleBlock.Children.Add(new TextBlock
        {
            Text = "WinUI shell — align with Avalonia for ops testing.",
            FontSize = 11,
            Opacity = 0.65
        });
        Grid.SetColumn(titleBlock, 0);
        titleRow.Children.Add(titleBlock);

        var titleBtns = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var handbook = new Button { Content = "Handbook", Padding = new Thickness(10, 4, 10, 4) };
        handbook.Click += OnHandbookClick;
        titleBtns.Children.Add(handbook);
        Grid.SetColumn(titleBtns, 1);
        titleRow.Children.Add(titleBtns);
        stack.Children.Add(titleRow);

        var badges = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        foreach (var tb in new[] { _badgeLayout, _badgeEnv, _badgeSpeech, _badgeAuto, _badgeKnow })
        {
            badges.Children.Add(new Border
            {
                Margin = new Thickness(0, 0, 8, 6),
                Padding = new Thickness(8, 4, 8, 4),
                CornerRadius = new CornerRadius(4),
                Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 48, 48, 52)),
                Child = tb
            });
        }

        stack.Children.Add(badges);

        var tiles = new Grid { HorizontalAlignment = HorizontalAlignment.Stretch };
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

        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var bRefresh = new Button { Content = "refresh all", Margin = new Thickness(0, 0, 8, 6), Padding = new Thickness(12, 6, 12, 6) };
        bRefresh.Click += (_, _) => OnRefreshAll();
        var bSave = new Button { Content = "save settings", Margin = new Thickness(0, 0, 8, 6), Padding = new Thickness(12, 6, 12, 6) };
        bSave.Click += (_, _) => OnSaveSettings();
        var bReindex = new Button { Content = "reindex knowledge", Margin = new Thickness(0, 0, 8, 6), Padding = new Thickness(12, 6, 12, 6) };
        bReindex.Click += (_, _) => OnReindex();
        var bApp = new Button { Content = "refresh active app", Margin = new Thickness(0, 0, 8, 6), Padding = new Thickness(12, 6, 12, 6) };
        bApp.Click += (_, _) => OnRefreshActiveApp();
        var bExport = new Button { Content = "export diagnostics", Margin = new Thickness(0, 0, 8, 6), Padding = new Thickness(12, 6, 12, 6) };
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

    private static Border MkTile(string title, TextBlock body) =>
        new()
        {
            Padding = new Thickness(8),
            Margin = new Thickness(0, 0, 6, 0),
            CornerRadius = new CornerRadius(6),
            BorderThickness = new Thickness(1, 1, 1, 1),
            BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 72, 72, 78)),
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 36, 36, 40)),
            Child = new StackPanel
            {
                Spacing = 4,
                Children =
                {
                    new TextBlock { Text = title, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, FontSize = 11 },
                    body
                }
            }
        };

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
            return;
        _tileLive.Text = "Click „refresh active app“ for foreground window + adapter.";
    }

    private void OnRefreshAll()
    {
        WinUiShellState.Settings = WinUiShellState.SettingsStore.LoadOrDefault();
        DotEnvStore.Invalidate();
        WinUiShellState.TryApplySettingsToSetup?.Invoke(WinUiShellState.Settings);
        WinUiShellState.TryRefreshSetupEnvSummary?.Invoke();
        WinUiThemeApplier.Apply(WinUiShellState.Settings.UiTheme);
        RefreshHeaderBadges();
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
            Icon = new SymbolIcon(Symbol.Find),
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
            if (Activator.CreateInstance(pageType) is not Page page)
                return;
            _frame.Content = page;
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
        var pages = new (string Label, Type PageType)[]
        {
            ("Ask", typeof(AskShellPage)),
            ("Dashboard", typeof(DashboardShellPage)),
            ("Setup", typeof(SetupShellPage)),
            ("Knowledge", typeof(KnowledgeShellPage)),
            ("Rituals", typeof(RitualsShellPage)),
            ("History", typeof(HistoryShellPage)),
            ("Diagnostics", typeof(DiagnosticsShellPage)),
            ("Console", typeof(ConsoleShellPage)),
            ("Live Context", typeof(LiveContextShellPage)),
            ("Experiments (Tier C)", typeof(ExperimentsShellPage))
        };

        var list = new ListView { SelectionMode = ListViewSelectionMode.Single, ItemsSource = pages.Select(p => p.Label).ToList() };
        var dlg = new ContentDialog
        {
            Title = "Go to page",
            Content = list,
            PrimaryButtonText = "Go",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = Content.XamlRoot
        };

        list.DoubleTapped += async (_, _) =>
        {
            dlg.Hide();
            await NavigateFromPaletteSelectionAsync(list, pages);
        };

        var r = await dlg.ShowAsync();
        if (r == ContentDialogResult.Primary)
            await NavigateFromPaletteSelectionAsync(list, pages);
    }

    private Task NavigateFromPaletteSelectionAsync(ListView list, (string Label, Type PageType)[] pages)
    {
        if (list.SelectedIndex < 0)
            return Task.CompletedTask;
        var t = pages[list.SelectedIndex].PageType;
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
}
