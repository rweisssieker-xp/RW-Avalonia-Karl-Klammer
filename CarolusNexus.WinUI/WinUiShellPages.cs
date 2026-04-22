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
using CarolusNexus_WinUI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace CarolusNexus_WinUI.Pages;

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
    private readonly CheckBox _axEnabled = new() { Content = "Enable ax.* integration tokens", IsChecked = true };
    private readonly TextBox _axTenant = new() { Header = "AX test tenant label (optional, non-secret)" };
    private readonly ComboBox _axBackend = new() { Header = "AX integration backend" };
    private readonly TextBox _axODataUrl = new() { Header = "AX OData base URL (e.g. https://AOS/AX/Data/)" };
    private readonly CheckBox _axODataWin = new() { Content = "OData/AIF: use Windows default credentials", IsChecked = true };
    private readonly TextBox _axAifUrl = new() { Header = "AIF service base URL (optional ping)" };
    private readonly TextBox _axDataArea = new() { Header = "DataAreaId / company (test)" };
    private readonly TextBox _axBcDll = new() { Header = "BusinessConnectorNet.dll path (COM)" };
    private readonly TextBox _axAos = new() { Header = "COM: AOS (host:port)" };
    private readonly TextBox _axDb = new() { Header = "COM: database name" };
    private readonly TextBox _axLang = new() { Header = "COM: language (e.g. en-us)" };
    private readonly TextBox _envSummary = new()
    {
        IsReadOnly = true,
        AcceptsReturn = true,
        MinHeight = 100,
        FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas"),
        TextWrapping = TextWrapping.Wrap
    };
    private readonly TextBlock _envPath = new() { TextWrapping = TextWrapping.Wrap };
    private readonly StackPanel _setupStatus = new() { Orientation = Orientation.Horizontal, Spacing = 10 };
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
        foreach (var b in new[] { "foreground_uia", "odata", "com_bc" })
            _axBackend.Items.Add(b);
        _axBackend.SelectedIndex = 0;

        static Expander MkExp(string header, bool expanded, params UIElement[] children)
        {
            var inner = new StackPanel { Spacing = 12 };
            foreach (var c in children)
                inner.Children.Add(c);
            return new Expander
            {
                Header = header,
                Content = inner,
                IsExpanded = expanded,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Stretch
            };
        }

        var sp = new StackPanel { Spacing = 14, Margin = new Thickness(20, 16, 20, 20), MaxWidth = 920 };
        sp.Children.Add(WinUiFluentChrome.PageTitle("Setup"));
        var setupHint = new TextBlock
        {
            Text = "Environment, safety, watch mode, tool host, and AX test tenant — aligned with Avalonia §5.4.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = WinUiFluentChrome.SecondaryTextBrush
        };
        WinUiFluentChrome.ApplyCaptionTextStyle(setupHint);
        sp.Children.Add(setupHint);
        sp.Children.Add(WinUiFluentChrome.WrapCard(_setupStatus, new Thickness(12, 10, 12, 10)));
        sp.Children.Add(_bar);

        var actionRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        var save = new Button { Content = "Save settings" };
        WinUiFluentChrome.StyleActionButton(save, accent: true);
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
        WinUiFluentChrome.StyleActionButton(smoke);
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
        WinUiFluentChrome.StyleActionButton(clearMem);
        clearMem.Click += (_, _) => ConversationMemoryStore.Clear();

        actionRow.Children.Add(save);
        actionRow.Children.Add(smoke);
        actionRow.Children.Add(clearMem);
        sp.Children.Add(WinUiFluentChrome.WrapCard(actionRow, new Thickness(16, 12, 16, 12)));

        sp.Children.Add(MkExp("Routing & appearance", true,
            _provider, _mode, _model, _uiTheme));
        sp.Children.Add(MkExp("Behavior", true,
            _speak, _useKnow, _suggestAuto, _uia, _mem, _memChars));
        sp.Children.Add(MkExp("Safety & governance", true,
            _hi, _safety, _neverSend, _neverPost, _panic, _denylist));
        sp.Children.Add(MkExp("Watch & tool host", true,
            _watchIv, _proactive, _proactiveIv, _toolHost, _toolPort));
        sp.Children.Add(MkExp("Dynamics AX 2012 — AIF / OData / COM (optional, test tenant)", false,
            _axEnabled, _axTenant, _axBackend, _axODataUrl, _axODataWin, _axAifUrl, _axDataArea, _axBcDll, _axAos, _axDb, _axLang));
        _envPath.Foreground = WinUiFluentChrome.SecondaryTextBrush;
        WinUiFluentChrome.ApplyCaptionTextStyle(_envPath);
        sp.Children.Add(MkExp(".env overview (keys only)", false, _envSummary, _envPath));

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
        RefreshSetupStatus(WinUiShellState.Settings);
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
        _axEnabled.IsChecked = s.AxIntegrationEnabled;
        _axTenant.Text = s.AxTestTenantLabel ?? "";
        _axBackend.SelectedItem = string.IsNullOrWhiteSpace(s.AxIntegrationBackend) ? "foreground_uia" : s.AxIntegrationBackend;
        _axODataUrl.Text = s.AxODataBaseUrl ?? "";
        _axODataWin.IsChecked = s.AxODataUseDefaultCredentials;
        _axAifUrl.Text = s.AxAifServiceBaseUrl ?? "";
        _axDataArea.Text = s.AxDataAreaId ?? "";
        _axBcDll.Text = s.AxBusinessConnectorNetAssemblyPath ?? "";
        _axAos.Text = s.AxBcObjectServer ?? "";
        _axDb.Text = s.AxBcDatabase ?? "";
        _axLang.Text = string.IsNullOrWhiteSpace(s.AxBcLanguage) ? "en-us" : s.AxBcLanguage;
        RefreshEnvSummary();
        RefreshSetupStatus(s);
    }

    private void RefreshSetupStatus(NexusSettings s)
    {
        _setupStatus.Children.Clear();
        var keyState = DotEnvStore.HasProviderKey(s.Provider) ? "key ready" : "key missing";
        var toolHost = s.EnableLocalToolHost ? $"127.0.0.1:{s.LocalToolHostPort}" : "off";
        var ax = s.AxIntegrationEnabled ? s.AxIntegrationBackend : "off";
        _setupStatus.Children.Add(WinUiFluentChrome.StatusTile("Provider", s.Provider, keyState));
        _setupStatus.Children.Add(WinUiFluentChrome.StatusTile("Mode", s.Mode, s.Model));
        _setupStatus.Children.Add(WinUiFluentChrome.StatusTile("Safety", s.Safety.Profile, s.Safety.PanicStopEnabled ? "panic enabled" : "panic off"));
        _setupStatus.Children.Add(WinUiFluentChrome.StatusTile("Tool host", toolHost, "local only"));
        _setupStatus.Children.Add(WinUiFluentChrome.StatusTile("AX", ax, s.AxTestTenantLabel ?? "test tenant optional"));
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
            AxIntegrationEnabled = _axEnabled.IsChecked != false,
            AxTestTenantLabel = _axTenant.Text?.Trim() ?? "",
            AxIntegrationBackend = _axBackend.SelectedItem?.ToString() ?? "foreground_uia",
            AxODataBaseUrl = _axODataUrl.Text?.Trim() ?? "",
            AxODataUseDefaultCredentials = _axODataWin.IsChecked != false,
            AxAifServiceBaseUrl = _axAifUrl.Text?.Trim() ?? "",
            AxDataAreaId = _axDataArea.Text?.Trim() ?? "",
            AxBusinessConnectorNetAssemblyPath = _axBcDll.Text?.Trim() ?? "",
            AxBcObjectServer = _axAos.Text?.Trim() ?? "",
            AxBcDatabase = _axDb.Text?.Trim() ?? "",
            AxBcLanguage = string.IsNullOrWhiteSpace(_axLang.Text) ? "en-us" : _axLang.Text!.Trim(),
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

public sealed class ExperimentsShellPage : Page
{
    private readonly TextBlock _observeStatus = new() { TextWrapping = TextWrapping.Wrap };
    private readonly TextBox _observeOutput = new()
    {
        IsReadOnly = true,
        AcceptsReturn = true,
        MinHeight = 280,
        TextWrapping = TextWrapping.Wrap,
        FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas"),
        FontSize = 12
    };

    public ExperimentsShellPage()
    {
        var sp = new StackPanel { Spacing = 16, Margin = new Thickness(20, 16, 20, 20) };
        sp.Children.Add(WinUiFluentChrome.PageTitle("Tier C experiments"));
        sp.Children.Add(new InfoBar
        {
            IsOpen = true,
            Severity = InfoBarSeverity.Warning,
            Title = "Not product claims",
            Message = "This area is for optional research builds only. Tag: " + TierCExperiments.Tag
        });
        var body = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Text = "Keep Tier C work isolated from default operator flows and messaging (see USP strategy).",
            Foreground = WinUiFluentChrome.SecondaryTextBrush
        };
        WinUiFluentChrome.ApplyBodyTextStyle(body);
        sp.Children.Add(WinUiFluentChrome.WrapCard(body));
        sp.Children.Add(WinUiFluentChrome.StatusTile("Computer-use reality", "observe-only", "foreground + adapter + UIA + risk; no clicks or writes"));

        _observeStatus.Foreground = WinUiFluentChrome.SecondaryTextBrush;
        WinUiFluentChrome.ApplyCaptionTextStyle(_observeStatus);
        _observeStatus.Text = "Ready. Observe-only does not execute model actions.";

        var observe = new Button { Content = "Observe foreground only" };
        WinUiFluentChrome.StyleActionButton(observe, accent: true);
        observe.Click += (_, _) => RunObserveOnly();

        var promote = new Button { Content = "Copy summary to log" };
        WinUiFluentChrome.StyleActionButton(promote);
        promote.Click += (_, _) =>
        {
            if (!string.IsNullOrWhiteSpace(_observeOutput.Text))
                NexusShell.Log("computer-use observe summary: " + _observeOutput.Text.Split('\n').FirstOrDefault());
        };

        sp.Children.Add(WinUiFluentChrome.SectionCard("Computer-use observe-only", "Safe local computer-use surface before any closed-loop automation", new StackPanel
        {
            Spacing = 10,
            Children =
            {
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 8,
                    Children = { observe, promote }
                },
                _observeStatus,
                _observeOutput
            }
        }));
        Content = new ScrollViewer { Content = sp };
    }

    private void RunObserveOnly()
    {
        try
        {
            var snapshot = ComputerUseLoopService.ObserveOnly(WinUiShellState.Settings);
            _observeOutput.Text = ComputerUseLoopService.FormatObserveOnly(snapshot);
            _observeStatus.Text = $"Observed {snapshot.ForegroundProcess} · {snapshot.AdapterFamily} · risk {snapshot.Risk}";
        }
        catch (Exception ex)
        {
            _observeStatus.Text = "Observe-only failed: " + ex.Message;
            _observeOutput.Text = ex.ToString();
            NexusShell.Log("computer-use observe-only failed: " + ex.Message);
        }
    }
}

