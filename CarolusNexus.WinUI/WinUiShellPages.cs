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
                : "Running observed foreground plan through guarded execution path...";
            _observeOutput.Text = "";
            var log = await ComputerUseLoopService.RunThroughSimulatorAsync(
                ComputerUseLoopService.BuildObservedPlanSteps(WinUiShellState.Settings),
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
        var executionEvidence = new Button { Content = "Execution evidence" };
        WinUiFluentChrome.StyleActionButton(executionEvidence);
        executionEvidence.Click += (_, _) => _log.Text = ExecutionEvidenceService.BuildReport();
        var recovery = new Button { Content = "Recovery guide" };
        WinUiFluentChrome.StyleActionButton(recovery);
        recovery.Click += (_, _) =>
            _log.Text = RecoverySuggestionService.BuildSuggestion(
                new CarolusNexus.Models.RecipeStep { ActionArgument = _log.Text ?? "" },
                _log.Text ?? "",
                NexusContext.GetSettings?.Invoke() ?? WinUiShellState.Settings);
        var adaptiveMemory = new Button { Content = "Adaptive memory" };
        WinUiFluentChrome.StyleActionButton(adaptiveMemory);
        adaptiveMemory.Click += (_, _) => _log.Text = AdaptiveOperatorMemoryService.BuildReport();
        var missionTimeline = new Button { Content = "Mission timeline" };
        WinUiFluentChrome.StyleActionButton(missionTimeline);
        missionTimeline.Click += (_, _) => _log.Text = MissionTimelineService.BuildTimeline();
        var sop = new Button { Content = "Watch to SOP" };
        WinUiFluentChrome.StyleActionButton(sop);
        sop.Click += (_, _) => _log.Text = WatchToSopGeneratorService.BuildSop();
        var drift = new Button { Content = "Drift detect" };
        WinUiFluentChrome.StyleActionButton(drift);
        drift.Click += (_, _) => _log.Text = DriftDetectionService.BuildReport();
        var missionScore = new Button { Content = "Mission score" };
        WinUiFluentChrome.StyleActionButton(missionScore);
        missionScore.Click += (_, _) => _log.Text = MissionControlScoreService.BuildScore();
        var heatmap = new Button { Content = "Confidence heatmap" };
        WinUiFluentChrome.StyleActionButton(heatmap);
        heatmap.Click += (_, _) =>
        {
            var settings = NexusContext.GetSettings?.Invoke() ?? WinUiShellState.Settings;
            var latest = ActionHistoryService.GetLatestPlanRunWithSteps();
            _log.Text = latest == null
                ? "(no plan run history yet for heatmap)"
                : ConfidenceHeatmapService.BuildHeatmap(latest.Steps, settings);
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
        var groupedActions = new StackPanel { Spacing = 12 };
        groupedActions.Children.Add(DiagnosticsGroup("Runtime", "Local app state, runtime checks and exportable diagnostics.", clear, export, report, audit, releaseReady));
        groupedActions.Children.Add(DiagnosticsGroup("AI / Evaluation", "USP reporting, evaluation lab, quality badges, drift and dataset builder.", usp, uspPack, evalLab, evalPack, quality, dataset, missionScore, heatmap, drift));
        groupedActions.Children.Add(DiagnosticsGroup("Sales / Pilot", "ROI, demo orchestration, pilot proof and pilot-mode progress.", aiRoi, aiRoiPack, aiDemo, aiDemoPack, pilotProof, demoProgress, pilotMode));
        groupedActions.Children.Add(DiagnosticsGroup("Enterprise controls", "Privacy, prompt compiler, evidence, AX memory, approvals, SOP, timeline and recovery.", privacy, compiler, timeline, evidence, executionEvidence, adaptiveMemory, missionTimeline, sop, recovery, axMemory, approvals, riskSim, flowRoi, regression));
        sp.Children.Add(new DevWinUI.SettingsExpander
        {
            Header = "Diagnostics command deck",
            Description = "Runtime checks, proof packs, AI evaluation, governance and pilot readiness actions.",
            IsExpanded = true,
            Content = groupedActions
        });
        sp.Children.Add(new DevWinUI.SettingsCard
        {
            Header = "Diagnostics output",
            Description = "Combined run output, release evidence and exported report summaries.",
            Content = _log
        });
        Content = new ScrollViewer { Content = sp };
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private static DevWinUI.SettingsCard DiagnosticsGroup(string header, string description, params Button[] buttons)
    {
        var wrap = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4,
            Margin = new Thickness(0, 6, 0, 0)
        };
        foreach (var button in buttons)
        {
            button.Margin = new Thickness(0, 0, 8, 8);
            wrap.Children.Add(button);
        }

        return new DevWinUI.SettingsCard
        {
            Header = header,
            Description = description,
            Content = wrap
        };
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
        root.Children.Add(new DevWinUI.SettingsCard
        {
            Header = "Studio readiness",
            Description = "Release gate, pilot mode and proof-pack status in one native DevWinUI card.",
            Content = _status
        });

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
        AddAction(actions, "Execution evidence", ShowExecutionEvidence);
        AddAction(actions, "Adaptive memory", ShowAdaptiveMemory);
        AddAction(actions, "Mission timeline", ShowMissionTimeline);
        AddAction(actions, "Watch to SOP", ShowWatchToSop);
        AddAction(actions, "Mission score", ShowMissionScore);
        AddAction(actions, "Drift detect", ShowDrift);
        AddAction(actions, "Heatmap", ShowConfidenceHeatmap);
        root.Children.Add(new DevWinUI.SettingsExpander
        {
            Header = "Studio actions",
            Description = "Buyer proof, pilot mode and demo progress actions for live sales and pilot sessions.",
            IsExpanded = true,
            Content = actions
        });
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
        var card = new DevWinUI.SettingsCard
        {
            Header = title,
            Description = body,
            Content = panel,
            Padding = new Thickness(14)
        };
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
        var card = new DevWinUI.SettingsCard
        {
            Header = title,
            Description = body,
            Content = panel,
            Padding = new Thickness(12)
        };
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

    private void ShowExecutionEvidence() => _output.Text = ExecutionEvidenceService.BuildReport();

    private void ShowAdaptiveMemory() => _output.Text = AdaptiveOperatorMemoryService.BuildReport();

    private void ShowMissionTimeline() => _output.Text = MissionTimelineService.BuildTimeline();

    private void ShowWatchToSop() => _output.Text = WatchToSopGeneratorService.BuildSop();

    private void ShowMissionScore() => _output.Text = MissionControlScoreService.BuildScore();

    private void ShowDrift() => _output.Text = DriftDetectionService.BuildReport();

    private void ShowConfidenceHeatmap()
    {
        var latest = ActionHistoryService.GetLatestPlanRunWithSteps();
        _output.Text = latest == null ? "(no plan run history yet for heatmap)" : ConfidenceHeatmapService.BuildHeatmap(latest.Steps, Settings());
    }

    private void StartPilotMode()
    {
        DemoProgressTrackerService.Reset();
        _output.Text = ReleaseReadinessService.BuildPilotModeReport(Settings()) + "\n\n" + DemoProgressTrackerService.BuildProgressReport();
        RefreshStatus();
    }

    private void ShowDemoProgress() => _output.Text = DemoProgressTrackerService.BuildProgressReport();
}

public sealed class UiLabShellPage : Page
{
    public UiLabShellPage()
    {
        var root = new Grid
        {
            Padding = new Thickness(28),
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }
            }
        };

        var header = new StackPanel { Spacing = 8, Margin = new Thickness(0, 0, 0, 20) };
        header.Children.Add(new TextBlock
        {
            Text = "UI Lab",
            FontSize = 34,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        });
        header.Children.Add(new TextBlock
        {
            Text = "DevWinUI.Controls ist aktiv. Diese Seite zeigt echte DevWinUI-Komponenten isoliert, bevor sie in USP Studio und Diagnostics wandern.",
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.78
        });
        Grid.SetRow(header, 0);
        root.Children.Add(header);

        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        var content = new StackPanel { Spacing = 16 };

        content.Children.Add(new DevWinUI.SettingsCard
        {
            Header = "DevWinUI SettingsCard",
            Description = "Sichtbarer Nachweis: diese Karte kommt aus DevWinUI.Controls und nutzt die globale DevWinUI.Controls Resource.",
            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 10,
                Children =
                {
                    new Button { Content = "Primary action" },
                    new Button { Content = "Secondary" }
                }
            }
        });

        var expander = new DevWinUI.SettingsExpander
        {
            Header = "DevWinUI SettingsExpander",
            Description = "Für USP-Cluster, Diagnosegruppen und KI-Governance-Sektionen.",
            Content = new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    new TextBlock { Text = "AI ROI, Governance, Privacy Firewall und Regression Suite können hier als gruppierte Controls erscheinen.", TextWrapping = TextWrapping.Wrap },
                    new ToggleSwitch { Header = "Pilot-safe UX pattern", IsOn = true },
                    new ToggleSwitch { Header = "Enterprise readiness badges", IsOn = true }
                }
            }
        };
        content.Children.Add(expander);

        content.Children.Add(new DevWinUI.SettingsCard
        {
            Header = "Nächste Übernahme",
            Description = "Wenn dieses Lab stabil läuft, werden die gleichen Patterns in USP Studio und Diagnostics verwendet.",
            Content = new ProgressBar { Value = 72, Maximum = 100, MinWidth = 220 }
        });

        scroll.Content = content;
        Grid.SetRow(scroll, 1);
        root.Children.Add(scroll);
        Content = root;
    }
}

