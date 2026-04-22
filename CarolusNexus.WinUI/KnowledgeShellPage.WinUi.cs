using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CarolusNexus;
using CarolusNexus.Services;
using CarolusNexus_WinUI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using VirtualKey = Windows.System.VirtualKey;
using VirtualKeyModifiers = Windows.System.VirtualKeyModifiers;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace CarolusNexus_WinUI.Pages;

/// <summary>Parity with Avalonia <c>KnowledgeTab</c>.</summary>
public sealed class KnowledgeShellPage : Page
{
    private readonly Grid _knowRoot = new();
    private readonly StackPanel _leftPane = new() { Spacing = 8 };
    private readonly StackPanel _rightPane = new() { Spacing = 8 };
    private readonly StackPanel _knowledgeStatus = new() { Orientation = Orientation.Horizontal, Spacing = 10 };
    private readonly Button _nbaPrimary = new();
    private readonly Button _nbaSecondary = new();
    private readonly Button _nbaDismiss = new();
    private Border? _nextBestActionBar;
    private NextBestAction? _nextBestAction;
    private readonly TextBox _search = new() { Header = "Search", PlaceholderText = "Filter by filename…" };
    private readonly ListView _docList = new() { MinHeight = 160, SelectionMode = ListViewSelectionMode.Single };
    private readonly TextBox _preview = new()
    {
        IsReadOnly = true,
        AcceptsReturn = true,
        TextWrapping = TextWrapping.Wrap,
        MinHeight = 200,
        FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas")
    };
    private readonly TextBlock _editState = new() { TextWrapping = TextWrapping.Wrap, FontSize = 12 };
    private readonly Border _knowledgeBusyState = WinUiFluentChrome.BusyState("Knowledge", "Checking documents and index state...");
    private string? _editingPath;
    private string[] _allFiles = Array.Empty<string>();
    private bool? _narrow;