public sealed class DiagnosticsShellPage : Page
{
    private readonly TextBox _log = new()
    {
        IsReadOnly = true,
        AcceptsReturn = true,
        TextWrapping = TextWrapping.Wrap,
        MinHeight = 320,
        FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas"),
        FontSize = 12
    };
    private readonly StringBuilder _sb = new();

    public DiagnosticsShellPage()
    {
        var sp = new StackPanel { Spacing = 14, Margin = new Thickness(20, 16, 20, 20) };
        sp.Children.Add(WinUiFluentChrome.PageTitle("Diagnostics"));
        var hint = new TextBlock
        {
            Text = "Live log from the shell; export bundles recent lines for support.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = WinUiFluentChrome.SecondaryTextBrush
        };
        WinUiFluentChrome.ApplyCaptionTextStyle(hint);
        sp.Children.Add(hint);
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        var clear = new Button { Content = "Clear" };
        WinUiFluentChrome.StyleActionButton(clear);
        clear.Click += (_, _) =>
        {
            _sb.Clear();
            _log.Text = "";
        };
        var export = new Button { Content = "Export to file" };
        WinUiFluentChrome.StyleActionButton(export, accent: true);
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
        sp.Children.Add(WinUiFluentChrome.WrapCard(row, new Thickness(16, 12, 16, 12)));
        sp.Children.Add(WinUiFluentChrome.WrapCard(_log, new Thickness(12, 10, 12, 10)));
        Content = new ScrollViewer { Content = sp };
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
        var sp = new StackPanel { Spacing = 16, Margin = new Thickness(20, 16, 20, 20), MaxWidth = 920 };
        sp.Children.Add(WinUiFluentChrome.PageTitle("CLI agents"));
        var sub = new TextBlock
        {
            Text = "Invoke a local CLI agent with a prompt; output streams below.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = WinUiFluentChrome.SecondaryTextBrush
        };
        WinUiFluentChrome.ApplyCaptionTextStyle(sub);
        sp.Children.Add(sub);
        var agent = new ComboBox { Header = "Agent" };
        foreach (var a in new[] { "codex", "claude code", "openclaw" })
            agent.Items.Add(a);
        agent.SelectedIndex = 0;
        var prompt = new TextBox { Header = "Prompt", AcceptsReturn = true, MinHeight = 120 };
        var run = new Button { Content = "Run" };
        WinUiFluentChrome.StyleActionButton(run, accent: true);
        var o = new TextBox
        {
            IsReadOnly = true,
            AcceptsReturn = true,
            MinHeight = 200,
            TextWrapping = TextWrapping.Wrap,
            FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas"),
            FontSize = 12
        };
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
        var form = new StackPanel { Spacing = 12 };
        form.Children.Add(agent);
        form.Children.Add(prompt);
        form.Children.Add(run);
        sp.Children.Add(WinUiFluentChrome.WrapCard(form));
        sp.Children.Add(WinUiFluentChrome.ColumnCaption("Output"));
        sp.Children.Add(WinUiFluentChrome.WrapCard(o, new Thickness(12, 10, 12, 10)));
        Content = new ScrollViewer { Content = sp };
    }
}
