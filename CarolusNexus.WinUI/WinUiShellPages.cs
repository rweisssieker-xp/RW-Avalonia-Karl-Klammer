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
using Microsoft.UI.Xaml.Media.Imaging;

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
    private readonly CheckBox _cliRoutes = new() { Content = "Enable CLI ask routes (use codex / use claude code / use openclaw)" };
    private readonly CheckBox _missionRoutes = new() { Content = "Enable mission/autonomy routes (autonomy, predictive, orbit)" };
    private readonly CheckBox _radicalAutoRoutes = new() { Content = "Enable radical auto routes (radical-auto + radical run)" };
    private readonly CheckBox _radicalIdeaRoutes = new() { Content = "Enable radical idea blueprint routes (radical ...)" };
    private readonly CheckBox _missionFallback = new() { Content = "Fallback mission mode for plain ask prompts" };
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
            _speak, _useKnow, _suggestAuto, _cliRoutes, _missionRoutes, _radicalAutoRoutes, _radicalIdeaRoutes, _missionFallback, _uia, _mem, _memChars));
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
            _cliRoutes.IsChecked = s.EnableCliHandoffRoutes;
            _missionRoutes.IsChecked = s.EnableMissionPromptRoutes;
            _radicalAutoRoutes.IsChecked = s.EnableRadicalAutoRoutes;
            _radicalIdeaRoutes.IsChecked = s.EnableRadicalIdeaBlueprint;
            _missionFallback.IsChecked = s.FallbackMissionModeWhenAsk;
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
            EnableCliHandoffRoutes = _cliRoutes.IsChecked == true,
            EnableMissionPromptRoutes = _missionRoutes.IsChecked == true,
            EnableRadicalAutoRoutes = _radicalAutoRoutes.IsChecked == true,
            EnableRadicalIdeaBlueprint = _radicalIdeaRoutes.IsChecked == true,
            FallbackMissionModeWhenAsk = _missionFallback.IsChecked == true,
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
    private readonly Button _runDryRun = new() { Content = "Run sample plan (dry-run)" };
    private readonly Button _runExecute = new() { Content = "Run sample plan (execute)" };
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
        sp.Children.Add(WinUiFluentChrome.StatusTile("Computer-use reality", "sim + guarded execute", "foreground + adapter + UIA + optional execution behind power-user"));

        _observeStatus.Foreground = WinUiFluentChrome.SecondaryTextBrush;
        WinUiFluentChrome.ApplyCaptionTextStyle(_observeStatus);
        _observeStatus.Text = "Ready. Observe-only does not execute model actions.";

        var observe = new Button { Content = "Observe foreground only" };
        WinUiFluentChrome.StyleActionButton(observe, accent: true);
        observe.Click += (_, _) => RunObserveOnly();

        WinUiFluentChrome.StyleActionButton(_runDryRun);
        _runDryRun.Click += async (_, _) => await RunSamplePlanAsync(dryRun: true);

        WinUiFluentChrome.StyleActionButton(_runExecute, accent: true);
        _runExecute.Click += async (_, _) => await RunSamplePlanAsync(dryRun: false);

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
                    Children = { observe, _runDryRun, _runExecute, promote }
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

    private async Task RunSamplePlanAsync(bool dryRun)
    {
        try
        {
            _observeStatus.Text = dryRun
                ? "Running dry-run simulation through computer-use sample steps..."
                : "Running sample plan through guarded execution path...";
            _observeOutput.Text = "";
            var log = await ComputerUseLoopService.RunThroughSimulatorAsync(
                ComputerUseLoopService.SampleTierCPlanSteps(),
                maxSteps: 3,
                dryRun: dryRun,
                settings: WinUiShellState.Settings);
            _observeOutput.Text = log;
            _observeStatus.Text = dryRun
                ? "Dry-run completed. Switch to execute only with safety profile “power-user”."
                : "Execution path completed (or blocked by guards/safety).";
            NexusShell.Log(
                dryRun
                    ? "computer-use sample dry-run completed."
                    : "computer-use sample execute attempt completed.");
        }
        catch (Exception ex)
        {
            _observeStatus.Text = (dryRun ? "Dry-run" : "Execute") + " failed: " + ex.Message;
            _observeOutput.Text = ex.ToString();
            NexusShell.Log("computer-use sample run failed: " + ex.Message);
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
        var report = new Button { Content = "Runtime report" };
        WinUiFluentChrome.StyleActionButton(report);
        report.Click += (_, _) =>
        {
            try
            {
                var settings = NexusContext.GetSettings?.Invoke() ?? WinUiShellState.Settings;
                _log.Text = RuntimeDiagnosticsService.BuildReport(settings);
                var path = RuntimeDiagnosticsService.SaveReport(settings);
                NexusShell.Log("Diagnostics runtime report → " + path);
            }
            catch (Exception ex)
            {
                NexusShell.Log("runtime report failed: " + ex.Message);
            }
        };
        var audit = new Button { Content = "Audit package" };
        WinUiFluentChrome.StyleActionButton(audit);
        audit.Click += (_, _) =>
        {
            try
            {
                var settings = NexusContext.GetSettings?.Invoke() ?? WinUiShellState.Settings;
                var path = AuditExportPackageService.Export(null, settings);
                _log.Text = "Audit package exported:\n" + path + "\n\n" + RuntimeDiagnosticsService.BuildReport(settings);
                NexusShell.Log("Audit package → " + path);
            }
            catch (Exception ex)
            {
                NexusShell.Log("audit package failed: " + ex.Message);
            }
        };
        var usp = new Button { Content = "USP Studio" };
        WinUiFluentChrome.StyleActionButton(usp);
        usp.Click += (_, _) =>
        {
            try
            {
                var settings = NexusContext.GetSettings?.Invoke() ?? WinUiShellState.Settings;
                _log.Text = UspStudioService.BuildStudioReport(settings, "");
                NexusShell.Log("USP Studio report generated.");
            }
            catch (Exception ex)
            {
                NexusShell.Log("USP Studio failed: " + ex.Message);
            }
        };
        var uspPack = new Button { Content = "USP Studio pack" };
        WinUiFluentChrome.StyleActionButton(uspPack);
        uspPack.Click += (_, _) =>
        {
            try
            {
                var settings = NexusContext.GetSettings?.Invoke() ?? WinUiShellState.Settings;
                var path = UspStudioService.ExportStudioPack(settings, "");
                _log.Text = "USP Studio pack exported:\n" + path + "\n\n" + UspStudioService.BuildStudioReport(settings, "");
                NexusShell.Log("USP Studio pack → " + path);
            }
            catch (Exception ex)
            {
                NexusShell.Log("USP Studio pack failed: " + ex.Message);
            }
        };
        var evalLab = new Button { Content = "AI Eval Lab" };
        WinUiFluentChrome.StyleActionButton(evalLab);
        evalLab.Click += (_, _) =>
        {
            try
            {
                var settings = NexusContext.GetSettings?.Invoke() ?? WinUiShellState.Settings;
                _log.Text = AiEvaluationLabService.BuildEvalLabReport(settings, "") + "\n\n" + AiEvaluationLabService.BuildHallucinationGuard(settings, "");
                NexusShell.Log("AI Evaluation Lab report generated.");
            }
            catch (Exception ex)
            {
                NexusShell.Log("AI Evaluation Lab failed: " + ex.Message);
            }
        };
        var evalPack = new Button { Content = "AI Eval pack" };
        WinUiFluentChrome.StyleActionButton(evalPack);
        evalPack.Click += (_, _) =>
        {
            try
            {
                var settings = NexusContext.GetSettings?.Invoke() ?? WinUiShellState.Settings;
                var path = AiEvaluationLabService.ExportEvaluationPack(settings, "");
                _log.Text = "AI Evaluation Lab pack exported:\n" + path + "\n\n" + AiEvaluationLabService.BuildEvalLabReport(settings, "");
                NexusShell.Log("AI Evaluation Lab pack → " + path);
            }
            catch (Exception ex)
            {
                NexusShell.Log("AI Evaluation Lab pack failed: " + ex.Message);
            }
        };
        var aiRoi = new Button { Content = "AI ROI" };
        WinUiFluentChrome.StyleActionButton(aiRoi);
        aiRoi.Click += (_, _) =>
        {
            try
            {
                var settings = NexusContext.GetSettings?.Invoke() ?? WinUiShellState.Settings;
                _log.Text = AiRoiOpportunityService.BuildRoiReport(settings, "") + "\n\n" + AiRoiOpportunityService.BuildOpportunityMatrix(settings, "");
                NexusShell.Log("AI ROI opportunity report generated.");
            }
            catch (Exception ex)
            {
                NexusShell.Log("AI ROI failed: " + ex.Message);
            }
        };
        var aiRoiPack = new Button { Content = "AI ROI pack" };
        WinUiFluentChrome.StyleActionButton(aiRoiPack);
        aiRoiPack.Click += (_, _) =>
        {
            try
            {
                var settings = NexusContext.GetSettings?.Invoke() ?? WinUiShellState.Settings;
                var path = AiRoiOpportunityService.ExportRoiPack(settings, "");
                _log.Text = "AI ROI opportunity pack exported:\n" + path + "\n\n" + AiRoiOpportunityService.BuildRoiReport(settings, "");
                NexusShell.Log("AI ROI opportunity pack → " + path);
            }
            catch (Exception ex)
            {
                NexusShell.Log("AI ROI pack failed: " + ex.Message);
            }
        };
        var aiDemo = new Button { Content = "AI demo" };
        WinUiFluentChrome.StyleActionButton(aiDemo);
        aiDemo.Click += (_, _) =>
        {
            try
            {
                var settings = NexusContext.GetSettings?.Invoke() ?? WinUiShellState.Settings;
                _log.Text = AiDemoOrchestratorService.BuildDemoRunbook(settings, "") + "\n\n" + AiDemoOrchestratorService.BuildClickPath(settings);
                NexusShell.Log("AI Demo Orchestrator report generated.");
            }
            catch (Exception ex)
            {
                NexusShell.Log("AI Demo Orchestrator failed: " + ex.Message);
            }
        };
        var aiDemoPack = new Button { Content = "AI demo pack" };
        WinUiFluentChrome.StyleActionButton(aiDemoPack);
        aiDemoPack.Click += (_, _) =>
        {
            try
            {
                var settings = NexusContext.GetSettings?.Invoke() ?? WinUiShellState.Settings;
                var path = AiDemoOrchestratorService.ExportDemoPack(settings, "");
                _log.Text = "AI Demo Orchestrator pack exported:\n" + path + "\n\n" + AiDemoOrchestratorService.BuildDemoRunbook(settings, "");
                NexusShell.Log("AI Demo Orchestrator pack → " + path);
            }
            catch (Exception ex)
            {
                NexusShell.Log("AI Demo Orchestrator pack failed: " + ex.Message);
            }
        };
        var pilotProof = new Button { Content = "Pilot proof pack" };
        WinUiFluentChrome.StyleActionButton(pilotProof, accent: true);
        pilotProof.Click += (_, _) =>
        {
            try
            {
                var settings = NexusContext.GetSettings?.Invoke() ?? WinUiShellState.Settings;
                DemoProgressTrackerService.Mark("buyer-pack-exported");
                var path = PilotProofPackService.ExportMasterPack(settings, "");
                _log.Text = "Pilot Proof Master Pack exported:\n" + path + "\n\n" + PilotProofPackService.BuildPilotSummary(settings, "");
                NexusShell.Log("Pilot Proof Master Pack → " + path);
            }
            catch (Exception ex)
            {
                NexusShell.Log("Pilot Proof Master Pack failed: " + ex.Message);
            }
        };
        var demoProgress = new Button { Content = "Demo progress" };
        WinUiFluentChrome.StyleActionButton(demoProgress);
        demoProgress.Click += (_, _) => _log.Text = DemoProgressTrackerService.BuildProgressReport();
        var releaseReady = new Button { Content = "Release ready" };
        WinUiFluentChrome.StyleActionButton(releaseReady);
        releaseReady.Click += (_, _) =>
        {
            var settings = NexusContext.GetSettings?.Invoke() ?? WinUiShellState.Settings;
            _log.Text = ReleaseReadinessService.BuildReadinessReport(settings) + "\n\n" + ReleaseReadinessService.BuildPilotModeReport(settings);
        };
        var quality = new Button { Content = "Quality badges" };
        WinUiFluentChrome.StyleActionButton(quality);
        quality.Click += (_, _) =>
        {
            var settings = NexusContext.GetSettings?.Invoke() ?? WinUiShellState.Settings;
            _log.Text = AiAnswerQualityBadgeService.BuildQualityBadge(settings, "");
        };
        var dataset = new Button { Content = "Eval dataset" };
        WinUiFluentChrome.StyleActionButton(dataset);
        dataset.Click += (_, _) =>
        {
            var settings = NexusContext.GetSettings?.Invoke() ?? WinUiShellState.Settings;
            var path = AiEvaluationDatasetBuilderService.ExportSeedDataset(settings, "");
            _log.Text = "AI evaluation dataset exported:\n" + path;
            NexusShell.Log("AI evaluation dataset → " + path);
        };
        var privacy = new Button { Content = "Privacy firewall" };
        WinUiFluentChrome.StyleActionButton(privacy);
        privacy.Click += (_, _) =>
        {
            var settings = NexusContext.GetSettings?.Invoke() ?? WinUiShellState.Settings;
            _log.Text = AiPrivacyFirewallService.BuildFirewallReport(settings, _log.Text ?? "");
        };
        var compiler = new Button { Content = "Prompt compiler" };
        WinUiFluentChrome.StyleActionButton(compiler);
        compiler.Click += (_, _) =>
        {
            var settings = NexusContext.GetSettings?.Invoke() ?? WinUiShellState.Settings;
            _log.Text = PromptToFlowCompilerService.BuildCompiledFlow(settings, _log.Text ?? "");
        };
        var timeline = new Button { Content = "Process timeline" };
        WinUiFluentChrome.StyleActionButton(timeline);
        timeline.Click += (_, _) => _log.Text = AiProcessMiningTimelineService.BuildTimeline();
        var evidence = new Button { Content = "Evidence contract" };
        WinUiFluentChrome.StyleActionButton(evidence);
        evidence.Click += (_, _) =>
        {
            var settings = NexusContext.GetSettings?.Invoke() ?? WinUiShellState.Settings;
            _log.Text = AiEvidenceAnswerContractService.BuildContract(settings, _log.Text ?? "");
        };
        var axMemory = new Button { Content = "AX memory" };
        WinUiFluentChrome.StyleActionButton(axMemory);
        axMemory.Click += (_, _) =>
        {
            var settings = NexusContext.GetSettings?.Invoke() ?? WinUiShellState.Settings;
            _log.Text = AxFormMemoryService.BuildMemoryReport(settings);
        };
        var approvals = new Button { Content = "Approval center" };
        WinUiFluentChrome.StyleActionButton(approvals);
        approvals.Click += (_, _) => _log.Text = HumanApprovalCenterService.SeedDemoApproval();
        var riskSim = new Button { Content = "Risk sim" };
        WinUiFluentChrome.StyleActionButton(riskSim);
        riskSim.Click += (_, _) =>
        {
            var settings = NexusContext.GetSettings?.Invoke() ?? WinUiShellState.Settings;
            _log.Text = AiRiskSimulatorService.BuildRiskSimulation(settings, _log.Text ?? "");
        };
        var flowRoi = new Button { Content = "Flow ROI" };
        WinUiFluentChrome.StyleActionButton(flowRoi);
        flowRoi.Click += (_, _) => _log.Text = FlowRoiTelemetryService.BuildReport();
        var regression = new Button { Content = "AI regression" };
        WinUiFluentChrome.StyleActionButton(regression);
        regression.Click += (_, _) =>
        {
            var settings = NexusContext.GetSettings?.Invoke() ?? WinUiShellState.Settings;
            _log.Text = AiRegressionSuiteService.BuildRegressionReport(settings);
        };
        var pilotMode = new Button { Content = "Pilot mode" };
        WinUiFluentChrome.StyleActionButton(pilotMode, accent: true);
        pilotMode.Click += (_, _) =>
        {
            var settings = NexusContext.GetSettings?.Invoke() ?? WinUiShellState.Settings;
            DemoProgressTrackerService.Reset();
            _log.Text = ReleaseReadinessService.BuildPilotModeReport(settings) + "\n\n" + DemoProgressTrackerService.BuildProgressReport();
        };
        row.Children.Add(clear);
        row.Children.Add(export);
        row.Children.Add(report);
        row.Children.Add(audit);
        row.Children.Add(usp);
        row.Children.Add(uspPack);
        row.Children.Add(evalLab);
        row.Children.Add(evalPack);
        row.Children.Add(aiRoi);
        row.Children.Add(aiRoiPack);
        row.Children.Add(aiDemo);
        row.Children.Add(aiDemoPack);
        row.Children.Add(pilotProof);
        row.Children.Add(demoProgress);
        row.Children.Add(releaseReady);
        row.Children.Add(quality);
        row.Children.Add(dataset);
        row.Children.Add(privacy);
        row.Children.Add(compiler);
        row.Children.Add(timeline);
        row.Children.Add(evidence);
        row.Children.Add(axMemory);
        row.Children.Add(approvals);
        row.Children.Add(riskSim);
        row.Children.Add(flowRoi);
        row.Children.Add(regression);
        row.Children.Add(pilotMode);
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

public sealed class UspStudioShellPage : Page
{
    private readonly TextBox _output = new()
    {
        IsReadOnly = true,
        AcceptsReturn = true,
        TextWrapping = TextWrapping.Wrap,
        MinHeight = 280,
        FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas"),
        FontSize = 12
    };

    private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap };

    public UspStudioShellPage()
    {
        var root = new StackPanel { Spacing = 16, Margin = new Thickness(20, 16, 20, 24) };
        root.Children.Add(WinUiFluentChrome.PageTitle("USP Studio"));

        var intro = new TextBlock
        {
            Text = "Buyer-ready AI/KI proof cockpit: Pilot Proof, ROI, Governance, Privacy, AX Memory, Risk and Release Readiness.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = WinUiFluentChrome.SecondaryTextBrush
        };
        WinUiFluentChrome.ApplyBodyTextStyle(intro);
        root.Children.Add(intro);

        _status.Foreground = WinUiFluentChrome.SecondaryTextBrush;
        WinUiFluentChrome.ApplyCaptionTextStyle(_status);
        root.Children.Add(WinUiFluentChrome.WrapCard(_status, new Thickness(14, 10, 14, 10)));

        var hero = new Grid { ColumnSpacing = 12, RowSpacing = 12 };
        hero.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        hero.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        hero.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        AddTile(hero, 0, "Pilot Proof", "Master pack, demo runbook, buyer summary", "Export pack", ExportPilotProof);
        AddTile(hero, 1, "AI ROI", "Flow inventory, watch evidence, annualized value", "Run ROI", ShowRoi);
        AddTile(hero, 2, "Governance", "Evidence, autonomy, mutation and approval controls", "Show proof", ShowGovernance);
        root.Children.Add(hero);

        var grid = new Grid { ColumnSpacing = 12, RowSpacing = 12 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        AddSmallTile(grid, 0, 0, "Privacy Firewall", "Redaction and block decision before model calls.", "Scan", ShowPrivacy);
        AddSmallTile(grid, 0, 1, "Prompt-to-Flow", "Compile free text into a guarded flow draft.", "Compile", ShowPromptCompiler);
        AddSmallTile(grid, 1, 0, "AX/Form Memory", "Known operator forms from watch sessions.", "Show memory", ShowAxMemory);
        AddSmallTile(grid, 1, 1, "Risk + Regression", "Execution risk, eval dataset and release gate.", "Run checks", ShowRiskAndRegression);
        root.Children.Add(grid);

        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        AddAction(actions, "USP Studio report", ShowStudio);
        AddAction(actions, "Studio pack", ExportStudioPack, accent: true);
        AddAction(actions, "Pilot mode", StartPilotMode);
        AddAction(actions, "Demo progress", ShowDemoProgress);
        root.Children.Add(WinUiFluentChrome.WrapCard(actions, new Thickness(14, 12, 14, 12)));
        root.Children.Add(WinUiFluentChrome.WrapCard(_output, new Thickness(12, 10, 12, 10)));

        Content = new ScrollViewer { Content = root };
        Loaded += (_, _) => RefreshStatus();
    }

    private void AddTile(Grid host, int column, string title, string body, string action, Action run)
    {
        var panel = new StackPanel { Spacing = 10 };
        var titleBlock = new TextBlock { Text = title, FontSize = 22, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap };
        var bodyBlock = new TextBlock { Text = body, Foreground = WinUiFluentChrome.SecondaryTextBrush, TextWrapping = TextWrapping.Wrap };
        var button = new Button { Content = action, HorizontalAlignment = HorizontalAlignment.Left };
        WinUiFluentChrome.StyleActionButton(button, accent: column == 0);
        button.Click += (_, _) => run();
        panel.Children.Add(titleBlock);
        panel.Children.Add(bodyBlock);
        panel.Children.Add(button);
        var card = WinUiFluentChrome.WrapCard(panel, new Thickness(18, 16, 18, 16));
        Grid.SetColumn(card, column);
        host.Children.Add(card);
    }

    private void AddSmallTile(Grid host, int row, int column, string title, string body, string action, Action run)
    {
        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(new TextBlock { Text = title, FontSize = 17, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(new TextBlock { Text = body, Foreground = WinUiFluentChrome.SecondaryTextBrush, TextWrapping = TextWrapping.Wrap });
        var button = new Button { Content = action, HorizontalAlignment = HorizontalAlignment.Left };
        WinUiFluentChrome.StyleActionButton(button, compact: true);
        button.Click += (_, _) => run();
        panel.Children.Add(button);
        var card = WinUiFluentChrome.WrapCard(panel, new Thickness(16, 14, 16, 14));
        Grid.SetRow(card, row);
        Grid.SetColumn(card, column);
        host.Children.Add(card);
    }

    private void AddAction(StackPanel host, string label, Action run, bool accent = false)
    {
        var button = new Button { Content = label };
        WinUiFluentChrome.StyleActionButton(button, accent: accent);
        button.Click += (_, _) => run();
        host.Children.Add(button);
    }

    private NexusSettings Settings() => NexusContext.GetSettings?.Invoke() ?? WinUiShellState.Settings;

    private void RefreshStatus()
    {
        var settings = Settings();
        _status.Text = ReleaseReadinessService.BuildReadinessReport(settings).Split('\n').Take(3).Aggregate((a, b) => a + " · " + b);
    }

    private void ShowStudio() => _output.Text = UspStudioService.BuildStudioReport(Settings(), "");

    private void ExportStudioPack()
    {
        var path = UspStudioService.ExportStudioPack(Settings(), "");
        _output.Text = "USP Studio pack exported:\n" + path + "\n\n" + UspStudioService.BuildStudioReport(Settings(), "");
        NexusShell.Log("USP Studio pack → " + path);
        RefreshStatus();
    }

    private void ExportPilotProof()
    {
        DemoProgressTrackerService.Mark("buyer-pack-exported");
        var path = PilotProofPackService.ExportMasterPack(Settings(), "");
        _output.Text = "Pilot Proof Master Pack exported:\n" + path + "\n\n" + PilotProofPackService.BuildPilotSummary(Settings(), "");
        NexusShell.Log("Pilot Proof Master Pack → " + path);
        RefreshStatus();
    }

    private void ShowRoi() => _output.Text = AiRoiOpportunityService.BuildRoiReport(Settings(), "") + "\n\n" + FlowRoiTelemetryService.BuildReport();

    private void ShowGovernance() => _output.Text = AiGovernanceUspService.BuildEvidenceModeReport(Settings(), "") + "\n\n" + HumanApprovalCenterService.SeedDemoApproval();

    private void ShowPrivacy() => _output.Text = AiPrivacyFirewallService.BuildFirewallReport(Settings(), _output.Text ?? "");

    private void ShowPromptCompiler() => _output.Text = PromptToFlowCompilerService.BuildCompiledFlow(Settings(), _output.Text ?? "");

    private void ShowAxMemory() => _output.Text = AxFormMemoryService.BuildMemoryReport(Settings());

    private void ShowRiskAndRegression() => _output.Text = AiRiskSimulatorService.BuildRiskSimulation(Settings(), _output.Text ?? "") + "\n\n" + AiRegressionSuiteService.BuildRegressionReport(Settings());

    private void StartPilotMode()
    {
        DemoProgressTrackerService.Reset();
        _output.Text = ReleaseReadinessService.BuildPilotModeReport(Settings()) + "\n\n" + DemoProgressTrackerService.BuildProgressReport();
        RefreshStatus();
    }

    private void ShowDemoProgress() => _output.Text = DemoProgressTrackerService.BuildProgressReport();
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
