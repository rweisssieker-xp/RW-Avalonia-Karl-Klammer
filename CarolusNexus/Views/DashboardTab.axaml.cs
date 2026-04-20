using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace CarolusNexus.Views;

public partial class DashboardTab : UserControl
{
    private ResponsiveBand? _dashLayoutBand;
    private Border[]? _dashCards;

    public DashboardTab()
    {
        InitializeComponent();
        _dashCards = [DashCard0, DashCard1, DashCard2, DashCard3, DashCard4, DashCard5, DashCard6];
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        LayoutUpdated += DashOnLayoutUpdated;
        ApplyDashboardResponsiveLayout();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        LayoutUpdated -= DashOnLayoutUpdated;
        base.OnDetachedFromVisualTree(e);
    }

    private void DashOnLayoutUpdated(object? sender, EventArgs e) => ApplyDashboardResponsiveLayout();

    private void ApplyDashboardResponsiveLayout()
    {
        if (_dashCards == null)
            return;
        var w = Bounds.Width;
        if (w <= 0)
            return;
        var band = ResponsiveLayout.GetBand(w);
        if (band == _dashLayoutBand)
            return;
        _dashLayoutBand = band;

        foreach (var b in _dashCards)
        {
            Grid.SetColumnSpan(b, 1);
            Grid.SetRowSpan(b, 1);
        }

        DashCardsGrid.ColumnDefinitions.Clear();
        DashCardsGrid.RowDefinitions.Clear();

        switch (band)
        {
            case ResponsiveBand.Narrow:
                DashCardsGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
                for (var r = 0; r < 7; r++)
                    DashCardsGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
                for (var i = 0; i < 7; i++)
                {
                    Grid.SetRow(_dashCards[i], i);
                    Grid.SetColumn(_dashCards[i], 0);
                }

                break;
            case ResponsiveBand.Medium:
                DashCardsGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
                DashCardsGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
                for (var r = 0; r < 4; r++)
                    DashCardsGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
                Grid.SetRow(_dashCards[0], 0);
                Grid.SetColumn(_dashCards[0], 0);
                Grid.SetRow(_dashCards[1], 0);
                Grid.SetColumn(_dashCards[1], 1);
                Grid.SetRow(_dashCards[2], 1);
                Grid.SetColumn(_dashCards[2], 0);
                Grid.SetRow(_dashCards[3], 1);
                Grid.SetColumn(_dashCards[3], 1);
                Grid.SetRow(_dashCards[4], 2);
                Grid.SetColumn(_dashCards[4], 0);
                Grid.SetRow(_dashCards[5], 2);
                Grid.SetColumn(_dashCards[5], 1);
                Grid.SetRow(_dashCards[6], 3);
                Grid.SetColumn(_dashCards[6], 0);
                Grid.SetColumnSpan(_dashCards[6], 2);
                break;
            default:
                DashCardsGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
                DashCardsGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
                DashCardsGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
                for (var r = 0; r < 3; r++)
                    DashCardsGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
                Grid.SetRow(_dashCards[0], 0);
                Grid.SetColumn(_dashCards[0], 0);
                Grid.SetRow(_dashCards[1], 0);
                Grid.SetColumn(_dashCards[1], 1);
                Grid.SetRow(_dashCards[2], 0);
                Grid.SetColumn(_dashCards[2], 2);
                Grid.SetRow(_dashCards[3], 1);
                Grid.SetColumn(_dashCards[3], 0);
                Grid.SetRow(_dashCards[4], 1);
                Grid.SetColumn(_dashCards[4], 1);
                Grid.SetRow(_dashCards[5], 1);
                Grid.SetColumn(_dashCards[5], 2);
                Grid.SetRow(_dashCards[6], 2);
                Grid.SetColumn(_dashCards[6], 0);
                Grid.SetColumnSpan(_dashCards[6], 3);
                break;
        }
    }

    public void RefreshSummaries(string env, string know, string live, string proactive, string gov, string rituals, string watch)
    {
        CardEnv.Text = env;
        CardKnow.Text = know;
        CardLive.Text = live;
        CardProactive.Text = proactive;
        CardGov.Text = gov;
        CardRituals.Text = rituals;
        CardWatch.Text = watch;
    }
}
