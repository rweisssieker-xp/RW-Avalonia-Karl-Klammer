using System;
using System.IO;
using System.Linq;
using CarolusNexus;
using CarolusNexus.Services;
using CarolusNexus_WinUI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using VirtualKey = Windows.System.VirtualKey;
using VirtualKeyModifiers = Windows.System.VirtualKeyModifiers;

namespace CarolusNexus_WinUI.Pages;

/// <summary>Seven summary cards + watch scene preview — aligned with Avalonia <c>DashboardTab</c>.</summary>
public sealed class DashboardShellPage : Page
{
    private readonly TextBlock _heroHeadline = new() { TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _heroReadiness = new() { TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _heroUsp = new() { TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _signalProvider = SignalText();
    private readonly TextBlock _signalReadiness = SignalText();
    private readonly TextBlock _signalContext = SignalText();
    private readonly TextBlock _signalWatch = SignalText();
    private readonly TextBlock _signalAction = SignalText();
    private readonly Button _nbaPrimary = new();
    private readonly Button _nbaSecondary = new();
    private readonly Button _nbaDismiss = new();
    private Border? _nextBestActionBar;
    private NextBestAction? _nextBestAction;
    private readonly TextBox _cardEnv = MkCard();
    private readonly TextBox _cardKnow = MkCard();
    private readonly TextBox _cardLive = MkCard();
    private readonly TextBox _cardPro = MkCard();
    private readonly TextBox _cardGov = MkCard();
    private readonly TextBox _cardRit = MkCard();
    private readonly TextBox _cardWatch = MkCard();
    private readonly TextBox _cardUsp = MkCard();
    private readonly TextBox _cardNext = MkCard();
    private readonly Image _sceneImage = new() { Height = 136 };
    private readonly TextBlock _sceneMeta = new()
    {
        FontSize = 11,
        TextWrapping = TextWrapping.Wrap,
        Foreground = WinUiFluentChrome.SecondaryTextBrush
    };

    public DashboardShellPage()
    {
        var root = new ScrollViewer();
        var stack = new StackPanel { Spacing = 16, Margin = new Thickness(20, 16, 20, 20) };
        stack.Children.Add(WinUiFluentChrome.PageTitle("Dashboard"));
        var dashHint = new TextBlock
        {
            Text = "Seven live cards + watch/proactive context — parity with Avalonia §5.3.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = WinUiFluentChrome.SecondaryTextBrush
        };
        WinUiFluentChrome.ApplyCaptionTextStyle(dashHint);
        stack.Children.Add(dashHint);
        stack.Children.Add(WinUiFluentChrome.StatusTile("Runtime reality", "mixed", "live local state; scene preview from latest watch snapshot; Ghost lives in the shell side panel"));
        stack.Children.Add(BuildDemoHero());
        _nextBestActionBar = BuildNextBestActionBar();
        stack.Children.Add(_nextBestActionBar);
        stack.Children.Add(BuildSignalStrip());
        var refresh = new Button { Content = "Refresh now", HorizontalAlignment = HorizontalAlignment.Left };
        WinUiFluentChrome.StyleActionButton(refresh, accent: true);
        WinUiFluentChrome.SetIconButton(refresh, "Refresh now", "\uE72C", "F5");
        WinUiFluentChrome.AddShortcut(refresh, VirtualKey.F5, tooltip: "F5");
        refresh.Click += (_, _) => RefreshFull();
        stack.Children.Add(refresh);

        stack.Children.Add(WinUiFluentChrome.ColumnCaption("Watch scene preview"));
        _sceneImage.Width = Double.NaN;
        _sceneImage.Stretch = Microsoft.UI.Xaml.Media.Stretch.UniformToFill;
        _sceneImage.HorizontalAlignment = HorizontalAlignment.Stretch;
        _sceneImage.VerticalAlignment = VerticalAlignment.Stretch;

        var previewScene = new StackPanel
        {
            Spacing = 8,
            Children =
            {
                _sceneImage,
                _sceneMeta
            }
        };
        var previewBox = new Border
        {
            Height = 148,
            CornerRadius = new CornerRadius(WinUiFluentChrome.CardCornerRadius),
            BorderThickness = new Thickness(1),
            BorderBrush = WinUiFluentChrome.CardBorderBrush,
            Background = WinUiFluentChrome.CardSurfaceBackground,
            Child = previewScene
        };
        WinUiFluentChrome.ApplyCardElevation(previewBox, 2f);
        stack.Children.Add(previewBox);

        var grid = new Grid { HorizontalAlignment = HorizontalAlignment.Stretch, ColumnSpacing = 10, RowSpacing = 10 };
        for (var i = 0; i < 5; i++)
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        void Add(int row, int col, string title, TextBox box, int colSpan = 1)
        {
            var b = new Border
            {
                Margin = new Thickness(0),
                Padding = new Thickness(14, 12, 14, 12),
                CornerRadius = new CornerRadius(WinUiFluentChrome.CardCornerRadius),
                BorderThickness = new Thickness(1),
                BorderBrush = WinUiFluentChrome.CardBorderBrush,
                Background = WinUiFluentChrome.CardSurfaceBackground,
                Child = new StackPanel
                {
                    Spacing = 6,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = title,
                            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                            Foreground = WinUiFluentChrome.PrimaryTextBrush
                        },
                        box
                    }
                }
            };
            WinUiFluentChrome.ApplyCardElevation(b, 2f);
            Grid.SetRow(b, row);
            Grid.SetColumn(b, col);
            if (colSpan > 1)
                Grid.SetColumnSpan(b, colSpan);
            grid.Children.Add(b);
        }

        Add(0, 0, "1 · Environment + Routing", _cardEnv);
        Add(0, 1, "2 · Knowledge + Memory", _cardKnow);
        Add(0, 2, "3 · Live Context", _cardLive);
        Add(1, 0, "4 · Proactive Karl + shell log", _cardPro);
        Add(1, 1, "5 · Governance + Audit", _cardGov);
        Add(1, 2, "6 · Recent operator flows", _cardRit);
        Add(2, 0, "7 · Recent Watch Sessions", _cardWatch, 3);
        Add(3, 0, "8 · AI / GUI / WebUI USP Radar", _cardUsp, 3);
        Add(4, 0, "9 · Next Action Intelligence", _cardNext, 3);

        stack.Children.Add(grid);
        root.Content = stack;
        Content = root;

        Loaded += (_, _) => RefreshFull();
    }

    public void RefreshFull()
    {
        WinUiDashboardData.FillCards(
            s => _cardEnv.Text = s,
            s => _cardKnow.Text = s,
            s => _cardLive.Text = s,
            s => _cardPro.Text = s,
            s => _cardGov.Text = s,
            s => _cardRit.Text = s,
            s => _cardWatch.Text = s,
            s => _cardUsp.Text = s,
            WinUiShellState.LiveContextLine,
            WinUiShellState.Settings);
        RefreshDemoHero();
        RefreshNextBestActionBar();
        RefreshSignalStrip();
        RefreshNextActionCard();
        RefreshScenePreview();
    }

    private Border BuildNextBestActionBar()
    {
        _nbaPrimary.Click += (_, _) =>
        {
            RefreshFull();
            NexusShell.Log("Next action: dashboard refreshed.");
        };
        _nbaSecondary.Click += (_, _) =>
        {
            if (_nextBestAction != null)
                NexusShell.Log("Next action suggestion: " + _nextBestAction.Message);
        };
        _nbaDismiss.Click += (_, _) =>
        {
            if (_nextBestActionBar != null)
                _nextBestActionBar.Visibility = Visibility.Collapsed;
        };

        _nextBestAction = NextBestActionService.Build(WinUiShellState.Settings, WinUiShellState.LiveContextLine);
        return WinUiFluentChrome.NextBestActionBar(_nextBestAction, _nbaPrimary, _nbaSecondary, _nbaDismiss);
    }

    private void RefreshNextBestActionBar()
    {
        _nextBestAction = NextBestActionService.Build(WinUiShellState.Settings, WinUiShellState.LiveContextLine);
        NexusShell.Log("Next action refreshed: " + _nextBestAction.Message);
    }

    private UIElement BuildDemoHero()
    {
        var hero = new Grid { ColumnSpacing = 16 };
        hero.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        hero.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(240) });

