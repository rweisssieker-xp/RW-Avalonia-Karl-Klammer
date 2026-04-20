using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CarolusNexus;
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
    public DashboardShellPage()
    {
        var sp = new StackPanel { Spacing = 12, Margin = new Thickness(12) };
        sp.Children.Add(new TextBlock { Text = "Dashboard", FontSize = 18, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        var btn = new Button { Content = "Refresh" };
        var tb = new TextBlock { TextWrapping = TextWrapping.Wrap };
        void Refresh()
        {
            tb.Text = "Recent log:\n" + NexusShell.FormatRecentLogForDashboard(8)
                      + "\n\nBand: " + ResponsiveLayout.GetBand(ActualWidth)
                      + "\nRepo: " + AppPaths.RepoRoot;
        }

        btn.Click += (_, _) => Refresh();
        sp.Children.Add(btn);
        sp.Children.Add(tb);
        Content = new ScrollViewer { Content = sp };
        Loaded += (_, _) => Refresh();
    }
}

public sealed class SetupShellPage : Page
{
    public SetupShellPage()
    {
        var sp = new StackPanel { Spacing = 10, Margin = new Thickness(12), MaxWidth = 720 };
        var provider = new ComboBox { Header = "Provider" };
        foreach (var p in new[] { "anthropic", "openai", "openai-compatible" })
            provider.Items.Add(p);
        var mode = new ComboBox { Header = "Mode" };
        foreach (var m in new[] { "companion", "agent", "automation", "watch" })
            mode.Items.Add(m);
        var model = new TextBox { Header = "Model" };
        var safety = new ComboBox { Header = "Safety" };
        foreach (var x in new[] { "strict", "balanced", "power-user" })
            safety.Items.Add(x);
        var uia = new CheckBox { Content = "UIA in Ask" };
        var mem = new CheckBox { Content = "Conversation memory" };
        var memChars = new TextBox { Header = "Memory max chars" };
        var hi = new CheckBox { Content = "High-risk second confirm", IsChecked = true };
        var bar = new InfoBar { IsOpen = false };

        void Bind()
        {
            var s = WinUiShellState.Settings;
            provider.SelectedItem = s.Provider;
            mode.SelectedItem = s.Mode;
            model.Text = s.Model;
            safety.SelectedItem = s.Safety.Profile;
            uia.IsChecked = s.IncludeUiaContextInAsk;
            mem.IsChecked = s.ConversationMemoryEnabled;
            memChars.Text = s.ConversationMemoryMaxChars.ToString();
            hi.IsChecked = s.HighRiskSecondConfirm;
        }

        var save = new Button { Content = "Save settings" };
        save.Click += (_, _) =>
        {
            static int Pi(string? t, int d, int lo, int hi) =>
                int.TryParse(t?.Trim(), out var v) ? Math.Clamp(v, lo, hi) : d;
            var s = WinUiShellState.Settings;
            s.Provider = provider.SelectedItem?.ToString() ?? s.Provider;
            s.Mode = mode.SelectedItem?.ToString() ?? s.Mode;
            s.Model = model.Text?.Trim() ?? s.Model;
            s.Safety.Profile = safety.SelectedItem?.ToString() ?? s.Safety.Profile;
            s.IncludeUiaContextInAsk = uia.IsChecked == true;
            s.ConversationMemoryEnabled = mem.IsChecked == true;
            s.ConversationMemoryMaxChars = Pi(memChars.Text, 8000, 2000, 32000);
            s.HighRiskSecondConfirm = hi.IsChecked != false;
            WinUiShellState.SettingsStore.Save(s);
            WinUiShellState.Settings = s;
            bar.Message = "Saved.";
            bar.IsOpen = true;
        };
        var smoke = new Button { Content = "Smoke LLM" };
        smoke.Click += async (_, _) =>
        {
            try
            {
                bar.Severity = InfoBarSeverity.Success;
                bar.Message = await LlmChatService.SmokeAsync(WinUiShellState.Settings);
                bar.IsOpen = true;
            }
            catch (Exception ex)
            {
                bar.Severity = InfoBarSeverity.Error;
                bar.Message = ex.Message;
                bar.IsOpen = true;
            }
        };
        var clearMem = new Button { Content = "Clear conversation memory" };
        clearMem.Click += (_, _) => ConversationMemoryStore.Clear();

        sp.Children.Add(bar);
        sp.Children.Add(provider);
        sp.Children.Add(mode);
        sp.Children.Add(model);
        sp.Children.Add(safety);
        sp.Children.Add(uia);
        sp.Children.Add(mem);
        sp.Children.Add(memChars);
        sp.Children.Add(hi);
        sp.Children.Add(save);
        sp.Children.Add(smoke);
        sp.Children.Add(clearMem);
        Content = new ScrollViewer { Content = sp };
        Loaded += (_, _) => Bind();
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
                t.Text = "Reindex OK; embeddings in background if configured.";
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
    private AutomationRecipe? _sel;
    private readonly Grid _grid = new() { Margin = new Thickness(12) };
    private readonly ScrollViewer _left;
    private readonly StackPanel _right;
    private ResponsiveBand? _ritBand;

    public RitualsShellPage()
    {
        _left = new ScrollViewer { Content = _recipeList };
        _right = new StackPanel { Spacing = 8 };
        _right.Children.Add(_name);
        _right.Children.Add(_steps);
        var save = new Button { Content = "Save (QA)" };
        save.Click += async (_, _) => await SaveAsync();
        _right.Children.Add(save);
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
            _grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            _grid.Children.Add(_left);
            _grid.Children.Add(_right);
            Grid.SetRow(_left, 0);
            Grid.SetRow(_right, 1);
        }
        else
        {
            _grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            _grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            _grid.Children.Add(_left);
            _grid.Children.Add(_right);
            Grid.SetColumn(_right, 1);
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
    public HistoryShellPage()
    {
        var grid = new Grid { Margin = new Thickness(12) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(280) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var lv = new ListView();
        var detail = new TextBox { IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap };
        lv.SelectionChanged += (_, _) =>
        {
            if (lv.SelectedItem is ActionHistoryEntry e)
                detail.Text = JsonSerializer.Serialize(e, new JsonSerializerOptions { WriteIndented = true });
        };
        var heal = new Button { Content = "Self-heal hint" };
        heal.Click += (_, _) =>
        {
            detail.Text = SelfHealSuggestionService.TrySuggestFromLastAuditFailure() ?? "(none)";
        };
        var sp = new StackPanel();
        sp.Children.Add(heal);
        sp.Children.Add(lv);
        grid.Children.Add(new ScrollViewer { Content = sp });
        grid.Children.Add(new ScrollViewer { Content = detail });
        Grid.SetColumn(detail, 1);
        Content = grid;
        Loaded += (_, _) =>
        {
            var doc = ActionHistoryService.Load();
            lv.ItemsSource = doc.Entries.OrderByDescending(x => x.UtcAt).ToList();
        };
    }
}

public sealed class DiagnosticsShellPage : Page
{
    private readonly TextBox _log = new() { IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap };
    private readonly StringBuilder _sb = new();

    public DiagnosticsShellPage()
    {
        var sp = new StackPanel { Margin = new Thickness(12) };
        var clear = new Button { Content = "Clear" };
        clear.Click += (_, _) =>
        {
            _sb.Clear();
            _log.Text = "";
        };
        sp.Children.Add(clear);
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
