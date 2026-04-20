using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using CarolusNexus;
using CarolusNexus.Models;
using CarolusNexus.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace CarolusNexus_WinUI.Pages;

/// <summary>Parity with Avalonia <c>HistoryTab</c>.</summary>
public sealed class HistoryShellPage : Page
{
    private List<ActionHistoryEntry> _entries = new();
    private bool? _histNarrowLayout;

    private readonly Grid _histRoot = new() { MinHeight = 400, Margin = new Thickness(12) };
    private readonly StackPanel _leftPane = new() { Spacing = 8 };
    private readonly StackPanel _rightPane = new() { Spacing = 6 };

    private readonly TextBox _histFilter = new() { Header = "Search", PlaceholderText = "Filter by label, summary, kind…" };
    private readonly ListView _histList = new() { SelectionMode = ListViewSelectionMode.Single, MinHeight = 120 };
    private readonly TextBox _histDetail = new()
    {
        IsReadOnly = true,
        AcceptsReturn = true,
        TextWrapping = TextWrapping.Wrap,
        FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas"),
        MinHeight = 200
    };

    public HistoryShellPage()
    {
        var btnRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var selfHeal = new Button { Content = "Self-heal hint (last audit)", Padding = new Thickness(10, 6, 10, 6) };
        selfHeal.Click += (_, _) =>
        {
            var hint = SelfHealSuggestionService.TrySuggestFromLastAuditFailure();
            _histDetail.Text = hint ?? "(no recent audit failure found)";
            if (hint != null)
                NexusShell.Log("Self-heal: " + hint);
        };
        var createRitual = new Button { Content = "Create ritual from selection", Padding = new Thickness(10, 6, 10, 6) };
        createRitual.Click += (_, _) => CreateRitualFromSelection();
        var refresh = new Button { Content = "Reload from disk", Padding = new Thickness(10, 6, 10, 6) };
        refresh.Click += (_, _) => Refresh();
        btnRow.Children.Add(selfHeal);
        btnRow.Children.Add(createRitual);
        btnRow.Children.Add(refresh);

        _leftPane.Children.Add(new TextBlock
        {
            Text = "Action history",
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            FontSize = 14
        });
        _leftPane.Children.Add(_histFilter);
        _leftPane.Children.Add(btnRow);
        _leftPane.Children.Add(_histList);

        _rightPane.Children.Add(new TextBlock { Text = "Detail", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        _rightPane.Children.Add(_histDetail);

        _histFilter.TextChanged += (_, _) => ApplyFilter();
        _histList.SelectionChanged += OnSelectionChanged;

        Content = _histRoot;
        Loaded += (_, _) =>
        {
            Refresh();
            ApplyHistoryResponsiveLayout(ActualWidth);
        };
        SizeChanged += (_, e) => ApplyHistoryResponsiveLayout(e.NewSize.Width);
    }

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_histList.SelectedItem is not ListViewItem lvi)
        {
            _histDetail.Text = "";
            return;
        }

        switch (lvi.Tag)
        {
            case ActionHistoryEntry ent:
                _histDetail.Text = JsonSerializer.Serialize(ent, new JsonSerializerOptions { WriteIndented = true });
                break;
            case string s:
                _histDetail.Text = s;
                break;
            default:
                _histDetail.Text = "";
                break;
        }
    }

    private void ApplyHistoryResponsiveLayout(double w)
    {
        if (w <= 0)
            return;
        var narrow = w < ResponsiveLayout.NarrowMax;
        if (_histNarrowLayout == narrow && _histRoot.Children.Count > 0)
            return;
        _histNarrowLayout = narrow;

        _histRoot.Children.Clear();
        _histRoot.ColumnDefinitions.Clear();
        _histRoot.RowDefinitions.Clear();

        if (narrow)
        {
            _histRoot.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            _histRoot.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            _histRoot.Children.Add(_leftPane);
            _histRoot.Children.Add(_rightPane);
            Grid.SetRow(_leftPane, 0);
            Grid.SetRow(_rightPane, 1);
            _leftPane.Margin = new Thickness(0, 0, 0, 8);
        }
        else
        {
            _histRoot.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(280) });
            _histRoot.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            _histRoot.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            _histRoot.Children.Add(_leftPane);
            _histRoot.Children.Add(_rightPane);
            Grid.SetColumn(_leftPane, 0);
            Grid.SetColumn(_rightPane, 1);
            _leftPane.Margin = new Thickness(0, 0, 12, 0);
        }
    }

    /// <summary>Reload JSON from disk and refresh the list (same as Avalonia <c>Refresh</c>).</summary>
    public void Refresh()
    {
        var doc = ActionHistoryService.Load();
        _entries = doc.Entries.OrderByDescending(e => e.UtcAt).ToList();
        if (_entries.Count == 0)
        {
            RebuildListPlaceholder("(no entries in action-history.json yet — run a plan/ritual)");
            _histDetail.Text = "";
            return;
        }

        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var q = _histFilter.Text?.Trim() ?? "";
        IEnumerable<ActionHistoryEntry> src = _entries;
        if (!string.IsNullOrEmpty(q))
        {
            src = _entries.Where(e =>
                e.ListLabel.Contains(q, StringComparison.OrdinalIgnoreCase)
                || e.Summary.Contains(q, StringComparison.OrdinalIgnoreCase)
                || e.Kind.Contains(q, StringComparison.OrdinalIgnoreCase));
        }

        var list = src.ToList();
        if (list.Count == 0)
        {
            RebuildListPlaceholder("(no matches)");
            return;
        }

        RebuildListEntries(list);
    }

    private void RebuildListPlaceholder(string message)
    {
        _histList.Items.Clear();
        _histList.Items.Add(new ListViewItem { Content = message, Tag = message });
    }

    private void RebuildListEntries(IReadOnlyList<ActionHistoryEntry> list)
    {
        _histList.Items.Clear();
        foreach (var e in list)
        {
            _histList.Items.Add(new ListViewItem
            {
                Content = e.ListLabel,
                Tag = e
            });
        }
    }

    private void CreateRitualFromSelection()
    {
        ActionHistoryEntry? entry = null;
        if (_histList.SelectedItem is ListViewItem lvi && lvi.Tag is ActionHistoryEntry e)
            entry = e;

        if (entry == null)
        {
            NexusShell.Log("History: select an entry.");
            return;
        }

        if (entry.Steps.Count == 0)
        {
            NexusShell.Log("History: selected entry has no steps.");
            return;
        }

        var recipe = new AutomationRecipe
        {
            Name = $"From history {entry.UtcAt.ToLocalTime():yyyy-MM-dd HH:mm}",
            Description = string.IsNullOrWhiteSpace(entry.Summary)
                ? "From action-history"
                : entry.Summary[..Math.Min(200, entry.Summary.Length)],
            Steps = entry.Steps.Select(s => new RecipeStep
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
