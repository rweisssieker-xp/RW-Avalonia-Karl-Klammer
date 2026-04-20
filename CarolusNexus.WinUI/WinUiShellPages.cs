using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CarolusNexus;
using CarolusNexus.Experiments;
using CarolusNexus.Models;
using CarolusNexus.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
namespace CarolusNexus_WinUI.Pages;

public sealed class AskShellPage : Page
{
    private readonly TextBox _prompt = new() { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 140 };
    private readonly TextBox _out = new() { IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 200 };
    private readonly CheckBox _shots = new() { Content = "Screenshots" };
    private readonly CheckBox _know = new() { Content = "Knowledge", IsChecked = true };
    private readonly InfoBar _busy = new() { IsOpen = false, Title = "Busy" };
    private readonly Grid _root = new() { Padding = new Thickness(12) };
    private readonly StackPanel _toolbar;
    private readonly ScrollViewer _scrollL;
    private readonly ScrollViewer _scrollR;
    private ResponsiveBand? _band;
    private CancellationTokenSource? _cts;

    public AskShellPage()
    {
        _toolbar = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var ask = new Button { Content = "Ask now" };
        ask.Click += async (_, _) => await RunAskAsync();
        var smoke = new Button { Content = "Smoke LLM" };
        smoke.Click += async (_, _) =>
        {
            _busy.IsOpen = true;
            try
            {
                _out.Text = await LlmChatService.SmokeAsync(WinUiShellState.Settings);
            }
            catch (Exception ex)
            {
                _out.Text = ex.Message;
            }
            finally
            {
                _busy.IsOpen = false;
            }
        };
        _toolbar.Children.Add(ask);
        _toolbar.Children.Add(smoke);
        _toolbar.Children.Add(_shots);
        _toolbar.Children.Add(_know);
        _scrollL = new ScrollViewer { Content = _prompt };
        _scrollR = new ScrollViewer { Content = _out };
        Content = _root;
        SizeChanged += (_, e) => ApplyLayout(e.NewSize.Width);
        Loaded += (_, _) => ApplyLayout(ActualWidth);
    }

    private void ApplyLayout(double w)
    {
        if (w <= 0)
            return;
        var band = ResponsiveLayout.GetBand(w);
        if (_band == band && _root.Children.Count > 0)
            return;
        _band = band;
        _root.Children.Clear();
        _root.RowDefinitions.Clear();
        _root.ColumnDefinitions.Clear();
        if (band == ResponsiveBand.Narrow)
        {
            _root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            _root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            _root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            _root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            _root.Children.Add(_toolbar);
            _root.Children.Add(_busy);
            _root.Children.Add(_scrollL);
            _root.Children.Add(_scrollR);
            Grid.SetRow(_toolbar, 0);
            Grid.SetRow(_busy, 1);
            Grid.SetRow(_scrollL, 2);
            Grid.SetRow(_scrollR, 3);
        }
        else
        {
            _root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            _root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            _root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            _root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            _root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            _root.Children.Add(_toolbar);
            _root.Children.Add(_busy);
            _root.Children.Add(_scrollL);
            _root.Children.Add(_scrollR);
            Grid.SetRow(_toolbar, 0);
            Grid.SetColumnSpan(_toolbar, 2);
            Grid.SetRow(_busy, 1);
            Grid.SetColumnSpan(_busy, 2);
            Grid.SetRow(_scrollL, 2);
            Grid.SetColumn(_scrollL, 0);
            Grid.SetRow(_scrollR, 2);
            Grid.SetColumn(_scrollR, 1);
        }
    }

