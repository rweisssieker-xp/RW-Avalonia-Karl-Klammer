using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CarolusNexus;
using CarolusNexus.Services;
using CarolusNexus_WinUI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
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
    private string[] _allFiles = Array.Empty<string>();
    private bool? _narrow;

    public KnowledgeShellPage()
    {
        var search = WinUiFluentChrome.AppBarCommand("Search", "\uE721", (_, _) => ApplySearch());
        var import = WinUiFluentChrome.AppBarCommand("Import", "\uE8B5", async (_, _) => await ImportAsync());
        var remove = WinUiFluentChrome.AppBarCommand("Remove", "\uE74D", (_, _) => RemoveSelected());
        var reindex = WinUiFluentChrome.AppBarCommand("Reindex", "\uE72C", (_, _) => OnReindex());
        var suggest = WinUiFluentChrome.AppBarCommand("Suggest flow", "\uE9CE", async (_, _) => await SuggestRitualAsync());

        _leftPane.Children.Add(WinUiFluentChrome.PageTitle("Knowledge"));
        var knowHint = new TextBlock
        {
            Text = "Local documents, search, import, and reindex — parity with the Avalonia Knowledge tab.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = WinUiFluentChrome.SecondaryTextBrush
        };
        WinUiFluentChrome.ApplyCaptionTextStyle(knowHint);
        _leftPane.Children.Add(knowHint);
        _nextBestActionBar = BuildNextBestActionBar();
        _leftPane.Children.Add(_nextBestActionBar);
        _leftPane.Children.Add(_knowledgeStatus);
        _leftPane.Children.Add(_search);
        _leftPane.Children.Add(WinUiFluentChrome.CommandSurface("Knowledge actions", search, import, remove, reindex, suggest));
        _leftPane.Children.Add(_docList);

        _rightPane.Children.Add(WinUiFluentChrome.ColumnCaption("Preview"));
        _rightPane.Children.Add(_preview);

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
            _knowRoot.RowDefinitions.Add(new RowDefinition { Height = new GridLength(220) });
            _knowRoot.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            _knowRoot.Children.Add(_leftPane);
            _knowRoot.Children.Add(_rightPane);
            Grid.SetRow(_leftPane, 0);
            Grid.SetRow(_rightPane, 1);
            _leftPane.Margin = new Thickness(0, 0, 0, 8);
        }
        else
        {
            _knowRoot.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(260) });
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
        Directory.CreateDirectory(AppPaths.KnowledgeDir);
        _allFiles = Directory.GetFiles(AppPaths.KnowledgeDir, "*.*", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileName).Where(f => f != null).Select(f => f!).ToArray();
        RefreshKnowledgeStatus();
        ApplySearch();
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
        if (_nextBestActionBar == null)
            return;
        var parent = _nextBestActionBar.Parent as StackPanel;
        var index = parent?.Children.IndexOf(_nextBestActionBar) ?? -1;
        var visible = _nextBestActionBar.Visibility;
        parent?.Children.Remove(_nextBestActionBar);
        _nextBestActionBar = WinUiFluentChrome.NextBestActionBar(_nextBestAction, _nbaPrimary, _nbaSecondary, _nbaDismiss);
        _nextBestActionBar.Visibility = visible == Visibility.Collapsed ? Visibility.Collapsed : Visibility.Visible;
        if (parent != null && index >= 0)
            parent.Children.Insert(index, _nextBestActionBar);
    }

    private void ApplySearch()
    {
        var q = _search.Text?.Trim() ?? "";
        var src = string.IsNullOrEmpty(q)
            ? _allFiles
            : _allFiles.Where(f => f.Contains(q, StringComparison.OrdinalIgnoreCase)).ToArray();
        _docList.ItemsSource = src;
    }

    private async Task ImportAsync()
    {
        var w = WinUiShellState.MainWindowRef;
        if (w == null)
        {
            NexusShell.Log("Import: no main window handle.");
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
    }

    private void RemoveSelected()
    {
        if (_docList.SelectedItem is not string name)
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
        _preview.Text = "";
    }

    private void OnReindex()
    {
        KnowledgeIndexService.Rebuild();
        RefreshList();
        _ = EmbeddingRagService.RebuildIfConfiguredAsync(default);
        NexusShell.Log("Knowledge reindex → index + chunks; embeddings in background (if configured).");
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
        _preview.Text = KnowledgeIndexService.ReadDocumentForPreview(path);
    }
}
