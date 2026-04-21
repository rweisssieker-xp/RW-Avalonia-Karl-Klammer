using System;
using CarolusNexus;
using CarolusNexus_WinUI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace CarolusNexus_WinUI.Pages;

/// <summary>Seven summary cards + 3D placeholder — aligned with Avalonia <c>DashboardTab</c>.</summary>
public sealed class DashboardShellPage : Page
{
    private readonly TextBox _cardEnv = MkCard();
    private readonly TextBox _cardKnow = MkCard();
    private readonly TextBox _cardLive = MkCard();
    private readonly TextBox _cardPro = MkCard();
    private readonly TextBox _cardGov = MkCard();
    private readonly TextBox _cardRit = MkCard();
    private readonly TextBox _cardWatch = MkCard();
    private readonly TextBox _cardUsp = MkCard();

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
        var refresh = new Button { Content = "Refresh now", HorizontalAlignment = HorizontalAlignment.Left };
        WinUiFluentChrome.StyleActionButton(refresh, accent: true);
        refresh.Click += (_, _) => RefreshFull();
        stack.Children.Add(refresh);

        stack.Children.Add(WinUiFluentChrome.ColumnCaption("3D preview"));
        var previewInner = new TextBlock
        {
            Text =
                "OfficeScene3D (OpenGL) ships in the Avalonia build.\n" +
                "WinUI uses the same dashboard data (incl. watch snapshots + proactive LLM in watch mode) in the cards below.",
            Margin = new Thickness(16, 14, 16, 14),
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = WinUiFluentChrome.SecondaryTextBrush
        };
        var previewBox = new Border
        {
            Height = 148,
            CornerRadius = new CornerRadius(WinUiFluentChrome.CardCornerRadius),
            BorderThickness = new Thickness(1),
            BorderBrush = WinUiFluentChrome.CardBorderBrush,
            Background = WinUiFluentChrome.CardSurfaceBackground,
            Child = previewInner
        };
        WinUiFluentChrome.ApplyCardElevation(previewBox, 2f);
        stack.Children.Add(previewBox);

        var grid = new Grid { HorizontalAlignment = HorizontalAlignment.Stretch, ColumnSpacing = 10, RowSpacing = 10 };
        for (var i = 0; i < 4; i++)
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
}
