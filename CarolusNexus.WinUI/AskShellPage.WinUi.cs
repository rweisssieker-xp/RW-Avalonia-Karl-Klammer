using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CarolusNexus;
using CarolusNexus.Models;
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

/// <summary>Full Ask workflow (plans, PTT, speak) — parity target for Avalonia Ask tab.</summary>
public sealed class AskShellPage : Page
{
    private sealed class PlanPreviewRow
    {
        public int Step { get; init; }
        public string ActionType { get; init; } = "";
        public string Target { get; init; } = "";
        public string Risk { get; init; } = "";
        public string Status { get; init; } = "";
        public int WaitMs { get; init; }
    }

    private readonly TextBox _prompt = new() { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 100 };
    private readonly TextBox _assistant = new() { IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 100 };
    private readonly TextBox _retrieval = new() { IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 56 };
    private readonly TextBox _planPreview = new() { IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 80, FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas") };
    private readonly ListView _planTable = new() { MinHeight = 160, SelectionMode = ListViewSelectionMode.None };
    private readonly TextBox _planExec = new() { IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 100, FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas") };
    private readonly TextBlock _executionStatusLine = new() { TextWrapping = TextWrapping.Wrap };
    private readonly TextBox _executionDetail = new()
    {
        IsReadOnly = true,
        AcceptsReturn = true,
        MinHeight = 84,
        TextWrapping = TextWrapping.Wrap,
        FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas"),
        FontSize = 11
    };
    private readonly CheckBox _shots = new() { Content = "Include screenshots", IsChecked = true };
    private readonly CheckBox _know = new() { Content = "Use local knowledge", IsChecked = true };
    private readonly InfoBar _busy = new() { IsOpen = false, Title = "Working" };
    private readonly TextBlock _commandStatus = new() { TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _nextAction = new() { TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _safetyGate = new() { TextWrapping = TextWrapping.Wrap };
    private readonly Button _nbaPrimary = new();
    private readonly Button _nbaSecondary = new();
    private readonly Button _nbaDismiss = new();
    private Border? _nextBestActionBar;
    private NextBestAction? _nextBestAction;
    private readonly InfoBar _planRisk = new()
    {
        IsOpen = true,
        Title = "Plan risk",
        Message = "No plan yet.",
        Severity = InfoBarSeverity.Informational
    };
    private readonly Grid _root = new() { Padding = new Thickness(20, 16, 20, 16) };
    private CancellationTokenSource? _cts;
    private readonly List<RecipeStep> _planSteps = new();
    private int _planStepIndex;
    private string _executionState = "idle";
    private string _executionMode = "none";
    private string _executionError = "";
    private int _executionDoneSteps;
    private int _executionTotalSteps;
    private string _executionLastStep = "";
    private WindowsMicRecorder? _mic;
    private bool _isRecording;
    private bool _awaitGlobalHotkeyRelease;
    private RadicalPlan? _lastRadicalPlan;
    private RadicalExecutionReport? _lastRadicalRun;
    public AskShellPage()
    {
        var bAsk = WinUiFluentChrome.AppBarCommand("Ask now", "\uE768", async (_, _) => await RunAskAsync(), "Ctrl+Enter", VirtualKey.Enter, VirtualKeyModifiers.Control);
        var bSmoke = WinUiFluentChrome.AppBarCommand("Smoke test", "\uE9CE", async (_, _) => await RunSmokeAsync(), "Ctrl+T", VirtualKey.T, VirtualKeyModifiers.Control);
        var bImport = WinUiFluentChrome.AppBarCommand("Import audio + transcribe", "\uE8B5", async (_, _) => await ImportAudioAsync(), "Ctrl+I", VirtualKey.I, VirtualKeyModifiers.Control);
        var bPtt0 = WinUiFluentChrome.AppBarCommand("Start push-to-talk", "\uE720", (_, _) => StartMic("(button)", false), "F6", VirtualKey.F6);
        var bPtt1 = WinUiFluentChrome.AppBarCommand("Stop + ask", "\uE71A", async (_, _) => await StopMicTranscribeAndAskAsync(), "Shift+F6", VirtualKey.F6, VirtualKeyModifiers.Shift);
        var bCancel = WinUiFluentChrome.AppBarCommand("Cancel recording", "\uE711", (_, _) => CancelMic(), "Ctrl+Esc", VirtualKey.Escape, VirtualKeyModifiers.Control);
        var bRun = WinUiFluentChrome.AppBarCommand("Run plan", "\uE768", async (_, _) => await ExecutePlanAsync(false), "F9", VirtualKey.F9);
        var bApr = WinUiFluentChrome.AppBarCommand("Approve + run", "\uE73E", async (_, _) => await ExecutePlanAfterConfirmAsync(), "Shift+F9", VirtualKey.F9, VirtualKeyModifiers.Shift);
        var bNext = WinUiFluentChrome.AppBarCommand("Run next step", "\uE72A", async (_, _) => await RunNextPlanStepAsync(), "F10", VirtualKey.F10);
        var bSave = WinUiFluentChrome.AppBarCommand("Save plan as flow", "\uE74E", (_, _) => SavePlanAsRitual(), "Ctrl+S", VirtualKey.S, VirtualKeyModifiers.Control);
        var bClr = WinUiFluentChrome.AppBarCommand("Clear plan", "\uE74D", (_, _) =>
        {
            _planPreview.Text = "";
            _planExec.Text = "";
            _planSteps.Clear();
            _planStepIndex = 0;
            _planTable.ItemsSource = Array.Empty<PlanPreviewRow>();
            _planRisk.Severity = InfoBarSeverity.Informational;
            _planRisk.Message = "No plan yet.";
            SetExecutionState("idle", "none", 0, 0, "");
            SetExecutionLastStep(null);
        }, "Ctrl+L", VirtualKey.L, VirtualKeyModifiers.Control);
        var bPanic = WinUiFluentChrome.AppBarCommand("Panic stop", "\uE711", (_, _) =>
        {
            _cts?.Cancel();
            NexusShell.Log("panic stop");
        }, "Esc", VirtualKey.Escape);
        var bSpeak = WinUiFluentChrome.AppBarCommand("Speak response", "\uE995", async (_, _) => await SpeakAsync(), "Ctrl+Shift+V", VirtualKey.V, VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift);

        var toolInner = new StackPanel { Spacing = 10 };
        toolInner.Children.Add(WinUiFluentChrome.ColumnCaption("Ask, voice, and plans"));
        toolInner.Children.Add(WinUiFluentChrome.CommandSurface("Ask", bAsk, bSmoke));
        toolInner.Children.Add(WinUiFluentChrome.CommandSurface("Voice", bPtt0, bPtt1, bCancel, bImport, bSpeak));
        toolInner.Children.Add(WinUiFluentChrome.CommandSurface("Plan", bSave, bClr));
        toolInner.Children.Add(WinUiFluentChrome.CommandSurface("Execution", bRun, bApr, bNext));
        toolInner.Children.Add(WinUiFluentChrome.CommandSurface("Safety", bPanic));
        var opts = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 16 };
        opts.Children.Add(_shots);
        opts.Children.Add(_know);
        toolInner.Children.Add(opts);
        var toolCard = WinUiFluentChrome.WrapCard(toolInner);

        var top = new StackPanel { Spacing = 12 };
        top.Children.Add(WinUiFluentChrome.PageTitle("Ask"));
        top.Children.Add(WinUiFluentChrome.StatusTile("Runtime reality", "guarded", "LLM/RAG real when configured; execution stays safety-gated"));
        var sub = new TextBlock
        {
            Text =
                "Prompt, retrieval (tier shown at top: semantic / FTS / keyword / sequential), plans, push-to-talk. RAG env: docs/Ki-und-RAG-Umgebung.md.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = WinUiFluentChrome.SecondaryTextBrush
        };
        WinUiFluentChrome.ApplyCaptionTextStyle(sub);
        top.Children.Add(sub);
        top.Children.Add(BuildCommandCenter());
        _nextBestActionBar = BuildNextBestActionBar();
        top.Children.Add(_nextBestActionBar);
        top.Children.Add(toolCard);
        _executionStatusLine.Foreground = WinUiFluentChrome.SecondaryTextBrush;
        WinUiFluentChrome.ApplyCaptionTextStyle(_executionStatusLine);
        top.Children.Add(WinUiFluentChrome.SectionCard("Execution state", "Dry-run/run guard and step state", new StackPanel
        {
            Spacing = 8,
            Children = { _executionStatusLine, _executionDetail }
        }));
        top.Children.Add(_planRisk);
        var mid = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = top };

        var left = new StackPanel { Spacing = 10 };
        left.Children.Add(WinUiFluentChrome.ColumnCaption("Prompt"));
        left.Children.Add(_prompt);
        left.Children.Add(WinUiFluentChrome.ColumnCaption("Assistant"));
        left.Children.Add(_assistant);
        left.Children.Add(WinUiFluentChrome.ColumnCaption("Retrieval + context"));
        left.Children.Add(_retrieval);

        var right = new StackPanel { Spacing = 10 };
        right.Children.Add(WinUiFluentChrome.ColumnCaption("Action plan risk table"));
        ConfigurePlanTable();
        right.Children.Add(_planTable);
        _planPreview.Visibility = Visibility.Collapsed;
        right.Children.Add(_planPreview);
        right.Children.Add(WinUiFluentChrome.ColumnCaption("Plan execution log"));
        right.Children.Add(_planExec);

        var body = new Grid { ColumnSpacing = 16 };

        _root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        _root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        _root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetRow(mid, 0);
        Grid.SetColumnSpan(mid, 2);
        Grid.SetRow(_busy, 1);
        Grid.SetColumnSpan(_busy, 2);
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        body.Children.Add(left);
        Grid.SetColumn(left, 0);
        body.Children.Add(right);
        Grid.SetColumn(right, 1);
        Grid.SetRow(body, 2);
        Grid.SetColumnSpan(body, 2);
        _root.Children.Add(mid);
        _root.Children.Add(_busy);
        _root.Children.Add(body);

        Content = _root;
        Loaded += (_, _) =>
        {
            WinUiShellState.OnPttPressed = OnGlobalPttPressed;
            WinUiShellState.PttAwaitsHotkeyRelease = () => _isRecording && _awaitGlobalHotkeyRelease;
            WinUiShellState.OnPttReleasedAsync = StopMicTranscribeAndAskAsync;
            ActivityStatusHub.RefreshFromStores();
            RefreshCommandCenter();
            RefreshExecutionStatus();
            RefreshNextBestAction();
        };
    }

    private Border BuildNextBestActionBar()
    {
        _nbaPrimary.Click += (_, _) => ApplyNextBestActionPrimary();
        _nbaSecondary.Click += (_, _) => ApplyNextBestActionSecondary();
        _nbaDismiss.Click += (_, _) =>
        {
            if (_nextBestActionBar != null)
                _nextBestActionBar.Visibility = Visibility.Collapsed;
        };
        _nextBestAction = NextBestActionService.Build(WinUiShellState.Settings, WinUiShellState.LiveContextLine);
        return WinUiFluentChrome.NextBestActionBar(_nextBestAction, _nbaPrimary, _nbaSecondary, _nbaDismiss);
    }

    private void RefreshNextBestAction()
    {
        _nextBestAction = NextBestActionService.Build(WinUiShellState.Settings, WinUiShellState.LiveContextLine);
        NexusShell.Log("Ask next action refreshed: " + _nextBestAction.Message);
    }

    private void ApplyNextBestActionPrimary()
    {
        if (_nextBestAction == null)
            return;

        switch (_nextBestAction.Intent)
        {
            case "setup.provider":
                NexusShell.Log("Next action: open Setup and configure the provider key.");
                break;
            case "live.ax_context":
                _prompt.Text = "Inspect the active AX context and propose a guarded, read-first plan. Do not write or post anything.";
                NexusShell.Log("Next action: AX context prompt prepared.");
                break;
            default:
                _prompt.Text = _nextBestAction.Message;
                NexusShell.Log("Next action: prompt prepared.");
                break;
        }
    }

    private void ApplyNextBestActionSecondary()
    {
        if (_nextBestAction == null)
            return;

        _prompt.Text = _nextBestAction.Intent switch
        {
            "ask.mail_summary" => "Summarize the active mail and draft a safe reply. Do not send.",
            "ask.knowledge" => "Use local knowledge to answer my current task. Cite the relevant local context and do not execute actions.",
            "live.ax_context" => "Build a guarded AX plan from the current foreground context. Read first, require approval for any write step.",
            _ => "Use the current desktop context to propose the safest next action. Do not execute anything."
        };
        NexusShell.Log("Next action: secondary prompt prepared.");
    }

    private UIElement BuildCommandCenter()
    {
        _commandStatus.Foreground = WinUiFluentChrome.SecondaryTextBrush;
        _nextAction.Foreground = WinUiFluentChrome.PrimaryTextBrush;
        _nextAction.FontWeight = Microsoft.UI.Text.FontWeights.SemiBold;
        _safetyGate.Foreground = WinUiFluentChrome.SecondaryTextBrush;

        var grid = new Grid { ColumnSpacing = 12 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var left = new StackPanel
        {
            Spacing = 6,
            Children =
            {
                WinUiFluentChrome.ColumnCaption("AI Command Center"),
                _commandStatus
            }
        };
        var right = new StackPanel
        {
            Spacing = 6,
            Children =
            {
                WinUiFluentChrome.ColumnCaption("Next Action + Safety Gate"),
                _nextAction,
                _safetyGate
            }
        };

        grid.Children.Add(left);
        grid.Children.Add(right);
        Grid.SetColumn(right, 1);
        return WinUiFluentChrome.WrapCard(grid, new Thickness(16, 14, 16, 14));
    }

    private void RefreshCommandCenter()
    {
        var insight = OperatorInsightService.BuildSnapshot(WinUiShellState.Settings);
        var screenshots = _shots.IsChecked == true ? "screenshots on" : "screenshots off";
        var knowledge = _know.IsChecked == true ? "knowledge on" : "knowledge off";
        _commandStatus.Text =
            $"{WinUiShellState.Settings.Provider} / {WinUiShellState.Settings.Mode} · {insight.ProcessName} · {insight.AdapterFamily}\n" +
            $"{screenshots} · {knowledge} · readiness {insight.ReadinessScore}/{insight.ReadinessMax}";
        _nextAction.Text = insight.SafeNextAction;
        _safetyGate.Text =
            $"Risky: {insight.RiskyAction}\n" +
            $"Gate: plan execution stays behind PlanGuard, approval, and Panic Stop.";
    }

    private void SetExecutionState(string state, string mode, int doneSteps, int totalSteps, string? error = null)
    {
        _executionState = state;
        _executionMode = mode;
        _executionDoneSteps = Math.Max(0, doneSteps);
        _executionTotalSteps = Math.Max(0, totalSteps);
        _executionError = error ?? "";
        RefreshExecutionStatus();
    }

    private void SetExecutionLastStep(string? lastStep)
    {
        _executionLastStep = (lastStep ?? "").Trim();
        RefreshExecutionStatus();
    }

    private void RefreshExecutionStatus()
    {
        _executionStatusLine.Text =
            $"State: {_executionState} · Mode: {_executionMode} · Steps: {_executionDoneSteps}/{_executionTotalSteps}";
        _executionDetail.Text =
            $"flow state: {_executionState}\n" +
            $"mode: {_executionMode}\n" +
            $"plan steps: {_executionDoneSteps}/{_executionTotalSteps}\n" +
            $"cursor: {_planStepIndex}\n" +
            (_executionError.Length == 0 ? "error: none" : $"error: {_executionError}") +
            "\n" +
            $"last step: {(_executionLastStep.Length == 0 ? "none" : _executionLastStep)}";
    }

    private void SetBusyBar(bool busy)
    {
        _busy.IsOpen = busy;
        ActivityStatusHub.SetAskBusy(busy);
    }

    private void OnGlobalPttPressed()
    {
        if (!OperatingSystem.IsWindows())
            return;
        TextToSpeechService.RequestBargeIn();
        StartMic("(hotkey)", true);
    }

    public bool AwaitsGlobalHotkeyRelease => _isRecording && _awaitGlobalHotkeyRelease;

    public async Task NotifyGlobalPushToTalkReleasedAsync() => await StopMicTranscribeAndAskAsync();

    private void StartMic(string source, bool awaitHotkeyRelease)
    {
        if (_isRecording)
            return;
        try
        {
            _mic = new WindowsMicRecorder();
            _mic.Start();
            _isRecording = true;
            _awaitGlobalHotkeyRelease = awaitHotkeyRelease;
            _retrieval.Text = $"Recording ({source}) …";
            CompanionHub.Publish(CompanionVisualState.Listening);
        }
        catch (Exception ex)
        {
            _retrieval.Text = "Mic: " + ex.Message;
        }
    }

    private void CancelMic()
    {
        if (!_isRecording)
            return;
        _isRecording = false;
        _awaitGlobalHotkeyRelease = false;
        _mic?.StopSync();
        _mic = null;
        CompanionHub.Publish(CompanionVisualState.Ready);
    }

    private async Task StopMicTranscribeAndAskAsync()
    {
        if (!_isRecording || _mic == null)
            return;
        _isRecording = false;
        _awaitGlobalHotkeyRelease = false;
        string? path = null;
        try
        {
            path = await _mic.StopToFileAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _retrieval.Text = "Mic stop: " + ex.Message;
            CompanionHub.Publish(CompanionVisualState.Ready);
            return;
        }
        finally
        {
            _mic = null;
        }

        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            CompanionHub.Publish(CompanionVisualState.Ready);
            return;
        }

        SetBusyBar(true);
        CompanionHub.Publish(CompanionVisualState.Transcribing);
        try
        {
            _cts = new CancellationTokenSource();
            var transcript = await Task.Run(async () =>
                    await SpeechTranscriptionService.TranscribeFileAsync(path, _cts.Token).ConfigureAwait(false))
                .ConfigureAwait(true);
            _retrieval.Text = transcript ?? "";
            if (!string.IsNullOrWhiteSpace(transcript))
            {
                if (VoiceIntentService.TryResolve(transcript, out var vir))
                {
                    if (vir.SkipLlm)
                    {
                        CompanionHub.Publish(CompanionVisualState.Ready);
                        return;
                    }

                    _prompt.Text = string.IsNullOrEmpty(vir.PrefillPrompt) ? transcript : vir.PrefillPrompt;
                }
                else
                    _prompt.Text = transcript;
            }
        }
        catch (Exception ex)
        {
            _retrieval.Text = "STT: " + ex.Message;
            CompanionHub.Publish(CompanionVisualState.Ready);
            return;
        }
        finally
        {
            SetBusyBar(false);
            try
            {
                File.Delete(path);
            }
            catch
            {
                // ignore
            }
        }

        if (!string.IsNullOrWhiteSpace(_prompt.Text))
            await RunAskAsync();
        else
            CompanionHub.Publish(CompanionVisualState.Ready);
    }

    private async Task ImportAudioAsync()
    {
        var w = WinUiShellState.MainWindowRef;
        if (w == null)
            return;
        var p = new FileOpenPicker();
        p.FileTypeFilter.Add(".wav");
        p.FileTypeFilter.Add(".mp3");
        p.FileTypeFilter.Add(".m4a");
        InitializeWithWindow.Initialize(p, WindowNative.GetWindowHandle(w));
        var f = await p.PickSingleFileAsync();
        if (f == null)
            return;
        SetBusyBar(true);
        try
        {
            _cts = new CancellationTokenSource();
            var path = f.Path;
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                _assistant.Text = "Could not read file path for transcription.";
                return;
            }

            var transcript = await Task.Run(async () =>
                    await SpeechTranscriptionService.TranscribeFileAsync(path, _cts.Token).ConfigureAwait(false))
                .ConfigureAwait(true);
            _prompt.Text = transcript ?? "";
            _retrieval.Text = "(import)";
        }
        catch (Exception ex)
        {
            _assistant.Text = "Import: " + ex.Message;
        }
        finally
        {
            SetBusyBar(false);
        }
    }

    private async Task RunSmokeAsync()
    {
        SetBusyBar(true);
        CompanionHub.Publish(CompanionVisualState.Thinking);
        try
        {
            _cts = new CancellationTokenSource();
            _assistant.Text = await LlmChatService.SmokeAsync(WinUiShellState.Settings, _cts.Token).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _assistant.Text = ex.Message;
            CompanionHub.Publish(CompanionVisualState.Error);
        }
        finally
        {
            SetBusyBar(false);
            CompanionHub.Publish(CompanionVisualState.Ready);
        }
    }

    private async Task RunAskAsync()
    {
        var prompt = _prompt.Text?.Trim();
        if (string.IsNullOrEmpty(prompt))
        {
            _assistant.Text = "Enter a prompt.";
            return;
        }

        SetBusyBar(true);
        CompanionHub.Publish(CompanionVisualState.Thinking);
        try
        {
            _cts = new CancellationTokenSource();
            var s = WinUiShellState.Settings;
            var ct = _cts.Token;

            if (AskPromptRouter.TryPersonaGreeting(prompt, out var greet))
            {
                _assistant.Text = greet;
                _planPreview.Text = "(Persona)";
                _planSteps.Clear();
                return;
            }

            if (AskPromptRouter.TryParseCliRoute(prompt, out var route) && route != null)
            {
                if (!WinUiShellState.Settings.EnableCliHandoffRoutes)
                {
                    _assistant.Text = "CLI handoff is disabled in settings. Enable it in Setup.";
                    _retrieval.Text = "CLI route blocked by settings.";
                    return;
                }
                await RunCliAsync(s, route, ct).ConfigureAwait(true);
                return;
            }

            if (AskPromptRouter.TryParseRadicalAutoRoute(prompt, out var radicalAuto) && radicalAuto != null)
            {
                if (!WinUiShellState.Settings.EnableRadicalAutoRoutes)
                {
                    _assistant.Text = "radical-auto is disabled in settings. Enable it in Setup.";
                    _retrieval.Text = "radical-auto route blocked by settings.";
                    return;
                }
                await RunRadicalAutoFromGoalAsync(s, radicalAuto.Goal, ct).ConfigureAwait(true);
                return;
            }

            if (AskPromptRouter.TryParseRadicalRoute(prompt, out var radical) && radical != null)
            {
                if (!WinUiShellState.Settings.EnableRadicalIdeaBlueprint)
                {
                    _assistant.Text = "radical ideas are disabled in settings. Enable them in Setup.";
                    _retrieval.Text = "radical ideas route blocked by settings.";
                    return;
                }
                prompt = BuildRadicalIdeasPrompt(radical.Goal);
            }

            var shots = _shots.IsChecked == true;
            var know = _know.IsChecked == true;
            string? knowOverride = null;
            if (know)
            {
                var aug = KnowledgeSnippetService.BuildAugmentationResult(prompt, 6000);
                knowOverride = aug.Bundle.ContextText;
                _retrieval.Text = KnowledgeSnippetService.FormatAugmentationForAskPanel(aug);
            }

            var fusion = UiAutomationVisionFusion.BuildAskAugmentation(s, shots);
            var adapt = OperatorAdapterRegistry.TryEnrichForegroundContext();
            if (!string.IsNullOrWhiteSpace(adapt))
                fusion = string.IsNullOrWhiteSpace(fusion) ? adapt : fusion + "\n\n" + adapt;
            var effective = string.IsNullOrWhiteSpace(fusion) ? prompt : fusion + "\n\n---\n" + prompt;

            var text = await LlmChatService
                .CompleteAsync(s, effective, shots, know, ct, know ? knowOverride : null)
                .ConfigureAwait(true);
            _assistant.Text = text;

            var tokens = ActionPlanExtractor.Extract(text);
            var fromRegex = ActionPlanExtractor.ToRecipeSteps(tokens);
            _planSteps.Clear();
            _planSteps.AddRange(fromRegex);
            if (_planSteps.Count == 0 && PlanJsonParser.TryParseRecipeStepsFromText(text, out var jsonSteps) && jsonSteps.Count > 0)
                _planSteps.AddRange(jsonSteps);
            _planStepIndex = 0;
            _planPreview.Text = fromRegex.Count > 0
                ? ActionPlanExtractor.FormatPreview(tokens)
                : FormatStepsForPreview(_planSteps);
            RefreshPlanTable();
            _planExec.Text = $"({_planSteps.Count} steps — run plan / run next)";
            SetExecutionState("ready", "parsed", _planSteps.Count, _planSteps.Count);
            SetExecutionLastStep(_planSteps.Count == 0 ? "No executable plan detected." : $"Parsed {_planSteps.Count} step(s).");
            RefreshPlanRiskPreview();

            if (s.SpeakResponses)
            {
                CompanionHub.Publish(CompanionVisualState.Speaking);
                var err = await TextToSpeechService.SpeakAsync(text, ct).ConfigureAwait(true);
                if (!string.IsNullOrEmpty(err))
                    NexusShell.Log("TTS: " + err);
            }
        }
        catch (Exception ex)
        {
            _assistant.Text = "Error: " + ex;
            CompanionHub.Publish(CompanionVisualState.Error);
        }
        finally
        {
            SetBusyBar(false);
            CompanionHub.Publish(CompanionVisualState.Ready);
        }
    }

    private async Task RunCliAsync(NexusSettings s, CliAskRoute route, CancellationToken ct)
    {
        var payload = route.Payload?.Trim() ?? "";
        if (string.IsNullOrEmpty(payload))
        {
            _assistant.Text = "Add task text after the CLI trigger.";
            return;
        }

        var combined = payload;
        if (route.WithScreenSummary && OperatingSystem.IsWindows())
        {
            var visionPrompt =
                "In 6–10 short bullets in English, describe what is visible on all monitors (main windows, visible apps, readable titles). No greeting.";
            var summary = await LlmChatService.CompleteAsync(s, visionPrompt, true, false, ct).ConfigureAwait(true);
            combined = "[Screen summary]\n" + summary.Trim() + "\n\n---\n[Task]\n" + payload;
        }

        var (logPath, excerpt) = await CliAgentRunner.RunAsync(route.Agent, combined, ct).ConfigureAwait(true);
        _assistant.Text = excerpt + "\n\n— Log —\n" + logPath;
        _planSteps.Clear();
        _planPreview.Text = "(CLI)";
    }

    private async Task RunRadicalAutoFromGoalAsync(NexusSettings s, string goal, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(goal))
        {
            _assistant.Text = "Please provide a radical mission goal.";
            return;
        }

        SetBusyBar(true);
        _planSteps.Clear();
        _lastRadicalPlan = null;
        _lastRadicalRun = null;
        CompanionHub.Publish(CompanionVisualState.Thinking);
        try
        {
            _cts = new CancellationTokenSource();
            ct = _cts.Token;
            var plan = await GoalOrchestrator.GeneratePlanAsync(s, goal, ct).ConfigureAwait(true);
            _lastRadicalPlan = plan;
            _planPreview.Text = plan.Markdown;
            _assistant.Text = plan.Markdown;
            _planExec.Text = $"radical plan persisted: {plan.PlanFilePath}";
            _planSteps.Clear();
            _planSteps.AddRange(plan.Steps);
            _planStepIndex = 0;

            var dryRun = string.IsNullOrWhiteSpace(plan.RiskLevel)
                          || !string.Equals(s.Safety.Profile, "power-user", StringComparison.OrdinalIgnoreCase)
                          || plan.RiskLevel.Equals("high", StringComparison.OrdinalIgnoreCase);
            if (dryRun || plan.RequiresApproval)
            {
                _planExec.Text = plan.RequiresApproval
                    ? "High-risk plan created: approval gate required before execution."
                    : "Plan generated in dry-run mode. Use “Run plan” to execute manually.";
                RefreshPlanTable();
                RefreshPlanRiskPreview();
                return;
            }

            var run = await AutoExecutor.RunAsync(plan, s, dryRun: false, ct).ConfigureAwait(true);
            _lastRadicalRun = run;
            _planExec.Text = BuildRadicalDigestText(plan, run);
            _assistant.Text = run.Summary;
            SetExecutionState(run.Completed ? "completed" : "partial", "real-run", run.ExecutedSteps, run.ExecutedSteps + run.FailedSteps);
            SetExecutionLastStep(run.Completed ? "Run completed." : "Run partial.");
            RefreshPlanTable();
            RefreshPlanRiskPreview();
        }
        catch (OperationCanceledException)
        {
            _assistant.Text = "radical run cancelled.";
            SetExecutionState("cancelled", "radical", _planStepIndex, _planSteps.Count);
            SetExecutionLastStep("cancelled");
        }
        catch (Exception ex)
        {
            _assistant.Text = "radical error: " + ex.Message;
            _planExec.Text = "radical error: " + ex.Message;
            NexusShell.Log("radical run error: " + ex.Message);
            CompanionHub.Publish(CompanionVisualState.Error);
            SetExecutionState("failed", "radical", _planStepIndex, _planSteps.Count, ex.Message);
            SetExecutionLastStep("failed");
        }
        finally
        {
            SetBusyBar(false);
            CompanionHub.Publish(CompanionVisualState.Ready);
        }
    }