    private async Task RunAskAsync()
    {
        var prompt = _prompt.Text?.Trim();
        if (string.IsNullOrEmpty(prompt))
        {
            _out.Text = "Enter a prompt.";
            return;
        }

        var s = WinUiShellState.Settings;
        _busy.IsOpen = true;
        try
        {
            _cts = new CancellationTokenSource();
            var ct = _cts.Token;
            var shots = _shots.IsChecked == true;
            var know = _know.IsChecked == true;
            string? knowOverride = null;
            if (know)
            {
                var bundle = KnowledgeSnippetService.BuildContextBundle(prompt, 6000);
                knowOverride = bundle.ContextText;
            }

            var fusion = UiAutomationVisionFusion.BuildAskAugmentation(s, shots);
            var adapt = OperatorAdapterRegistry.TryEnrichForegroundContext();
            if (!string.IsNullOrWhiteSpace(adapt))
                fusion = string.IsNullOrWhiteSpace(fusion) ? adapt : fusion + "\n\n" + adapt;
            var effective = string.IsNullOrWhiteSpace(fusion) ? prompt : fusion + "\n\n---\n" + prompt;
            var text = await LlmChatService.CompleteAsync(s, effective, shots, know, ct, know ? knowOverride : null);
            var tokens = ActionPlanExtractor.Extract(text);
            var preview = ActionPlanExtractor.FormatPreview(tokens);
            _out.Text = text + (tokens.Count > 0 ? "\n\n--- Plan tokens ---\n" + preview : "");
        }
        catch (Exception ex)
        {
            _out.Text = "Error: " + ex.Message;
        }
        finally
        {
            _busy.IsOpen = false;
        }
    }
}

