using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using CarolusNexus;
using CarolusNexus.Models;
using CarolusNexus.Services;

namespace CarolusNexus.Views;

public partial class HistoryTab : UserControl
{
    private List<ActionHistoryEntry> _entries = new();
    private bool? _histNarrowLayout;

    public HistoryTab()
    {
        InitializeComponent();
        BtnSelfHealHint.Click += (_, _) =>
        {
            var hint = SelfHealSuggestionService.TrySuggestFromLastAuditFailure();
            HistDetail.Text = hint ?? "(no recent audit failure found)";
            if (hint != null)
                NexusShell.Log("Self-heal: " + hint);
        };
        BtnCreateRitual.Click += (_, _) => CreateRitualFromSelection();
        HistFilter.TextChanged += (_, _) => ApplyFilter();
        HistList.SelectionChanged += (_, _) =>
        {
            if (HistList.SelectedItem is ActionHistoryEntry e)
            {
                HistDetail.Text = JsonSerializer.Serialize(e,
                    new JsonSerializerOptions { WriteIndented = true });
            }
            else
                HistDetail.Text = HistList.SelectedItem?.ToString() ?? "";
        };
        Loaded += (_, _) => Refresh();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        LayoutUpdated += HistOnLayoutUpdated;
        ApplyHistoryResponsiveLayout();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        LayoutUpdated -= HistOnLayoutUpdated;
        base.OnDetachedFromVisualTree(e);
    }

    private void HistOnLayoutUpdated(object? sender, EventArgs e) => ApplyHistoryResponsiveLayout();

    private void ApplyHistoryResponsiveLayout()
    {
        var w = Bounds.Width;
        if (w <= 0)
            return;
        var narrow = w < ResponsiveLayout.NarrowMax;
        if (_histNarrowLayout == narrow)
            return;
        _histNarrowLayout = narrow;

        HistRootGrid.ColumnDefinitions.Clear();
        HistRootGrid.RowDefinitions.Clear();
        if (narrow)
        {
            HistRootGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            HistRootGrid.RowDefinitions.Add(new RowDefinition(new GridLength(200)));
            HistRootGrid.RowDefinitions.Add(new RowDefinition(GridLength.Star));
            Grid.SetColumn(HistLeftPane, 0);
            Grid.SetRow(HistLeftPane, 0);
            Grid.SetColumn(HistRightPane, 0);
            Grid.SetRow(HistRightPane, 1);
            HistLeftPane.Margin = new Thickness(0, 0, 0, 8);
        }
        else
        {
            HistRootGrid.RowDefinitions.Add(new RowDefinition(GridLength.Star));
            HistRootGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(280)));
            HistRootGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            Grid.SetColumn(HistLeftPane, 0);
            Grid.SetRow(HistLeftPane, 0);
            Grid.SetColumn(HistRightPane, 1);
            Grid.SetRow(HistRightPane, 0);
            HistLeftPane.Margin = new Thickness(0, 0, 12, 0);
        }
    }

    public void Refresh()
    {
        var doc = ActionHistoryService.Load();
        _entries = doc.Entries.OrderByDescending(e => e.UtcAt).ToList();
        if (_entries.Count == 0)
        {
            HistList.ItemsSource = new[] { "(no entries in action-history.json yet — run a plan/ritual)" };
            HistDetail.Text = "";
            return;
        }

        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var q = HistFilter.Text?.Trim() ?? "";
        IEnumerable<ActionHistoryEntry> src = _entries;
        if (!string.IsNullOrEmpty(q))
        {
            src = _entries.Where(e =>
                e.ListLabel.Contains(q, StringComparison.OrdinalIgnoreCase)
                || e.Summary.Contains(q, StringComparison.OrdinalIgnoreCase)
                || e.Kind.Contains(q, StringComparison.OrdinalIgnoreCase));
        }

        var list = src.ToList();
        HistList.ItemsSource = list.Count == 0 ? new[] { "(no matches)" } : list;
    }

    private void CreateRitualFromSelection()
    {
        if (HistList.SelectedItem is not ActionHistoryEntry e)
        {
            NexusShell.Log("History: select an entry.");
            return;
        }

        if (e.Steps.Count == 0)
        {
            NexusShell.Log("History: selected entry has no steps.");
            return;
        }

        var recipe = new AutomationRecipe
        {
            Name = $"From history {e.UtcAt.ToLocalTime():yyyy-MM-dd HH:mm}",
            Description = string.IsNullOrWhiteSpace(e.Summary) ? "From action-history" : e.Summary[..Math.Min(200, e.Summary.Length)],
            Steps = e.Steps.Select(s => new RecipeStep
            {
                ActionType = s.ActionType,
                ActionArgument = s.ActionArgument,
                WaitMs = s.WaitMs
            }).ToList()
        };
        RitualRecipeStore.AppendRecipe(recipe);
        NexusShell.Log($"Ritual created from history: {recipe.Name} ({recipe.Steps.Count} steps).");
    }
}