    public KnowledgeShellPage()
    {
        _knowledgeBusyState.Visibility = Visibility.Collapsed;

        var search = WinUiFluentChrome.AppBarCommand("Search", "\uE721", (_, _) => ApplySearch(), "Ctrl+F", VirtualKey.F, VirtualKeyModifiers.Control);
        var import = WinUiFluentChrome.AppBarCommand("Import", "\uE8B5", async (_, _) => await ImportAsync(), "Ctrl+I", VirtualKey.I, VirtualKeyModifiers.Control);
        var remove = WinUiFluentChrome.AppBarCommand("Remove", "\uE74D", (_, _) => RemoveSelected(), "Del", VirtualKey.Delete);
        var reindex = WinUiFluentChrome.AppBarCommand("Reindex", "\uE72C", (_, _) => OnReindex(), "F5", VirtualKey.F5);
        var suggest = WinUiFluentChrome.AppBarCommand("Suggest flow", "\uE9CE", async (_, _) => await SuggestRitualAsync(), "Ctrl+G", VirtualKey.G, VirtualKeyModifiers.Control);
        var edit = WinUiFluentChrome.AppBarCommand("Edit selected", "\uE70F", (_, _) => BeginEditSelected(), "Ctrl+E", VirtualKey.E, VirtualKeyModifiers.Control);
        var save = WinUiFluentChrome.AppBarCommand("Save changes", "\uE74E", (_, _) => SaveEdit(), "Ctrl+S", VirtualKey.S, VirtualKeyModifiers.Control);
        var cancel = WinUiFluentChrome.AppBarCommand("Cancel edit", "\uE711", (_, _) => CancelEdit(), "Esc", VirtualKey.Escape);

        _leftPane.Children.Add(WinUiFluentChrome.PageTitle("Knowledge"));
        var knowHint = new TextBlock
        {
            Text = "Local documents, search, import, and reindex — parity with the Avalonia Knowledge tab.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = WinUiFluentChrome.SecondaryTextBrush
        };
        WinUiFluentChrome.ApplyCaptionTextStyle(knowHint);
        _leftPane.Children.Add(knowHint);
        _leftPane.Children.Add(WinUiFluentChrome.WrapCard(_knowledgeStatus, new Thickness(12, 10, 12, 10)));
        _leftPane.Children.Add(_knowledgeBusyState);
        var knowledgeActionRow = new StackPanel { Spacing = 8 };
        knowledgeActionRow.Children.Add(WinUiFluentChrome.CommandSurface("Knowledge actions", search, import, remove, reindex, suggest));
        _leftPane.Children.Add(WinUiFluentChrome.SectionCard("Knowledge actions", "Search, import, remove, reindex, suggest", knowledgeActionRow));

        var editorActionRow = new StackPanel { Spacing = 8 };
        editorActionRow.Children.Add(WinUiFluentChrome.CommandSurface("Editor", edit, save, cancel));
        _leftPane.Children.Add(WinUiFluentChrome.SectionCard("Editor actions", "Toggle local edit mode for supported text formats", editorActionRow));
        _leftPane.Children.Add(WinUiFluentChrome.SectionCard("Document browser", "Search and select local corpus files", _search));
        _leftPane.Children.Add(WinUiFluentChrome.SectionCard("Files", "Current local knowledge entries", _docList));
        _nextBestActionBar = BuildNextBestActionBar();
        _leftPane.Children.Add(_nextBestActionBar);

        var previewSection = new StackPanel { Spacing = 6 };
        _editState.Foreground = WinUiFluentChrome.SecondaryTextBrush;
        WinUiFluentChrome.ApplyCaptionTextStyle(_editState);
        previewSection.Children.Add(_editState);
        previewSection.Children.Add(_preview);
        _rightPane.Children.Add(WinUiFluentChrome.SectionCard("Preview", "Selected file content and editor output", previewSection));

        _docList.SelectionChanged += OnSel;

        _knowRoot.Children.Add(_leftPane);
        _knowRoot.Children.Add(_rightPane);
        Content = new ScrollViewer { Content = _knowRoot, Padding = new Thickness(20, 16, 20, 16) };

        Loaded += (_, _) =>
        {
            RefreshList();
            RefreshNextBestActionBar();
            ApplyLayout(ActualWidth);
        };
        SizeChanged += (_, e) => ApplyLayout(e.NewSize.Width);
    }