public sealed class DashboardShellPage : Page
{
    private readonly Grid _grid = new() { Margin = new Thickness(12) };
    private readonly TextBlock _summary = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 12) };
    private ResponsiveBand? _dashBand;

    public DashboardShellPage()
    {
        var header = new StackPanel { Spacing = 8, Margin = new Thickness(0, 0, 0, 8) };
        header.Children.Add(new TextBlock { Text = "Dashboard", FontSize = 18, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        var btn = new Button { Content = "Refresh", HorizontalAlignment = HorizontalAlignment.Left };
        btn.Click += (_, _) => RefreshSummary();
        header.Children.Add(btn);
        header.Children.Add(_summary);

        _grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        Grid.SetRow(header, 0);
        _grid.Children.Add(header);

        Content = _grid;
        SizeChanged += (_, e) => ApplyDashboardTiles(e.NewSize.Width);
        Loaded += (_, _) =>
        {
            RefreshSummary();
            ApplyDashboardTiles(ActualWidth);
        };
    }

    private void RefreshSummary()
    {
        var knowCount = Directory.Exists(AppPaths.KnowledgeDir)
            ? Directory.GetFiles(AppPaths.KnowledgeDir).Length
            : 0;
        var idx = File.Exists(AppPaths.KnowledgeIndex);
        var ch = File.Exists(AppPaths.KnowledgeChunks);
        var fts = File.Exists(AppPaths.KnowledgeFtsDb);
        var emb = File.Exists(AppPaths.KnowledgeEmbeddings);
        _summary.Text =
            $"Knowledge files: {knowCount} · Index: {(idx ? "yes" : "no")} · Chunks: {(ch ? "yes" : "no")} · FTS5: {(fts ? "yes" : "no")} · Embeddings: {(emb ? "yes" : "no")}\n" +
            "Recent log:\n" + NexusShell.FormatRecentLogForDashboard(10)
            + "\n\nRepo: " + AppPaths.RepoRoot;
    }

    private void ApplyDashboardTiles(double w)
    {
        if (w <= 0)
            return;
        var band = ResponsiveLayout.GetBand(w);
        var cols = band == ResponsiveBand.Narrow ? 1 : band == ResponsiveBand.Medium ? 2 : 3;
        if (_dashBand == band && _grid.Children.Count > 1)
            return;
        _dashBand = band;

        while (_grid.Children.Count > 1)
            _grid.Children.RemoveAt(_grid.Children.Count - 1);
        _grid.ColumnDefinitions.Clear();

        var tiles = new Grid { Margin = new Thickness(0, 8, 0, 0) };
        for (var c = 0; c < cols; c++)
            tiles.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var t1 = MkTile("Operator", "Memory & recent activity");
        var t2 = MkTile("Layout", $"Current band: {band} (Narrow < {ResponsiveLayout.NarrowMax}px, Medium < {ResponsiveLayout.MediumMax}px).");
        var t3 = MkTile("Environment", AppPaths.RepoRoot);
        Grid.SetColumn(t1, 0);
        tiles.Children.Add(t1);
        if (cols >= 2)
        {
            Grid.SetColumn(t2, 1);
            tiles.Children.Add(t2);
        }

        if (cols >= 3)
        {
            Grid.SetColumn(t3, 2);
            tiles.Children.Add(t3);
        }
        else if (cols == 2)
        {
            Grid.SetRow(t3, 1);
            Grid.SetColumnSpan(t3, 2);
            tiles.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            tiles.Children.Add(t3);
        }
        else
        {
            tiles.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            tiles.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Grid.SetRow(t2, 1);
            Grid.SetRow(t3, 2);
            tiles.Children.Add(t2);
            tiles.Children.Add(t3);
        }

        Grid.SetRow(tiles, 1);
        _grid.Children.Add(tiles);
    }

    private static Border MkTile(string title, string body) =>
        new()
        {
            Padding = new Thickness(12),
            Margin = new Thickness(4),
            CornerRadius = new CornerRadius(6),
            BorderThickness = new Thickness(1),
            Child = new StackPanel
            {
                Spacing = 4,
                Children =
                {
                    new TextBlock { Text = title, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold },
                    new TextBlock { Text = body, TextWrapping = TextWrapping.Wrap, Opacity = 0.9 }
                }
            }
        };
}

public sealed class SetupShellPage : Page
{
    private readonly ComboBox _provider = new() { Header = "Provider" };
    private readonly ComboBox _mode = new() { Header = "Mode" };
    private readonly TextBox _model = new() { Header = "Model" };
    private readonly ComboBox _uiTheme = new() { Header = "UI theme (WinUI)" };
    private readonly CheckBox _speak = new() { Content = "speak responses" };
    private readonly CheckBox _useKnow = new() { Content = "use local knowledge", IsChecked = true };
    private readonly CheckBox _suggestAuto = new() { Content = "suggest automations" };
    private readonly CheckBox _uia = new() { Content = "Ask: UIA snapshot of foreground window (Windows)" };
    private readonly CheckBox _mem = new() { Content = "Conversation memory" };
    private readonly TextBox _memChars = new() { Header = "Memory max chars" };
    private readonly CheckBox _hi = new() { Content = "High-risk plans: second confirmation", IsChecked = true };
    private readonly ComboBox _safety = new() { Header = "Safety profile" };
    private readonly CheckBox _neverSend = new() { Content = "never auto-send" };
    private readonly CheckBox _neverPost = new() { Content = "never auto-post / book" };
    private readonly CheckBox _panic = new() { Content = "panic stop enabled" };
    private readonly TextBox _denylist = new() { Header = "Denylist (comma-separated)" };
    private readonly TextBox _watchIv = new() { Header = "Watch interval (s)" };
    private readonly CheckBox _proactive = new() { Content = "Proactive LLM hint (Dashboard, watch mode)" };
    private readonly TextBox _proactiveIv = new() { Header = "LLM min interval (s)" };
    private readonly CheckBox _toolHost = new() { Content = "Start local tool host (127.0.0.1)" };
    private readonly TextBox _toolPort = new() { Header = "Tool host port" };
    private readonly TextBox _envSummary = new()
    {
        IsReadOnly = true,
        AcceptsReturn = true,
        MinHeight = 100,
        FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas"),
        TextWrapping = TextWrapping.Wrap
    };
    private readonly TextBlock _envPath = new() { FontSize = 11, Opacity = 0.85, TextWrapping = TextWrapping.Wrap };
    private readonly InfoBar _bar = new() { IsOpen = false };

    public SetupShellPage()
    {
        foreach (var p in new[] { "anthropic", "openai", "openai-compatible" })
            _provider.Items.Add(p);
        foreach (var m in new[] { "companion", "agent", "automation", "watch" })
            _mode.Items.Add(m);
        foreach (var t in new[] { "Dark", "Light", "Default" })
            _uiTheme.Items.Add(t);
        foreach (var x in new[] { "strict", "balanced", "power-user" })
            _safety.Items.Add(x);

        var sp = new StackPanel { Spacing = 10, Margin = new Thickness(12), MaxWidth = 820 };
        sp.Children.Add(new TextBlock { Text = "Setup (aligned with Avalonia §5.4)", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, FontSize = 14 });
        sp.Children.Add(_bar);
        sp.Children.Add(_provider);
        sp.Children.Add(_mode);
        sp.Children.Add(_model);
        sp.Children.Add(_uiTheme);
        sp.Children.Add(_speak);
        sp.Children.Add(_useKnow);
        sp.Children.Add(_suggestAuto);
        sp.Children.Add(_uia);
        sp.Children.Add(_mem);
        sp.Children.Add(_memChars);
        sp.Children.Add(_hi);
        sp.Children.Add(_safety);
        sp.Children.Add(_neverSend);
        sp.Children.Add(_neverPost);
        sp.Children.Add(_panic);
        sp.Children.Add(_denylist);
        sp.Children.Add(new TextBlock { Text = "Watch & tool host", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Margin = new Thickness(0, 8, 0, 0) });
        sp.Children.Add(_watchIv);
        sp.Children.Add(_proactive);
        sp.Children.Add(_proactiveIv);
        sp.Children.Add(_toolHost);
        sp.Children.Add(_toolPort);
        sp.Children.Add(new TextBlock { Text = ".env overview (keys only)", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Margin = new Thickness(0, 8, 0, 0) });
        sp.Children.Add(_envSummary);
        sp.Children.Add(_envPath);

        var save = new Button { Content = "Save settings" };
        save.Click += (_, _) =>
        {
            var s = Gather();
            WinUiShellState.SettingsStore.Save(s);
            WinUiShellState.Settings = s;
            WinUiThemeApplier.Apply(s.UiTheme);
            _bar.Severity = InfoBarSeverity.Success;
            _bar.Message = "Saved.";
            _bar.IsOpen = true;
            NexusShell.Log("settings.json saved (Setup page).");
        };
        var smoke = new Button { Content = "Smoke LLM" };
        smoke.Click += async (_, _) =>
        {
            try
            {
                _bar.Severity = InfoBarSeverity.Success;
                _bar.Message = await LlmChatService.SmokeAsync(WinUiShellState.Settings);
                _bar.IsOpen = true;
            }
            catch (Exception ex)
            {
                _bar.Severity = InfoBarSeverity.Error;
                _bar.Message = ex.Message;
                _bar.IsOpen = true;
            }
        };
        var clearMem = new Button { Content = "Clear conversation memory" };
        clearMem.Click += (_, _) => ConversationMemoryStore.Clear();

        sp.Children.Add(save);
        sp.Children.Add(smoke);
        sp.Children.Add(clearMem);
        Content = new ScrollViewer { Content = sp };

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Apply(WinUiShellState.Settings);
        RefreshEnvSummary();
        WinUiShellState.TryGatherSettingsFromSetup = Gather;
        WinUiShellState.TryApplySettingsToSetup = Apply;
        WinUiShellState.TryRefreshSetupEnvSummary = RefreshEnvSummary;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        WinUiShellState.TryGatherSettingsFromSetup = null;
        WinUiShellState.TryApplySettingsToSetup = null;
        WinUiShellState.TryRefreshSetupEnvSummary = null;
    }

    private void RefreshEnvSummary()
    {
        _envPath.Text = AppPaths.EnvFile;
        var keys = DotEnvSummary.ListKeys(AppPaths.EnvFile);
        _envSummary.Text = keys.Count == 0
            ? "(no .env or empty — template: windows\\.env.example)"
            : string.Join("\r\n", keys.Select(k => k + "=***"));
    }

    private void Apply(NexusSettings s)
    {
        _provider.SelectedItem = s.Provider;
        _mode.SelectedItem = s.Mode;
        _model.Text = s.Model;
        _uiTheme.SelectedItem = string.IsNullOrWhiteSpace(s.UiTheme) ? "Dark" : s.UiTheme;
        _speak.IsChecked = s.SpeakResponses;
        _useKnow.IsChecked = s.UseLocalKnowledge;
        _suggestAuto.IsChecked = s.SuggestAutomations;
        _uia.IsChecked = s.IncludeUiaContextInAsk;
        _mem.IsChecked = s.ConversationMemoryEnabled;
        _memChars.Text = s.ConversationMemoryMaxChars.ToString();
        _hi.IsChecked = s.HighRiskSecondConfirm;
        _safety.SelectedItem = s.Safety.Profile;
        _neverSend.IsChecked = s.Safety.NeverAutoSend;
        _neverPost.IsChecked = s.Safety.NeverAutoPostBook;
        _panic.IsChecked = s.Safety.PanicStopEnabled;
        _denylist.Text = s.Safety.Denylist;
        _watchIv.Text = s.WatchSnapshotIntervalSeconds.ToString();
        _proactive.IsChecked = s.ProactiveDashboardLlm;
        _proactiveIv.Text = s.ProactiveLlmMinIntervalSeconds.ToString();
        _toolHost.IsChecked = s.EnableLocalToolHost;
        _toolPort.Text = s.LocalToolHostPort.ToString();
        RefreshEnvSummary();
    }

    private NexusSettings Gather()
    {
        static int Pi(string? t, int d, int lo, int hi) =>
            int.TryParse(t?.Trim(), out var v) ? Math.Clamp(v, lo, hi) : d;

        return new NexusSettings
        {
            Provider = _provider.SelectedItem?.ToString() ?? "anthropic",
            Mode = _mode.SelectedItem?.ToString() ?? "companion",
            Model = _model.Text?.Trim() ?? "",
            UiTheme = _uiTheme.SelectedItem?.ToString() ?? "Dark",
            SpeakResponses = _speak.IsChecked == true,
            UseLocalKnowledge = _useKnow.IsChecked == true,
            SuggestAutomations = _suggestAuto.IsChecked == true,
            IncludeUiaContextInAsk = _uia.IsChecked == true,
            ConversationMemoryEnabled = _mem.IsChecked == true,
            ConversationMemoryMaxChars = Pi(_memChars.Text, 8000, 2000, 32000),
            HighRiskSecondConfirm = _hi.IsChecked != false,
            WatchSnapshotIntervalSeconds = Pi(_watchIv.Text, 45, 15, 600),
            ProactiveDashboardLlm = _proactive.IsChecked == true,
            ProactiveLlmMinIntervalSeconds = Pi(_proactiveIv.Text, 180, 60, 3600),
            EnableLocalToolHost = _toolHost.IsChecked == true,
            LocalToolHostPort = Pi(_toolPort.Text, 17888, 1024, 65535),
            Safety = new SafetySettings
            {
                Profile = _safety.SelectedItem?.ToString() ?? "balanced",
                NeverAutoSend = _neverSend.IsChecked == true,
                NeverAutoPostBook = _neverPost.IsChecked == true,
                PanicStopEnabled = _panic.IsChecked == true,
                Denylist = _denylist.Text ?? "",
            },
        };
    }
}

public sealed class KnowledgeShellPage : Page
{
    public KnowledgeShellPage()
    {
        var sp = new StackPanel { Spacing = 10, Margin = new Thickness(12) };
        var t = new TextBlock { TextWrapping = TextWrapping.Wrap };
        var b = new Button { Content = "Reindex knowledge" };
        b.Click += async (_, _) =>
        {
            b.IsEnabled = false;
            try
            {
                KnowledgeIndexService.Rebuild();
                _ = EmbeddingRagService.RebuildIfConfiguredAsync(default);
                t.Text = "Reindex OK — chunks + local FTS5 (knowledge-fts.db); embeddings in background if configured.";
            }
            catch (Exception ex)
            {
                t.Text = ex.Message;
            }
            finally
            {
                b.IsEnabled = true;
            }
        };
        sp.Children.Add(new TextBlock { Text = "Knowledge", FontSize = 18, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        sp.Children.Add(b);
        sp.Children.Add(t);
        Content = new ScrollViewer { Content = sp };
    }
}

public sealed class RitualsShellPage : Page
{
    private readonly StackPanel _recipeList = new() { Spacing = 4 };
    private readonly TextBox _name = new() { Header = "Name" };
    private readonly TextBox _steps = new() { AcceptsReturn = true, Header = "Steps JSON", MinHeight = 160 };
    private readonly Button _save = new() { Content = "Save (QA)" };
    private AutomationRecipe? _sel;
    private readonly Grid _grid = new() { Margin = new Thickness(12) };
    private readonly ScrollViewer _left;
    private ResponsiveBand? _ritBand;

    public RitualsShellPage()
    {
        _left = new ScrollViewer { Content = _recipeList };
        _save.Click += async (_, _) => await SaveAsync();
        Content = _grid;
        Loaded += (_, _) =>
        {
            Reload();
            ApplyRitualLayout(ActualWidth);
        };
        SizeChanged += (_, e) => ApplyRitualLayout(e.NewSize.Width);
    }

    private void ApplyRitualLayout(double w)
    {
        if (w <= 0)
            return;
        var band = ResponsiveLayout.GetBand(w);
        if (_ritBand == band && _grid.Children.Count > 0)
            return;
        _ritBand = band;
        _grid.Children.Clear();
        _grid.RowDefinitions.Clear();
        _grid.ColumnDefinitions.Clear();
        if (band == ResponsiveBand.Narrow)
        {
            _grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            _grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            _grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            _grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            _grid.Children.Add(_left);
            _grid.Children.Add(_name);
            _grid.Children.Add(_steps);
            _grid.Children.Add(_save);
            Grid.SetRow(_left, 0);
            Grid.SetRow(_name, 1);
            Grid.SetRow(_steps, 2);
            Grid.SetRow(_save, 3);
        }
        else if (band == ResponsiveBand.Medium)
        {
            _grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            _grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var right = new StackPanel { Spacing = 8 };
            right.Children.Add(_name);
            right.Children.Add(_steps);
            right.Children.Add(_save);
            _grid.Children.Add(_left);
            _grid.Children.Add(right);
            Grid.SetColumn(right, 1);
        }
        else
        {
            _grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            _grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            _grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var mid = new StackPanel { Spacing = 8 };
            mid.Children.Add(_name);
            mid.Children.Add(_save);
            _grid.Children.Add(_left);
            _grid.Children.Add(mid);
            _grid.Children.Add(_steps);
            Grid.SetColumn(mid, 1);
            Grid.SetColumn(_steps, 2);
        }
    }

    private void Reload()
    {
        _recipeList.Children.Clear();
        foreach (var r in RitualRecipeStore.LoadAll().OrderBy(x => x.Name))
        {
            var b = new Button { Content = r.Name, HorizontalAlignment = HorizontalAlignment.Stretch };
            var copy = r;
            b.Click += (_, _) =>
            {
                _sel = copy;
                _name.Text = copy.Name;
                _steps.Text = JsonSerializer.Serialize(copy.Steps, new JsonSerializerOptions { WriteIndented = true });
            };
            _recipeList.Children.Add(b);
        }
    }

    private async Task SaveAsync()
    {
        var recipe = _sel ?? new AutomationRecipe { Id = Guid.NewGuid().ToString("n") };
        recipe.Name = _name.Text?.Trim() ?? "Untitled";
        try
        {
            recipe.Steps = JsonSerializer.Deserialize<List<RecipeStep>>(_steps.Text ?? "[]") ?? new List<RecipeStep>();
        }
        catch
        {
            await new ContentDialog { Title = "JSON", Content = "Invalid JSON", CloseButtonText = "OK", XamlRoot = XamlRoot }.ShowAsync();
            return;
        }

        var qa = RitualQualityGate.Validate(recipe, WinUiShellState.Settings);
        if (!qa.Ok)
        {
            await new ContentDialog
            {
                Title = "QA",
                Content = string.Join("\n", qa.Issues),
                CloseButtonText = "OK",
                XamlRoot = XamlRoot
            }.ShowAsync();
            return;
        }

        RitualRecipeStore.Upsert(recipe);
        _sel = recipe;
        Reload();
    }
}

public sealed class HistoryShellPage : Page
{
    private readonly Grid _grid = new() { Margin = new Thickness(12) };
    private readonly ListView _lv = new();
    private readonly TextBox _detail = new() { IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap };
    private readonly ScrollViewer _listScroll;
    private readonly ScrollViewer _detailScroll;
    private ResponsiveBand? _histBand;

    public HistoryShellPage()
    {
        _lv.SelectionChanged += (_, _) =>
        {
            if (_lv.SelectedItem is ActionHistoryEntry e)
                _detail.Text = JsonSerializer.Serialize(e, new JsonSerializerOptions { WriteIndented = true });
        };
        var heal = new Button { Content = "Self-heal hint" };
        heal.Click += (_, _) =>
        {
            _detail.Text = SelfHealSuggestionService.TrySuggestFromLastAuditFailure() ?? "(none)";
        };
        var sp = new StackPanel { Spacing = 8 };
        sp.Children.Add(heal);
        sp.Children.Add(_lv);
        _listScroll = new ScrollViewer { Content = sp };
        _detailScroll = new ScrollViewer { Content = _detail };
        Content = _grid;
        Loaded += (_, _) =>
        {
            var doc = ActionHistoryService.Load();
            _lv.ItemsSource = doc.Entries.OrderByDescending(x => x.UtcAt).ToList();
            ApplyHistoryLayout(ActualWidth);
        };
        SizeChanged += (_, e) => ApplyHistoryLayout(e.NewSize.Width);
    }

    private void ApplyHistoryLayout(double w)
    {
        if (w <= 0)
            return;
        var band = ResponsiveLayout.GetBand(w);
        if (_histBand == band && _grid.Children.Count > 0)
            return;
        _histBand = band;
        _grid.Children.Clear();
        _grid.RowDefinitions.Clear();
        _grid.ColumnDefinitions.Clear();
        if (band == ResponsiveBand.Narrow)
        {
            _grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            _grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            _grid.Children.Add(_listScroll);
            _grid.Children.Add(_detailScroll);
            Grid.SetRow(_detailScroll, 1);
        }
        else
        {
            _grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(280) });
            _grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            _grid.Children.Add(_listScroll);
            _grid.Children.Add(_detailScroll);
            Grid.SetColumn(_detailScroll, 1);
        }
    }
}

public sealed class ExperimentsShellPage : Page
{
    public ExperimentsShellPage()
    {
        var sp = new StackPanel { Spacing = 12, Margin = new Thickness(12) };
        sp.Children.Add(new TextBlock { Text = "Tier C experiments", FontSize = 18, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        sp.Children.Add(new InfoBar
        {
            IsOpen = true,
            Severity = InfoBarSeverity.Warning,
            Title = "Not product claims",
            Message = "This area is for optional research builds only. Tag: " + TierCExperiments.Tag
        });
        sp.Children.Add(new TextBlock { TextWrapping = TextWrapping.Wrap, Text = "Keep Tier C work isolated from default operator flows and messaging (see USP strategy)." });
        Content = new ScrollViewer { Content = sp };
    }
}

public sealed class DiagnosticsShellPage : Page
{
    private readonly TextBox _log = new() { IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap };
    private readonly StringBuilder _sb = new();

    public DiagnosticsShellPage()
    {
        var sp = new StackPanel { Margin = new Thickness(12) };
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var clear = new Button { Content = "Clear" };
        clear.Click += (_, _) =>
        {
            _sb.Clear();
            _log.Text = "";
        };
        var export = new Button { Content = "Export to file" };
        export.Click += (_, _) =>
        {
            try
            {
                Directory.CreateDirectory(AppPaths.DataDir);
                var name = Path.Combine(AppPaths.DataDir, $"diagnostics-{DateTime.Now:yyyyMMdd-HHmmss}.log");
                var header = AppBuildInfo.Summary + Environment.NewLine + new string('=', 60) + Environment.NewLine;
                File.WriteAllText(name, header + (_log.Text ?? ""));
                NexusShell.Log($"export diagnostics → {name}");
            }
            catch (Exception ex)
            {
                NexusShell.Log("export diagnostics failed: " + ex.Message);
            }
        };
        row.Children.Add(clear);
        row.Children.Add(export);
        sp.Children.Add(row);
        sp.Children.Add(_log);
        Content = sp;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        WinUiShellState.GlobalLogLine += OnLog;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        WinUiShellState.GlobalLogLine -= OnLog;
    }

    private void OnLog(string line)
    {
        _ = DispatcherQueue.TryEnqueue(() =>
        {
            _sb.AppendLine(line);
            if (_sb.Length > 120_000)
                _sb.Remove(0, _sb.Length - 120_000);
            _log.Text = _sb.ToString();
        });
    }
}

public sealed class ConsoleShellPage : Page
{
    public ConsoleShellPage()
    {
        var sp = new StackPanel { Spacing = 10, Margin = new Thickness(12), MaxWidth = 900 };
        var agent = new ComboBox { Header = "Agent" };
        foreach (var a in new[] { "codex", "claude code", "openclaw" })
            agent.Items.Add(a);
        agent.SelectedIndex = 0;
        var prompt = new TextBox { Header = "Prompt", AcceptsReturn = true, MinHeight = 120 };
        var run = new Button { Content = "Run" };
        var o = new TextBox { IsReadOnly = true, AcceptsReturn = true, MinHeight = 200, TextWrapping = TextWrapping.Wrap };
        run.Click += async (_, _) =>
        {
            var p = prompt.Text?.Trim();
            if (string.IsNullOrEmpty(p))
                return;
            run.IsEnabled = false;
            try
            {
                var (path, ex) = await CliAgentRunner.RunAsync(agent.SelectedItem?.ToString() ?? "codex", p);
                o.Text = path + "\n\n" + ex;
            }
            catch (Exception ex)
            {
                o.Text = ex.ToString();
            }
            finally
            {
                run.IsEnabled = true;
            }
        };
        sp.Children.Add(agent);
        sp.Children.Add(prompt);
        sp.Children.Add(run);
        sp.Children.Add(o);
        Content = new ScrollViewer { Content = sp };
    }
}

public sealed class LiveContextShellPage : Page
{
    public LiveContextShellPage()
    {
        var sp = new StackPanel { Spacing = 10, Margin = new Thickness(12) };
        var t = new TextBox { IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 200 };
        var b = new Button { Content = "Refresh" };
        b.Click += (_, _) =>
        {
            if (!OperatingSystem.IsWindows())
            {
                t.Text = "Windows only.";
                return;
            }

            var d = ForegroundWindowInfo.TryReadDetail();
            if (d == null)
            {
                t.Text = "(no detail)";
                return;
            }

            var fam = OperatorAdapterRegistry.ResolveFamily(d.Value.ProcessName, d.Value.Title);
            t.Text = $"{d.Value.Title} / {d.Value.ProcessName} / {fam}\n{OperatorAdapterRegistry.TryEnrichForegroundContext()}";
        };
        sp.Children.Add(b);
        sp.Children.Add(t);
        Content = new ScrollViewer { Content = sp };
    }
}
