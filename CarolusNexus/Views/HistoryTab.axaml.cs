using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Interactivity;
using CarolusNexus.Models;
using CarolusNexus.Services;

namespace CarolusNexus.Views;

public partial class HistoryTab : UserControl
{
    private List<ActionHistoryEntry> _entries = new();

    public HistoryTab()
    {
        InitializeComponent();
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

    public void Refresh()
    {
        var doc = ActionHistoryService.Load();
        _entries = doc.Entries.OrderByDescending(e => e.UtcAt).ToList();
        if (_entries.Count == 0)
        {
            HistList.ItemsSource = new[] { "(noch keine Einträge in action-history.json — Plan/Ritual-Lauf ausführen)" };
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
        HistList.ItemsSource = list.Count == 0 ? new[] { "(keine Treffer)" } : list;
    }

    private void CreateRitualFromSelection()
    {
        if (HistList.SelectedItem is not ActionHistoryEntry e)
        {
            NexusShell.Log("History: Eintrag wählen.");
            return;
        }

        if (e.Steps.Count == 0)
        {
            NexusShell.Log("History: gewählter Eintrag hat keine Schritte.");
            return;
        }

        var recipe = new AutomationRecipe
        {
            Name = $"Aus History {e.UtcAt.ToLocalTime():yyyy-MM-dd HH:mm}",
            Description = string.IsNullOrWhiteSpace(e.Summary) ? "Aus action-history" : e.Summary[..Math.Min(200, e.Summary.Length)],
            Steps = e.Steps.Select(s => new RecipeStep
            {
                ActionType = s.ActionType,
                ActionArgument = s.ActionArgument,
                WaitMs = s.WaitMs
            }).ToList()
        };
        RitualRecipeStore.AppendRecipe(recipe);
        NexusShell.Log($"Ritual aus History angelegt: {recipe.Name} ({recipe.Steps.Count} Schritte).");
    }
}
