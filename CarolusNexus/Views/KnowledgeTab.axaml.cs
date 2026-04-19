using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using CarolusNexus;
using CarolusNexus.Services;

namespace CarolusNexus.Views;

public partial class KnowledgeTab : UserControl
{
    private string[] _allFiles = Array.Empty<string>();

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
            NexusShell.Log("Knowledge-Reindex → knowledge-index.json");
        };
        BtnSuggestRitual.Click += async (_, _) => await SuggestRitualAsync();
        DocList.SelectionChanged += OnSel;
        RefreshList();
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
            Title = "Dateien nach knowledge importieren",
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
                NexusShell.Log("Import Fehler: " + ex.Message);
            }
        }

        RefreshList();
        NexusShell.Log($"Import fertig ({files.Count} Dateien).");
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
            NexusShell.Log("Entfernt: " + name);
        }
        catch (Exception ex)
        {
            NexusShell.Log("Löschen Fehler: " + ex.Message);
        }

        RefreshList();
        Preview.Text = "";
    }

    private async Task SuggestRitualAsync()
    {
        if (DocList.SelectedItem is not string name)
        {
            NexusShell.Log("Bitte ein Dokument wählen.");
            return;
        }

        var path = Path.Combine(AppPaths.KnowledgeDir, name);
        string doc;
        try
        {
            doc = File.ReadAllText(path);
        }
        catch
        {
            NexusShell.Log("Datei nicht lesbar (evtl. binär).");
            return;
        }

        if (doc.Length > 14_000)
            doc = doc[..14_000] + "\n…";

        var prompt =
            "Extrahiere aus dem folgenden Dokument eine kurze Ritual-Checkliste als **nur** JSON-Array " +
            "von Objekten {\"actionType\":\"token\",\"actionArgument\":\"…\",\"waitMs\":0}. " +
            "actionArgument: konkrete Schritte oder [ACTION:hotkey|Ctrl+S] Stil. Max. 12 Schritte. Kein Markdown.\n\n" +
            doc;

        try
        {
            var s = NexusContext.GetSettings();
            NexusShell.Log("LLM: Ritual-Vorschlag …");
            var json = await LlmChatService.CompleteAsync(s, prompt, false, false).ConfigureAwait(true);
            NexusShell.Log("Ritual-Vorschlag fertig — in Rituals-Tab einfügen.");
            // Optional: Zwischenablage wäre möglich; Nutzer kopiert aus Log/Preview
            Preview.Text = "Vorschlag (JSON):\n\n" + json;
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
        try
        {
            Preview.Text = File.ReadAllText(path);
        }
        catch
        {
            Preview.Text = "(Binär oder nicht lesbar — PDF/DOCX-Parser Stub.)";
        }
    }
}