    private static string BuildRadicalDigestText(RadicalPlan plan, RadicalExecutionReport run)
    {
        var scope = string.Join(", ",
            run.StepSummaries.Take(3)
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrWhiteSpace(s)));
        return
            $"Captain Digest\nGoal: {plan.GoalText}\nRisk: {plan.RiskLevel}\nMode: {(run.DryRun ? "dry-run" : "real")}\nStatus: {(run.Completed ? "completed" : "partial")}\nSummary: {run.Summary}\nTop: {(string.IsNullOrWhiteSpace(scope) ? "(no steps)" : scope)}";
    }

    private static string BuildRadicalIdeasPrompt(string goal)
    {
        return
            "Du bist ein Radical AI Product Innovator und Vibe Coding Builder.\n" +
            "Erzeuge 5 radikale, disruptive KI-/AI-Features für diese App.\n\n" +
            "1) Gib zuerst ein kurzes Breakdown der aktuellen App.\n" +
            "2) Gib danach genau 5 Ideen, jede: komplette Interaktion ersetzt, mindestens 10x Verbesserung, AI-zentrale Entscheidung, bestehende UI vereinfacht oder eliminiert.\n" +
            "3) Wähle die beste Idee und beschreibe sie als sofort baubares MVP (Komponentenstruktur, minimal notwendige Screens, AI-Core, UX-Flow, Build-Schritte).\n" +
            "4) Erkläre kurz, warum das 10x besser ist, was ersetzt wird und warum schwer zu kopieren.\n\n" +
            "Starte ohne konservative Feature-Add-ons, bitte komplett neu denken unter dem Stichwort: User macht wenig, System entscheidet.\n\n" +
            $"Zielbereich der App: {goal}";
    }

    private async Task ExecutePlanAsync(bool dryRun)
    {
        var steps = ActivePlanSteps();
        if (steps.Count == 0)
        {
            _planExec.Text = "No steps — run ask first.";
            SetExecutionState("blocked", dryRun ? "dry-run" : "run", 0, 0, "No steps");
            SetExecutionLastStep("No steps");
            return;
        }

        _cts = new CancellationTokenSource();
        _planStepIndex = 0;
        SetExecutionState("running", dryRun ? "dry-run" : "run", 0, steps.Count);
        try
        {
            var log = await SimplePlanSimulator.RunAsync(steps, dryRun, WinUiShellState.Settings, null, _cts.Token)
                .ConfigureAwait(true);
            _planExec.Text = log;
            SetExecutionLastStep(LastResultFromSimulator(log));
            _planStepIndex = steps.Count;
            SetExecutionState("completed", dryRun ? "dry-run" : "run", steps.Count, steps.Count);
        }
        catch (OperationCanceledException)
        {
            SetExecutionState("cancelled", dryRun ? "dry-run" : "run", _planStepIndex, steps.Count);
            SetExecutionLastStep("");
            _planExec.Text += "\n[execution cancelled]";
            NexusShell.Log("Plan run cancelled.");
        }
        catch (Exception ex)
        {
            SetExecutionState("failed", dryRun ? "dry-run" : "run", _planStepIndex, steps.Count, ex.Message);
            SetExecutionLastStep(ex.Message);
            _planExec.Text += "\n" + ex.Message;
            NexusShell.Log("Plan run failed: " + ex.Message);
        }
        finally
        {
            if (_planExec.Text.Length > 14000)
                _planExec.Text = _planExec.Text[^14000..];
        }
    }

    private async Task ExecutePlanAfterConfirmAsync()
    {
        if (XamlRoot == null)
        {
            await ExecutePlanAsync(false);
            return;
        }

        var dlg = new ContentDialog
        {
            Title = "Run plan?",
            Content = "Execute the current plan steps on this PC (subject to safety profile).",
            PrimaryButtonText = "Run",
            CloseButtonText = "Cancel",
            XamlRoot = XamlRoot
        };
        var r = await dlg.ShowAsync();
        if (r == ContentDialogResult.Primary)
            await ExecutePlanAsync(false);
    }

    private async Task RunNextPlanStepAsync()
    {
        var steps = ActivePlanSteps();
        if (steps.Count == 0 || _planStepIndex >= steps.Count)
        {
            NexusShell.Log("No next step.");
            SetExecutionState("completed", "next-step", _planStepIndex, steps.Count);
            SetExecutionLastStep("No next step");
            return;
        }

        _cts ??= new CancellationTokenSource();
        var slice = new List<RecipeStep> { steps[_planStepIndex] };
        SetExecutionState("running", "next-step", _planStepIndex, steps.Count);
        try
        {
            var line = await SimplePlanSimulator.RunAsync(slice, false, WinUiShellState.Settings, null, _cts.Token)
                .ConfigureAwait(true);
            _planExec.Text += "\n" + line;
            SetExecutionLastStep(LastResultFromSimulator(line));
            _planStepIndex++;
            SetExecutionState(_planStepIndex >= steps.Count ? "completed" : "paused", "next-step", _planStepIndex, steps.Count);
        }
        catch (OperationCanceledException)
        {
            SetExecutionState("cancelled", "next-step", _planStepIndex, steps.Count);
            SetExecutionLastStep("cancelled");
            _planExec.Text += "\n[next step cancelled]";
            NexusShell.Log("Plan next step cancelled.");
        }
        catch (Exception ex)
        {
            SetExecutionState("failed", "next-step", _planStepIndex, steps.Count, ex.Message);
            SetExecutionLastStep(ex.Message);
            _planExec.Text += "\n[next step failed] " + ex.Message;
            NexusShell.Log("Plan next step failed: " + ex.Message);
        }

        if (_planExec.Text.Length > 14000)
            _planExec.Text = _planExec.Text[^14000..];
    }

    private List<RecipeStep> ActivePlanSteps()
    {
        if (_planSteps.Count > 0)
            return _planSteps;
        return SimplePlanSimulator.ParsePlanPreviewLines(_planPreview.Text ?? "");
    }

    public Task PaletteAskNowAsync() => RunAskAsync();

    public Task PaletteRunPlanAsync() => ExecutePlanAsync(false);

    private void RefreshPlanRiskPreview()
    {
        var steps = ActivePlanSteps();
        if (steps.Count == 0)
        {
            _planRisk.Severity = InfoBarSeverity.Informational;
            _planRisk.Message = "No executable plan detected.";
            return;
        }

        var writeLike = steps.Count(s =>
        {
            var a = s.ActionArgument ?? "";
            return a.Contains("[ACTION:", StringComparison.OrdinalIgnoreCase)
                   || a.Contains("click", StringComparison.OrdinalIgnoreCase)
                   || a.Contains("type", StringComparison.OrdinalIgnoreCase)
                   || a.Contains("send", StringComparison.OrdinalIgnoreCase)
                   || a.Contains("post", StringComparison.OrdinalIgnoreCase)
                   || a.Contains("book", StringComparison.OrdinalIgnoreCase);
        });
        var axLike = steps.Count(s => (s.ActionArgument ?? "").Contains("ax.", StringComparison.OrdinalIgnoreCase)
                                     || (s.ActionArgument ?? "").Contains("ax|", StringComparison.OrdinalIgnoreCase));
        var powerUser = string.Equals(WinUiShellState.Settings.Safety.Profile, "power-user", StringComparison.OrdinalIgnoreCase);
        var severity = writeLike > 0 || axLike > 0
            ? (powerUser ? InfoBarSeverity.Warning : InfoBarSeverity.Error)
            : InfoBarSeverity.Success;

        _planRisk.Severity = severity;
        var quality = OperatorInsightService.ScoreFlow(steps, WinUiShellState.Settings);
        _planRisk.Message =
            $"{steps.Count} step(s) · write-like: {writeLike} · AX-like: {axLike} · safety: {WinUiShellState.Settings.Safety.Profile}. " +
            $"{quality.Summary} " +
            (powerUser ? "Execution can run through guards." : "Execution remains guarded/simulation unless safety allows it.");
    }

    private void SavePlanAsRitual()
    {
        var steps = ActivePlanSteps();
        if (steps.Count == 0)
            return;
        var title = _prompt.Text?.Trim();
        if (string.IsNullOrWhiteSpace(title))
            title = "Flow " + DateTime.Now.ToString("yyyy-MM-dd HH:mm");
        if (title.Length > 120)
            title = title[..120];
        var recipe = new AutomationRecipe
        {
            Id = Guid.NewGuid().ToString("n"),
            Name = title,
            Description = "From WinUI Ask / plan",
            Steps = steps.ToList()
        };
        RitualRecipeStore.AppendRecipe(recipe);
        NexusShell.Log("Saved flow: " + recipe.Name);
    }

    private async Task SpeakAsync()
    {
        var t = _assistant.Text?.Trim();
        if (string.IsNullOrEmpty(t))
            return;
        SetBusyBar(true);
        CompanionHub.Publish(CompanionVisualState.Speaking);
        try
        {
            _cts = new CancellationTokenSource();
            var err = await TextToSpeechService.SpeakAsync(t, _cts.Token).ConfigureAwait(true);
            if (!string.IsNullOrEmpty(err))
                NexusShell.Log("TTS: " + err);
        }
        finally
        {
            SetBusyBar(false);
            CompanionHub.Publish(CompanionVisualState.Ready);
        }
    }

    private static string FormatStepsForPreview(IReadOnlyList<RecipeStep> steps)
    {
        var sb = new StringBuilder();
        for (var i = 0; i < steps.Count; i++)
        {
            var s = steps[i];
            sb.Append(i + 1).Append(". ").Append(s.ActionType).Append(": ").Append(s.ActionArgument);
            if (s.WaitMs > 0)
                sb.Append(" (wait ").Append(s.WaitMs).Append("ms)");
            sb.AppendLine();
        }

        return sb.ToString().TrimEnd();
    }

    private void ConfigurePlanTable()
    {
        if (_planTable.ItemTemplate != null)
            return;

        _planTable.Header = new Grid
        {
            ColumnSpacing = 10,
            Padding = new Thickness(8, 6, 8, 6),
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(44) },
                new ColumnDefinition { Width = new GridLength(110) },
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                new ColumnDefinition { Width = new GridLength(92) },
                new ColumnDefinition { Width = new GridLength(98) },
                new ColumnDefinition { Width = new GridLength(70) }
            },
            Children =
            {
                HeaderText("#", 0),
                HeaderText("Action", 1),
                HeaderText("Target", 2),
                HeaderText("Risk", 3),
                HeaderText("Status", 4),
                HeaderText("Wait", 5)
            }
        };

        _planTable.ItemTemplate = BuildPlanRowTemplate();
        _planTable.ItemsSource = Array.Empty<PlanPreviewRow>();
    }

    private static TextBlock HeaderText(string text, int col)
    {
        var tb = new TextBlock
        {
            Text = text,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = WinUiFluentChrome.PrimaryTextBrush
        };
        Grid.SetColumn(tb, col);
        return tb;
    }

    private static DataTemplate BuildPlanRowTemplate()
    {
        const string xaml =
            """
            <DataTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
              <Grid ColumnSpacing="10" Padding="8,6,8,6">
                <Grid.ColumnDefinitions>
                  <ColumnDefinition Width="44"/>
                  <ColumnDefinition Width="110"/>
                  <ColumnDefinition Width="*"/>
                  <ColumnDefinition Width="92"/>
                  <ColumnDefinition Width="98"/>
                  <ColumnDefinition Width="70"/>
                </Grid.ColumnDefinitions>
                <TextBlock Grid.Column="0" Text="{Binding Step}" TextWrapping="Wrap"/>
                <TextBlock Grid.Column="1" Text="{Binding ActionType}" TextWrapping="Wrap"/>
                <TextBlock Grid.Column="2" Text="{Binding Target}" TextWrapping="Wrap"/>
                <TextBlock Grid.Column="3" Text="{Binding Risk}" TextWrapping="Wrap"/>
                <TextBlock Grid.Column="4" Text="{Binding Status}" TextWrapping="Wrap"/>
                <TextBlock Grid.Column="5" Text="{Binding WaitMs}" TextWrapping="Wrap"/>
              </Grid>
            </DataTemplate>
            """;
        return (DataTemplate)Microsoft.UI.Xaml.Markup.XamlReader.Load(xaml);
    }

    private void RefreshPlanTable()
    {
        var steps = ActivePlanSteps();
        _planTable.ItemsSource = steps.Select((s, i) => new PlanPreviewRow
        {
            Step = i + 1,
            ActionType = string.IsNullOrWhiteSpace(s.ActionType) ? "token" : s.ActionType,
            Target = Shorten(s.ActionArgument ?? "", 96),
            Risk = ClassifyRisk(s),
            Status = ClassifyStatus(s),
            WaitMs = s.WaitMs
        }).ToList();
    }

    private static string ClassifyRisk(RecipeStep step)
    {
        var a = step.ActionArgument ?? "";
        if (a.Contains("ax.", StringComparison.OrdinalIgnoreCase) || a.Contains("ax|", StringComparison.OrdinalIgnoreCase))
            return "AX";
        if (a.Contains("send", StringComparison.OrdinalIgnoreCase) ||
            a.Contains("post", StringComparison.OrdinalIgnoreCase) ||
            a.Contains("book", StringComparison.OrdinalIgnoreCase))
            return "High";
        if (a.Contains("[ACTION:", StringComparison.OrdinalIgnoreCase) ||
            a.Contains("click", StringComparison.OrdinalIgnoreCase) ||
            a.Contains("type", StringComparison.OrdinalIgnoreCase))
            return "Guarded";
        return "Low";
    }

    private static string ClassifyStatus(RecipeStep step)
    {
        var risk = ClassifyRisk(step);
        return risk switch
        {
            "Low" => "safe",
            "Guarded" => "guarded",
            "AX" => "review",
            _ => "confirm"
        };
    }

    private static string Shorten(string value, int max)
    {
        value = value.Replace("\r", " ").Replace("\n", " ").Trim();
        return value.Length <= max ? value : value[..max] + "...";
    }

    private static string LastResultFromSimulator(string? logText)
    {
        if (string.IsNullOrWhiteSpace(logText))
            return "";

        var lines = logText.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        for (var i = lines.Length - 1; i >= 0; i--)
        {
            var line = lines[i].Trim();
            if (line.Length == 0)
                continue;
            if (line.StartsWith("→", StringComparison.Ordinal) || line.Contains("→", StringComparison.Ordinal))
                return line;
        }

        return lines.Length > 0 ? lines[^1].Trim() : "";
    }
}