public sealed class BackendCoverageShellPage : Page
{
    private readonly TextBox _output = new()
    {
        IsReadOnly = true,
        AcceptsReturn = true,
        TextWrapping = TextWrapping.NoWrap,
        FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas"),
        MinHeight = 520
    };

    public BackendCoverageShellPage()
    {
        var root = new StackPanel { Spacing = 14, Padding = new Thickness(28, 24, 28, 28) };
        root.Children.Add(WinUiFluentChrome.PageTitle("Backend Coverage"));
        root.Children.Add(new TextBlock
        {
            Text = "Runtime coverage across WinUI surfaces, backend services, adapters and guarded execution.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = WinUiFluentChrome.SecondaryTextBrush
        });

        var summary = new Grid { ColumnSpacing = 12, RowSpacing = 12 };
        summary.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        summary.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        summary.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        AddSummaryCard(summary, 0, "Coverage", "Surface map for backend capabilities.", "Open", ShowCoverage, accent: true);
        AddSummaryCard(summary, 1, "Gaps", "Guarded, disabled and report-only capabilities.", "Inspect", ShowGaps);
        AddSummaryCard(summary, 2, "AX Workbench", "AX status, token readiness and foreground probe.", "Probe", ShowAxWorkbench);
        root.Children.Add(summary);

        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        AddAction(actions, "Service inventory", ShowServices);
        AddAction(actions, "Runtime files", ShowRuntimeFiles);
        AddAction(actions, "Token matrix", ShowTokenMatrix);
        AddAction(actions, "Refresh foreground", ShowAxWorkbench);
        root.Children.Add(new DevWinUI.SettingsExpander
        {
            Header = "Backend actions",
            Description = "Direct reports for runtime coverage, adapters, persisted data and known executable tokens.",
            IsExpanded = true,
            Content = actions
        });

        root.Children.Add(WinUiFluentChrome.WrapCard(_output, new Thickness(12, 10, 12, 10)));
        Content = new ScrollViewer { Content = root };
        Loaded += (_, _) => ShowCoverage();
    }