        var title = new TextBlock
        {
            Text = "AI operator cockpit",
            FontSize = 26,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = WinUiFluentChrome.PrimaryTextBrush,
            TextWrapping = TextWrapping.Wrap
        };
        _heroHeadline.Foreground = WinUiFluentChrome.SecondaryTextBrush;
        WinUiFluentChrome.ApplyBodyTextStyle(_heroHeadline);
        _heroUsp.Foreground = WinUiFluentChrome.SecondaryTextBrush;
        WinUiFluentChrome.ApplyCaptionTextStyle(_heroUsp);

        var left = new StackPanel
        {
            Spacing = 8,
            Children = { title, _heroHeadline, _heroUsp }
        };

        _heroReadiness.FontSize = 30;
        _heroReadiness.FontWeight = Microsoft.UI.Text.FontWeights.Bold;
        _heroReadiness.Foreground = WinUiFluentChrome.PrimaryTextBrush;
        var right = new StackPanel
        {
            Spacing = 4,
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                new TextBlock
                {
                    Text = "Demo readiness",
                    Foreground = WinUiFluentChrome.SecondaryTextBrush,
                    TextWrapping = TextWrapping.Wrap
                },
                _heroReadiness
            }
        };

        hero.Children.Add(left);
        hero.Children.Add(right);
        Grid.SetColumn(right, 1);

        return WinUiFluentChrome.WrapCard(hero, new Thickness(18, 16, 18, 16));
    }

    private void RefreshDemoHero()
    {
        var insight = OperatorInsightService.BuildSnapshot(WinUiShellState.Settings);
        var live = string.IsNullOrWhiteSpace(WinUiShellState.LiveContextLine)
            ? "No foreground context captured yet."
            : WinUiShellState.LiveContextLine;
        _heroReadiness.Text = $"{insight.ReadinessScore}/{insight.ReadinessMax}";
        _heroUsp.Text = insight.Usp;
        _heroHeadline.Text = $"{WinUiShellState.Settings.Provider} / {WinUiShellState.Settings.Mode} · {live}";
    }

    private UIElement BuildSignalStrip()
    {
        var grid = new Grid { ColumnSpacing = 10, RowSpacing = 10 };
        for (var i = 0; i < 5; i++)
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        AddSignal(grid, 0, "AI Route", _signalProvider);
        AddSignal(grid, 1, "Readiness", _signalReadiness);
        AddSignal(grid, 2, "GUI Context", _signalContext);
        AddSignal(grid, 3, "Watch Replay", _signalWatch);
        AddSignal(grid, 4, "USP Action", _signalAction);

        return grid;
    }

    private static void AddSignal(Grid grid, int col, string title, TextBlock value)
    {
        var caption = new TextBlock
        {
            Text = title,
            Foreground = WinUiFluentChrome.SecondaryTextBrush,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 11
        };

        var body = new StackPanel
        {
            Spacing = 4,
            Children = { caption, value }
        };

        var card = new Border
        {
            Padding = new Thickness(12, 10, 12, 10),
            CornerRadius = new CornerRadius(WinUiFluentChrome.CardCornerRadius),
            BorderThickness = new Thickness(1),
            BorderBrush = WinUiFluentChrome.CardBorderBrush,
            Background = WinUiFluentChrome.CardSurfaceBackground,
            Child = body
        };
        WinUiFluentChrome.ApplyCardElevation(card, 1f);
        Grid.SetColumn(card, col);
        grid.Children.Add(card);
    }

    private void RefreshSignalStrip()
    {
        var insight = OperatorInsightService.BuildSnapshot(WinUiShellState.Settings);
        _signalProvider.Text = $"{WinUiShellState.Settings.Provider} / {WinUiShellState.Settings.Mode}";
        _signalReadiness.Text = $"{insight.ReadinessScore}/{insight.ReadinessMax} · {insight.OperatorPosture}";
        _signalContext.Text = $"{insight.ProcessName} · {insight.AdapterFamily}";
        _signalWatch.Text = string.IsNullOrWhiteSpace(insight.ContextReplay)
            ? insight.WatchSummary
            : insight.ContextReplay;
        _signalAction.Text = insight.SafeNextAction;
    }

    private void RefreshNextActionCard()
    {
        var insight = OperatorInsightService.BuildSnapshot(WinUiShellState.Settings);
        _cardNext.Text =
            $"Active: {insight.ProcessName} · {insight.AdapterFamily}\n" +
            $"Likely task: {insight.LikelyTask}\n" +
            $"Safe next action: {insight.SafeNextAction}\n" +
            $"Risky action: {insight.RiskyAction}\n" +
            $"Recommended flow: {insight.RecommendedFlow}\n" +
            $"Posture: {insight.OperatorPosture}";
    }

    private void RefreshScenePreview()
    {
        try
        {
            var doc = WatchSessionService.LoadOrEmpty();
            var latest = doc.Entries.LastOrDefault();
            if (latest == null)
            {
                _sceneImage.Source = null;
                _sceneImage.MinWidth = 0;
                _sceneMeta.Text =
                    "No watch snapshot yet. Start watch mode and wait for first thumb capture.";
                return;
            }

            if (!string.IsNullOrWhiteSpace(latest.ThumbnailPath))
            {
                var abs = Path.Combine(AppPaths.DataDir, latest.ThumbnailPath);
                if (File.Exists(abs))
                {
                    _sceneImage.Source = new BitmapImage(new Uri(abs));
                }
                else
                {
                    _sceneImage.Source = null;
                    _sceneImage.MinWidth = 0;
                }
            }
            else
            {
                _sceneImage.Source = null;
                _sceneImage.MinWidth = 0;
            }

            _sceneMeta.Text =
                $"Latest watch frame: {latest.ProcessName ?? "unknown"} · {latest.AdapterFamily ?? "generic"} · hash " +
                $"{(string.IsNullOrWhiteSpace(latest.ScreenHash) ? "?" : latest.ScreenHash[..Math.Min(8, latest.ScreenHash.Length)])}";
        }
        catch (Exception ex)
        {
            _sceneImage.Source = null;
            _sceneMeta.Text = "Scene preview failed: " + ex.Message;
            NexusShell.Log("dashboard scene preview failed: " + ex.Message);
        }
    }

    private static TextBox MkCard() =>
        new()
        {
            IsReadOnly = true,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            MinHeight = 72,
            FontSize = 12
        };

    private static TextBlock SignalText() =>
        new()
        {
            TextWrapping = TextWrapping.Wrap,
            Foreground = WinUiFluentChrome.PrimaryTextBrush,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            FontSize = 13
        };
}
