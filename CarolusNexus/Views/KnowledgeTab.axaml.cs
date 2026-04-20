using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using CarolusNexus;
using CarolusNexus.Services;

namespace CarolusNexus.Views;

public partial class KnowledgeTab : UserControl
{
    private string[] _allFiles = Array.Empty<string>();
    private bool? _knowNarrowLayout;

    public KnowledgeTab()
    {
        InitializeComponent();
        BtnSearch.Click += (_, _) => ApplySearch();
        BtnImport.Click += async (_, _) => await ImportAsync();
        BtnRemove.Click += (_, _) => RemoveSelected();
        BtnReindex.Click += (_, _) =>
        {
            KnowledgeIndexService.Rebuild();
            RefreshList();
            _ = EmbeddingRagService.RebuildIfConfiguredAsync(default);
            NexusShell.Log("Knowledge reindex → index + chunks; embeddings in background (if configured).");
        };
        BtnSuggestRitual.Click += async (_, _) => await SuggestRitualAsync();
        DocList.SelectionChanged += OnSel;
        RefreshList();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        LayoutUpdated += KnowOnLayoutUpdated;
        ApplyKnowledgeResponsiveLayout();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        LayoutUpdated -= KnowOnLayoutUpdated;
        base.OnDetachedFromVisualTree(e);
    }

    private void KnowOnLayoutUpdated(object? sender, EventArgs e) => ApplyKnowledgeResponsiveLayout();

    private void ApplyKnowledgeResponsiveLayout()
    {
        var w = Bounds.Width;
        if (w <= 0)
            return;
        var narrow = w < ResponsiveLayout.NarrowMax;
        if (_knowNarrowLayout == narrow)
            return;
        _knowNarrowLayout = narrow;

        KnowRootGrid.ColumnDefinitions.Clear();
        KnowRootGrid.RowDefinitions.Clear();
        if (narrow)
        {
            KnowRootGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            KnowRootGrid.RowDefinitions.Add(new RowDefinition(new GridLength(220)));
            KnowRootGrid.RowDefinitions.Add(new RowDefinition(GridLength.Star));
            Grid.SetColumn(KnowLeftPane, 0);
            Grid.SetRow(KnowLeftPane, 0);
            Grid.SetColumn(KnowRightPane, 0);
            Grid.SetRow(KnowRightPane, 1);
            KnowLeftPane.Margin = new Thickness(0, 0, 0, 8);
        }
        else
        {
            KnowRootGrid.RowDefinitions.Add(new RowDefinition(GridLength.Star));
            KnowRootGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(240)));
            KnowRootGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            Grid.SetColumn(KnowLeftPane, 0);
            Grid.SetRow(KnowLeftPane, 0);
            Grid.SetColumn(KnowRightPane, 1);
            Grid.SetRow(KnowRightPane, 0);
            KnowLeftPane.Margin = new Thickness(0, 0, 12, 0);
        }
    }

    public void RefreshList()
    {
        Directory.CreateDirectory(AppPaths.KnowledgeDir);
        _allFiles = Directory.GetFiles(AppPaths.KnowledgeDir, "*.*", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileName).Where(f => f != null).Select(f => f!).ToArray();
        ApplySearch();
    }

    private void ApplySearch()
    {
        var q = SearchQuery.Text?.Trim() ?? "";
        var src = string.IsNullOrEmpty(q)
            ? _allFiles
            : _allFiles.Where(f => f.Contains(q, StringComparison.OrdinalIgnoreCase)).ToArray();
        DocList.ItemsSource = src;
    }

    private async Task ImportAsync()
    {
        var top = TopLevel.GetTopLevel(this);
        if (top?.StorageProvider is not { } sp)
            return;
        var files = await sp.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Import files into knowledge",
            AllowMultiple = true,
        }).ConfigureAwait(true);
        if (files.Count == 0)
            return;
        Directory.CreateDirectory(AppPaths.KnowledgeDir);
        foreach (var f in files)
        {
            try
            {
                var path = f.Path.LocalPath;
                var name = Path.GetFileName(path);
                if (string.IsNullOrEmpty(name))
                    continue;
                var dest = Path.Combine(AppPaths.KnowledgeDir, name);
                File.Copy(path, dest, overwrite: true);
            }
            catch (Exception ex)
            {
                NexusShell.Log("Import error: " + ex.Message);
            }
        }

        RefreshList();
        NexusShell.Log($"Import done ({files.Count} files).");
    }

    private void RemoveSelected()
    {
        if (DocList.SelectedItem is not string name)
            return;
        var path = Path.Combine(AppPaths.KnowledgeDir, name);
        try
        {
            if (File.Exists(path))
                File.Delete(path);
            NexusShell.Log("Removed: " + name);
        }
        catch (Exception ex)
        {
            NexusShell.Log("Delete error: " + ex.Message);
        }

        RefreshList();
        Preview.Text = "";
    }

    private async Task SuggestRitualAsync()
    {
        if (DocList.SelectedItem is not string name)
        {
            NexusShell.Log("Please select a document.");
            return;
        }

        var path = Path.Combine(AppPaths.KnowledgeDir, name);
        var doc = KnowledgeIndexService.ReadDocumentForPreview(path, 14_000);
        if (string.IsNullOrWhiteSpace(doc) || doc.StartsWith("(No text preview", StringComparison.Ordinal) ||
            doc.StartsWith("(File missing", StringComparison.Ordinal) ||
            doc.StartsWith("Read failed", StringComparison.Ordinal))
        {
            NexusShell.Log("File not usable as text (format or empty).");
            return;
        }

        var prompt =
            "From the following document, extract a short ritual checklist as **only** a JSON array " +
            "of objects {\"actionType\":\"token\",\"actionArgument\":\"…\",\"waitMs\":0}. " +
            "actionArgument: concrete steps or [ACTION:hotkey|Ctrl+S] style. Max. 12 steps. No markdown.\n\n" +
            doc;

        try
        {
            var s = NexusContext.GetSettings();
            NexusShell.Log("LLM: ritual suggestion …");
            var json = await LlmChatService.CompleteAsync(s, prompt, false, false).ConfigureAwait(true);
            NexusShell.Log("Ritual suggestion ready — paste into Rituals tab.");
            // Optional: clipboard possible; user copies from log/preview
            Preview.Text = "Suggestion (JSON):\n\n" + json;
        }
        catch (Exception ex)
        {
            NexusShell.Log("suggest ritual: " + ex.Message);
        }
    }

    private void OnSel(object? s, SelectionChangedEventArgs e)
    {
        if (DocList.SelectedItem is not string fname)
            return;
        var path = Path.Combine(AppPaths.KnowledgeDir, fname);
        Preview.Text = KnowledgeIndexService.ReadDocumentForPreview(path);
    }
}