    private void ApplyLayout(double w)
    {
        if (w <= 0)
            return;
        var narrow = w < ResponsiveLayout.NarrowMax;
        if (_narrow == narrow)
            return;
        _narrow = narrow;

        _knowRoot.Children.Clear();
        _knowRoot.ColumnDefinitions.Clear();
        _knowRoot.RowDefinitions.Clear();

        if (narrow)
        {
            _knowRoot.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            _knowRoot.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            _knowRoot.Children.Add(_leftPane);
            _knowRoot.Children.Add(_rightPane);
            Grid.SetRow(_leftPane, 0);
            Grid.SetRow(_rightPane, 1);
            _leftPane.Margin = new Thickness(0, 0, 0, 8);
        }
        else
        {
            _knowRoot.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(380) });
            _knowRoot.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            _knowRoot.Children.Add(_leftPane);
            _knowRoot.Children.Add(_rightPane);
            Grid.SetColumn(_leftPane, 0);
            Grid.SetColumn(_rightPane, 1);
            _leftPane.Margin = new Thickness(0, 0, 12, 0);
        }
    }

    public void RefreshList()
    {
        _knowledgeBusyState.Visibility = Visibility.Visible;
        Directory.CreateDirectory(AppPaths.KnowledgeDir);
        _allFiles = Directory.GetFiles(AppPaths.KnowledgeDir, "*.*", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileName).Where(f => f != null).Select(f => f!).ToArray();
        RefreshKnowledgeStatus();
        ApplySearch();
        _knowledgeBusyState.Visibility = Visibility.Collapsed;
    }

    private void RefreshKnowledgeStatus()
    {
        _knowledgeStatus.Children.Clear();
        var indexState = File.Exists(AppPaths.KnowledgeIndex) ? "index ready" : "index missing";
        var embeddingsState = File.Exists(AppPaths.KnowledgeEmbeddings) ? "embeddings ready" : "optional";
        _knowledgeStatus.Children.Add(WinUiFluentChrome.StatusTile("Documents", _allFiles.Length.ToString(), "local RAG corpus"));
        _knowledgeStatus.Children.Add(WinUiFluentChrome.StatusTile("Index", indexState, AppPaths.KnowledgeIndex));
        _knowledgeStatus.Children.Add(WinUiFluentChrome.StatusTile("Semantic RAG", embeddingsState, "OpenAI key dependent"));
    }

    private Border BuildNextBestActionBar()
    {
        _nbaPrimary.Click += (_, _) =>
        {
            NexusShell.Log("Next action: ask with local knowledge from the Ask page.");
        };
        _nbaSecondary.Click += (_, _) => OnReindex();
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
        NexusShell.Log("Knowledge next action refreshed: " + _nextBestAction.Message);
    }

    private void ApplySearch()
    {
        var q = _search.Text?.Trim() ?? "";
        var src = string.IsNullOrEmpty(q)
            ? _allFiles
            : _allFiles.Where(f => f.Contains(q, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (src.Length == 0)
        {
            _docList.Items.Clear();
            _docList.Items.Add(new ListViewItem
            {
                Content = WinUiFluentChrome.EmptyState("No documents", "No files match the current filter.", "Import files or change the search term."),
                IsHitTestVisible = false
            });
            return;
        }
        _docList.ItemsSource = src;
    }

    private async Task ImportAsync()
    {
        _knowledgeBusyState.Visibility = Visibility.Visible;
        var w = WinUiShellState.MainWindowRef;
        if (w == null)
        {
            NexusShell.Log("Import: no main window handle.");
            _knowledgeBusyState.Visibility = Visibility.Collapsed;
            return;
        }

        var p = new FileOpenPicker();
        foreach (var ext in new[]
                 {
                     ".txt", ".md", ".pdf", ".docx", ".xlsx", ".pptx", ".csv", ".json", ".xml", ".html", ".htm"
                 })
            p.FileTypeFilter.Add(ext);
        p.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
        InitializeWithWindow.Initialize(p, WindowNative.GetWindowHandle(w));
        var files = await p.PickMultipleFilesAsync();
        if (files == null || files.Count == 0)
            return;

        Directory.CreateDirectory(AppPaths.KnowledgeDir);
        foreach (var f in files)
        {
            try
            {
                var path = f.Path;
                if (string.IsNullOrEmpty(path))
                    continue;
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
        _knowledgeBusyState.Visibility = Visibility.Collapsed;
    }

    private void RemoveSelected()
    {
        _knowledgeBusyState.Visibility = Visibility.Visible;
        if (_docList.SelectedItem is not string name)
        {
            _knowledgeBusyState.Visibility = Visibility.Collapsed;
            return;
        }
        try
        {
            var path = Path.Combine(AppPaths.KnowledgeDir, name);
            if (File.Exists(path))
                File.Delete(path);
            NexusShell.Log("Removed: " + name);
            RefreshList();
            _preview.Text = "";
            _editingPath = null;
            UpdateEditState();
        }
        catch (Exception ex)
        {
            NexusShell.Log("Delete error: " + ex.Message);
        }
        finally
        {
            _knowledgeBusyState.Visibility = Visibility.Collapsed;
        }
    }

    private void OnReindex()
    {
        _knowledgeBusyState.Visibility = Visibility.Visible;
        try
        {
            KnowledgeIndexService.Rebuild();
            RefreshList();
            _ = EmbeddingRagService.RebuildIfConfiguredAsync(default);
            NexusShell.Log("Knowledge reindex → index + chunks; embeddings in background (if configured).");
        }
        finally
        {
            _knowledgeBusyState.Visibility = Visibility.Collapsed;
        }
    }

    private async Task SuggestRitualAsync()
    {
        if (_docList.SelectedItem is not string name)
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
            "From the following document, extract a short operator-flow checklist as **only** a JSON array " +
            "of objects {\"actionType\":\"token\",\"actionArgument\":\"…\",\"waitMs\":0}. " +
            "actionArgument: concrete steps or [ACTION:hotkey|Ctrl+S] style. Max. 12 steps. No markdown.\n\n" +
            doc;

        try
        {
            var s = WinUiShellState.Settings;
            NexusShell.Log("LLM: flow suggestion …");
            var json = await LlmChatService.CompleteAsync(s, prompt, false, false).ConfigureAwait(true);
            NexusShell.Log("Flow suggestion ready — paste into Operator flows.");
            _preview.Text = "Suggestion (JSON):\n\n" + json;
        }
        catch (Exception ex)
        {
            NexusShell.Log("suggest flow: " + ex.Message);
        }
    }

    private void OnSel(object sender, SelectionChangedEventArgs e)
    {
        if (_docList.SelectedItem is not string fname)
            return;
        var path = Path.Combine(AppPaths.KnowledgeDir, fname);
        _editingPath = null;
        _preview.IsReadOnly = true;
        _preview.Text = KnowledgeIndexService.ReadDocumentForPreview(path);
        UpdateEditState();
    }

    private void BeginEditSelected()
    {
        if (_docList.SelectedItem is not string name)
        {
            NexusShell.Log("Edit: select a knowledge document first.");
            return;
        }

        var path = Path.Combine(AppPaths.KnowledgeDir, name);
        if (!IsEditableKnowledgeFile(path))
        {
            NexusShell.Log("Edit: this format is preview-only. Use text/markdown/json/csv/xml/html for direct editing.");
            return;
        }

        try
        {
            _preview.Text = File.ReadAllText(path, Encoding.UTF8);
            _preview.IsReadOnly = false;
            _editingPath = path;
            UpdateEditState();
            NexusShell.Log("Edit mode: " + name);
        }
        catch (Exception ex)
        {
            NexusShell.Log("Edit failed: " + ex.Message);
        }
    }

    private void SaveEdit()
    {
        if (string.IsNullOrWhiteSpace(_editingPath))
        {
            NexusShell.Log("Save edit: no active edit.");
            return;
        }

        try
        {
            File.WriteAllText(_editingPath, _preview.Text ?? "", Encoding.UTF8);
            _preview.IsReadOnly = true;
            var saved = Path.GetFileName(_editingPath);
            _editingPath = null;
            RefreshList();
            UpdateEditState();
            NexusShell.Log("Knowledge saved: " + saved + " — reindex recommended.");
        }
        catch (Exception ex)
        {
            NexusShell.Log("Save edit failed: " + ex.Message);
        }
    }

    private void CancelEdit()
    {
        if (string.IsNullOrWhiteSpace(_editingPath))
            return;
        var path = _editingPath;
        _editingPath = null;
        _preview.IsReadOnly = true;
        _preview.Text = KnowledgeIndexService.ReadDocumentForPreview(path);
        UpdateEditState();
        NexusShell.Log("Knowledge edit canceled.");
    }

    private void UpdateEditState()
    {
        _editState.Text = string.IsNullOrWhiteSpace(_editingPath)
            ? "Preview mode. Select a text document and choose Edit selected to modify it locally."
            : "Edit mode: " + Path.GetFileName(_editingPath) + " — Save changes writes the local file.";
    }

    private static bool IsEditableKnowledgeFile(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext is ".txt" or ".md" or ".csv" or ".json" or ".xml" or ".html" or ".htm" or ".log";
    }
}