    private static NexusSettings Settings() => NexusContext.GetSettings?.Invoke() ?? WinUiShellState.Settings;

    private void AddSummaryCard(Grid host, int column, string title, string body, string action, Action run, bool accent = false)
    {
        var button = new Button { Content = action, HorizontalAlignment = HorizontalAlignment.Left };
        WinUiFluentChrome.StyleActionButton(button, accent: accent);
        button.Click += (_, _) => run();
        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(new TextBlock { Text = title, FontSize = 18, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(new TextBlock { Text = body, Foreground = WinUiFluentChrome.SecondaryTextBrush, TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(button);
        var card = new DevWinUI.SettingsCard
        {
            Header = title,
            Description = body,
            Content = panel,
            Padding = new Thickness(14)
        };
        Grid.SetColumn(card, column);
        host.Children.Add(card);
    }

    private void AddAction(StackPanel host, string label, Action run)
    {
        var button = new Button { Content = label };
        WinUiFluentChrome.StyleActionButton(button);
        button.Click += (_, _) => run();
        host.Children.Add(button);
    }

    private void ShowCoverage() => _output.Text = BackendCoverageService.BuildCoverageReport(Settings());

    private void ShowGaps() => _output.Text = BackendCoverageService.BuildGapReport(Settings());

    private void ShowAxWorkbench() => _output.Text = BackendCoverageService.BuildAxWorkbenchReport(Settings());

    private void ShowServices() => _output.Text = BackendCoverageService.BuildServiceInventoryReport();

    private void ShowRuntimeFiles() => _output.Text = BackendCoverageService.BuildRuntimeFilesReport();

    private void ShowTokenMatrix()
    {
        var settings = Settings();
        var steps = new[]
        {
            new RecipeStep { ActionType = "Win32 hotkey", Channel = "ui", ActionArgument = "[ACTION:hotkey|Ctrl+L]" },
            new RecipeStep { ActionType = "Win32 type", Channel = "ui", ActionArgument = "[ACTION:type|demo]" },
            new RecipeStep { ActionType = "Browser open", Channel = "ui", ActionArgument = "browser.open:https://example.com" },
            new RecipeStep { ActionType = "Explorer path", Channel = "ui", ActionArgument = "explorer.open_path:C:\\\\" },
            new RecipeStep { ActionType = "UIA invoke", Channel = "ui", ActionArgument = "uia.invoke:OK" },
            new RecipeStep { ActionType = "AX context", Channel = "ui", ActionArgument = "ax.read_context" },
            new RecipeStep { ActionType = "AX write", Channel = "ui", ActionArgument = "ax.setvalue:Field=Value" },
            new RecipeStep { ActionType = "API get", Channel = "api", ActionArgument = "api.get:https://example.com" },
            new RecipeStep { ActionType = "Script", Channel = "script", ActionArgument = "powershell:Get-Date" },
            new RecipeStep { ActionType = "Unknown", Channel = "ui", ActionArgument = "[ACTION:unknown|x]" }
        };
        _output.Text = AutomationTokenReadiness.BuildReport(steps, settings);
    }
}

public sealed class ExcelAxCheckShellPage : Page
{
    private readonly TextBox _filePath = new() { Header = "Excel/CSV file", PlaceholderText = "Select .xlsx or .csv" };
    private readonly ComboBox _preset = new() { Header = "AX preset" };
    private readonly ComboBox _keyColumn = new() { Header = "Excel key column" };
    private readonly TextBox _axEntity = new() { Header = "AX OData entity" };
    private readonly TextBox _axKeyField = new() { Header = "AX key field" };
    private readonly TextBox _maxRows = new() { Header = "Max rows", Text = "500" };
    private readonly TextBox _runQuestion = new() { Header = "Ask latest run", PlaceholderText = "Which rows block the AX check?" };
    private readonly TextBox _preview = new()
    {
        IsReadOnly = true,
        AcceptsReturn = true,
        TextWrapping = TextWrapping.NoWrap,
        FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas"),
        MinHeight = 220
    };
    private readonly TextBox _result = new()
    {
        IsReadOnly = true,
        AcceptsReturn = true,
        TextWrapping = TextWrapping.NoWrap,
        FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas"),
        MinHeight = 360
    };
    private ExcelAxSourcePreview? _loaded;
    private ExcelAxValidationRun? _lastRun;

    public ExcelAxCheckShellPage()
    {
        foreach (var p in ExcelAxValidationService.Presets)
            _preset.Items.Add(p);
        _preset.SelectedIndex = 0;
        _preset.SelectionChanged += (_, _) => ApplyDefaultPreset();

        var root = new StackPanel { Spacing = 14, Margin = new Thickness(20, 16, 20, 20) };
        root.Children.Add(WinUiFluentChrome.PageTitle("Excel + AX Check"));
        root.Children.Add(new TextBlock
        {
            Text = "Read-only workbench for checking Excel lists against AX 2012 R3 CU13. No AX write/post/book action is executed here.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = WinUiFluentChrome.SecondaryTextBrush
        });

        var pick = new Button { Content = "Select file" };
        WinUiFluentChrome.StyleActionButton(pick, accent: true);
        pick.Click += (_, _) => SelectFile();
        var load = new Button { Content = "Load preview" };
        WinUiFluentChrome.StyleActionButton(load);
        load.Click += (_, _) => LoadPreview();
        var validate = new Button { Content = "Run read-only check" };
        WinUiFluentChrome.StyleActionButton(validate, accent: true);
        validate.Click += (_, _) => RunValidation();
        var copilot = new Button { Content = "AI reconciliation" };
        WinUiFluentChrome.StyleActionButton(copilot);
        copilot.Click += (_, _) => ShowCopilotReport();
        var inbox = new Button { Content = "Exception inbox" };
        WinUiFluentChrome.StyleActionButton(inbox);
        inbox.Click += (_, _) => ShowExceptionInbox();
        var pilotPack = new Button { Content = "Create pilot pack" };
        WinUiFluentChrome.StyleActionButton(pilotPack);
        pilotPack.Click += (_, _) => CreatePilotPack();
        var diff = new Button { Content = "Smart diff" };
        WinUiFluentChrome.StyleActionButton(diff);
        diff.Click += (_, _) => ShowSmartDiff();
        var safeMode = new Button { Content = "Safe-mode cert" };
        WinUiFluentChrome.StyleActionButton(safeMode);
        safeMode.Click += (_, _) => CreateSafeModeCertificate();
        var ask = new Button { Content = "Ask run" };
        WinUiFluentChrome.StyleActionButton(ask, accent: true);
        ask.Click += (_, _) => AskLatestRun();
        var previewAi = new Button { Content = "Preview AI" };
        WinUiFluentChrome.StyleActionButton(previewAi);
        previewAi.Click += (_, _) => ShowPreviewIntelligence();
        var taskBoard = new Button { Content = "Task board" };
        WinUiFluentChrome.StyleActionButton(taskBoard);
        taskBoard.Click += (_, _) => ShowTaskBoard();
        var roi = new Button { Content = "ROI/process" };
        WinUiFluentChrome.StyleActionButton(roi);
        roi.Click += (_, _) => ShowRoiAndProcess();
        var fixExport = new Button { Content = "Fix export" };
        WinUiFluentChrome.StyleActionButton(fixExport);
        fixExport.Click += (_, _) => CreateFixExport();
        var bundle = new Button { Content = "Evidence ZIP" };
        WinUiFluentChrome.StyleActionButton(bundle);
        bundle.Click += (_, _) => CreateEvidenceBundle();
        var dashboard = new Button { Content = "Run dashboard" };
        WinUiFluentChrome.StyleActionButton(dashboard);
        dashboard.Click += (_, _) => ShowRunDashboard();
        var openFolder = new Button { Content = "Open exports" };
        WinUiFluentChrome.StyleActionButton(openFolder);
        openFolder.Click += (_, _) => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(AppPaths.ExcelAxChecksDir) { UseShellExecute = true });

        var actionRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Children = { pick, load, validate, previewAi, copilot, inbox, taskBoard, roi, fixExport, bundle, dashboard, pilotPack, diff, safeMode, openFolder } };
        root.Children.Add(WinUiFluentChrome.WrapCard(new StackPanel
        {
            Spacing = 12,
            Children = { _filePath, actionRow }
        }));

        var config = new Grid { ColumnSpacing = 12, RowSpacing = 12 };
        config.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        config.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        config.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        config.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        config.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
        AddToGrid(config, _preset, 0);
        AddToGrid(config, _keyColumn, 1);
        AddToGrid(config, _axEntity, 2);
        AddToGrid(config, _axKeyField, 3);
        AddToGrid(config, _maxRows, 4);
        root.Children.Add(WinUiFluentChrome.WrapCard(config));

        var askRow = new Grid { ColumnSpacing = 10 };
        askRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        askRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(_runQuestion, 0);
        Grid.SetColumn(ask, 1);
        askRow.Children.Add(_runQuestion);
        askRow.Children.Add(ask);
        root.Children.Add(WinUiFluentChrome.WrapCard(askRow));

        var status = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        var settings = Settings();
        status.Children.Add(WinUiFluentChrome.StatusTile("Mode", "read-only", "AX mutations disabled"));
        status.Children.Add(WinUiFluentChrome.StatusTile("AX", settings.AxIntegrationEnabled ? "enabled" : "disabled", string.IsNullOrWhiteSpace(settings.AxODataBaseUrl) ? "UIA fallback" : "OData first"));
        status.Children.Add(WinUiFluentChrome.StatusTile("Evidence", AppPaths.ExcelAxChecksDir, "csv + json"));
        root.Children.Add(WinUiFluentChrome.WrapCard(status));

        root.Children.Add(WinUiFluentChrome.ColumnCaption("Preview"));
        root.Children.Add(WinUiFluentChrome.WrapCard(_preview, new Thickness(12, 10, 12, 10)));
        root.Children.Add(WinUiFluentChrome.ColumnCaption("Result"));
        root.Children.Add(WinUiFluentChrome.WrapCard(_result, new Thickness(12, 10, 12, 10)));

        Content = new ScrollViewer { Content = root };
    }

    private static NexusSettings Settings() => NexusContext.GetSettings?.Invoke() ?? WinUiShellState.Settings;

    private static void AddToGrid(Grid grid, FrameworkElement element, int column)
    {
        Grid.SetColumn(element, column);
        grid.Children.Add(element);
    }

    private void SelectFile()
    {
        using var dlg = new System.Windows.Forms.OpenFileDialog
        {
            Filter = "Excel/CSV (*.xlsx;*.csv)|*.xlsx;*.csv|All files (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };
        if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            _filePath.Text = dlg.FileName;
            LoadPreview();
        }
    }

    private void LoadPreview()
    {
        try
        {
            _loaded = ExcelAxValidationService.LoadPreview(_filePath.Text.Trim(), 60);
            _keyColumn.Items.Clear();
            foreach (var c in _loaded.Columns)
                _keyColumn.Items.Add(c);
            _keyColumn.SelectedItem = _loaded.SuggestedKeyColumn;
            ApplyDefaultPreset();
            _preview.Text = BuildPreviewText(_loaded);
            _result.Text = "Preview loaded. Run read-only check to create evidence export.";
        }
        catch (Exception ex)
        {
            _preview.Text = ex.ToString();
            _result.Text = "";
        }
    }

    private void ApplyDefaultPreset()
    {
        if (_loaded == null)
            return;
        var opt = ExcelAxValidationService.BuildDefaultOptions(_loaded, _preset.SelectedItem?.ToString());
        if (_keyColumn.SelectedItem == null)
            _keyColumn.SelectedItem = opt.KeyColumn;
        _axEntity.Text = opt.AxEntity;
        _axKeyField.Text = opt.AxKeyField;
        _maxRows.Text = opt.MaxRows.ToString();
    }

    private void RunValidation()
    {
        try
        {
            if (_loaded == null)
                LoadPreview();
            if (_loaded == null)
                return;
            var opt = CurrentOptions();
            _lastRun = ExcelAxValidationService.Validate(_loaded.FilePath, opt, Settings());
            _result.Text = ExcelAxValidationService.BuildRunReport(_lastRun);
            NexusShell.Log("Excel + AX check exported: " + _lastRun.ExportPath);
        }
        catch (Exception ex)
        {
            _result.Text = ex.ToString();
        }
    }

    private void ShowCopilotReport()
    {
        if (_lastRun == null)
        {
            _result.Text = "Run read-only check first. The AI Reconciliation Copilot uses the latest validation evidence.";
            return;
        }

        _result.Text = ExcelAxValidationService.BuildReconciliationCopilotReport(_lastRun);
    }

    private void ShowExceptionInbox()
    {
        if (_lastRun == null)
        {
            _result.Text = "Run read-only check first. The AI Exception Inbox uses the latest validation evidence.";
            return;
        }

        _result.Text = ExcelAxValidationService.BuildReadinessScoreReport(_lastRun)
            + "\n\n"
            + ExcelAxValidationService.BuildExceptionInboxReport(_lastRun);
    }

    private void CreatePilotPack()
    {
        if (_lastRun == null)
        {
            _result.Text = "Run read-only check first. The pilot pack is generated from the latest validation evidence.";
            return;
        }

        var pack = ExcelAxValidationService.CreatePilotPack(_lastRun);
        _result.Text = pack.Summary
            + "\n\nMarkdown: " + pack.MarkdownPath
            + "\nCSV: " + pack.CsvPath
            + "\nJSON: " + pack.JsonPath
            + "\n\n" + ExcelAxValidationService.BuildExceptionInboxReport(_lastRun);
        NexusShell.Log("Excel + AX pilot pack created: " + pack.MarkdownPath);
    }

    private void ShowSmartDiff()
    {
        if (_lastRun == null)
        {
            _result.Text = "Run read-only check first. Smart Diff compares the latest validation evidence with the previous run for the same file.";
            return;
        }

        _result.Text = ExcelAxValidationService.BuildRunDiffReport(_lastRun);
    }

    private void CreateSafeModeCertificate()
    {
        if (_lastRun == null)
        {
            _result.Text = "Run read-only check first. The safe-mode certificate is generated from the latest validation evidence.";
            return;
        }

        var cert = ExcelAxValidationService.CreateSafeModeCertificate(_lastRun);
        _result.Text = ExcelAxValidationService.BuildSafeModeCertificateReport(_lastRun)
            + "\n\nCertificate: " + cert.CertificatePath;
        NexusShell.Log("Excel + AX safe-mode certificate created: " + cert.CertificatePath);
    }

    private void AskLatestRun()
    {
        if (_lastRun == null)
        {
            _result.Text = "Run read-only check first. Natural-language answers use the latest validation evidence.";
            return;
        }

        _result.Text = ExcelAxValidationService.AnswerRunQuestion(_lastRun, _runQuestion.Text);
    }

    private void ShowPreviewIntelligence()
    {
        if (_loaded == null)
        {
            LoadPreview();
            if (_loaded == null)
                return;
        }

        _result.Text = ExcelAxValidationService.BuildPreviewIntelligenceReport(_loaded, CurrentOptions(), _lastRun);
    }

    private void ShowTaskBoard()
    {
        if (_lastRun == null)
        {
            _result.Text = "Run read-only check first. The operator task board uses the latest validation evidence.";
            return;
        }

        _result.Text = ExcelAxValidationService.BuildOperatorTaskBoardReport(_lastRun)
            + "\n\n"
            + ExcelAxValidationService.BuildFixProposalPackReport(_lastRun);
    }

    private void ShowRoiAndProcess()
    {
        if (_lastRun == null)
        {
            _result.Text = "Run read-only check first. ROI and Process Twin use the latest validation evidence.";
            return;
        }

        _result.Text = ExcelAxValidationService.BuildProcessTwinReport(_lastRun)
            + "\n\n"
            + ExcelAxValidationService.BuildRoiEstimatorReport(_lastRun)
            + "\n\n"
            + ExcelAxValidationService.BuildEvidenceTimelineReport(_lastRun);
    }

    private void CreateFixExport()
    {
        if (_lastRun == null)
        {
            _result.Text = "Run read-only check first. The fix export is generated from the latest validation evidence.";
            return;
        }

        var export = ExcelAxValidationService.CreateFixExportCsv(_lastRun);
        _result.Text = export.Summary + "\n\nCSV: " + export.CsvPath;
        NexusShell.Log("Excel + AX fix export created: " + export.CsvPath);
    }

    private void CreateEvidenceBundle()
    {
        if (_lastRun == null)
        {
            _result.Text = "Run read-only check first. The evidence ZIP is generated from the latest validation evidence.";
            return;
        }

        var zip = ExcelAxValidationService.CreateEvidenceBundleZip(_lastRun);
        _result.Text = zip.Summary
            + "\n\nZIP: " + zip.ZipPath
            + "\n\nIncluded:\n- " + string.Join("\n- ", zip.IncludedFiles);
        NexusShell.Log("Excel + AX evidence bundle created: " + zip.ZipPath);
    }

    private void ShowRunDashboard()
    {
        _result.Text = ExcelAxValidationService.BuildRunDashboardReport();
    }

    private ExcelAxValidationOptions CurrentOptions() =>
        new(
            _preset.SelectedItem?.ToString() ?? ExcelAxValidationService.Presets[0],
            _keyColumn.SelectedItem?.ToString() ?? _loaded?.SuggestedKeyColumn ?? "",
            _axEntity.Text.Trim(),
            _axKeyField.Text.Trim(),
            int.TryParse(_maxRows.Text.Trim(), out var maxRows) ? Math.Clamp(maxRows, 1, 10000) : 500);

    private static string BuildPreviewText(ExcelAxSourcePreview preview)
    {
        var sb = new StringBuilder();
        sb.AppendLine("File: " + preview.FilePath);
        sb.AppendLine("Sheet: " + preview.SheetName);
        sb.AppendLine("Suggested key: " + preview.SuggestedKeyColumn);
        sb.AppendLine();
        sb.AppendLine("Columns");
        foreach (var p in preview.Profiles)
            sb.AppendLine($"- {p.Index + 1}: {p.Name}; non-empty={p.NonEmptyCount}; key-candidate={p.LooksLikeKey}");
        sb.AppendLine();
        sb.AppendLine(string.Join(" | ", preview.Columns));
        foreach (var row in preview.Rows.Take(20))
            sb.AppendLine(string.Join(" | ", row.Select(v => v.Length > 32 ? v[..32] + "..." : v)));
        return sb.ToString().TrimEnd();
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
