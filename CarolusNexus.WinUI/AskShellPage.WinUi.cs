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
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace CarolusNexus_WinUI.Pages;

/// <summary>Full Ask workflow (plans, PTT, speak) — parity target for Avalonia Ask tab.</summary>
public sealed class AskShellPage : Page
{
    private readonly TextBox _prompt = new() { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 100 };
    private readonly TextBox _assistant = new() { IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 100 };
    private readonly TextBox _retrieval = new() { IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 56 };
    private readonly TextBox _planPreview = new() { IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 80, FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas") };
    private readonly TextBox _planExec = new() { IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 100, FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas") };
    private readonly CheckBox _shots = new() { Content = "Include screenshots", IsChecked = true };
    private readonly CheckBox _know = new() { Content = "Use local knowledge", IsChecked = true };
    private readonly InfoBar _busy = new() { IsOpen = false, Title = "Working" };
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
    private WindowsMicRecorder? _mic;
    private bool _isRecording;
    private bool _awaitGlobalHotkeyRelease;
    public AskShellPage()
    {
        var bAsk = new Button { Content = "Ask now" };
        WinUiFluentChrome.StyleActionButton(bAsk, accent: true);
        bAsk.Click += async (_, _) => await RunAskAsync();
        var bSmoke = new Button { Content = "Smoke test" };
        WinUiFluentChrome.StyleActionButton(bSmoke);
        bSmoke.Click += async (_, _) => await RunSmokeAsync();
        var bImport = new Button { Content = "Import audio + transcribe" };
        WinUiFluentChrome.StyleActionButton(bImport);
        bImport.Click += async (_, _) => await ImportAudioAsync();
        var bPtt0 = new Button { Content = "Start push-to-talk" };
        WinUiFluentChrome.StyleActionButton(bPtt0);
        bPtt0.Click += (_, _) => StartMic("(button)", false);
        var bPtt1 = new Button { Content = "Stop + ask" };
        WinUiFluentChrome.StyleActionButton(bPtt1);
        bPtt1.Click += async (_, _) => await StopMicTranscribeAndAskAsync();
        var bCancel = new Button { Content = "Cancel recording" };
        WinUiFluentChrome.StyleActionButton(bCancel);
        bCancel.Click += (_, _) => CancelMic();
        var bRun = new Button { Content = "Run plan" };
        WinUiFluentChrome.StyleActionButton(bRun, accent: true);
        bRun.Click += async (_, _) => await ExecutePlanAsync(false);
        var bApr = new Button { Content = "Approve + run" };
        WinUiFluentChrome.StyleActionButton(bApr);
        bApr.Click += async (_, _) => await ExecutePlanAfterConfirmAsync();
        var bNext = new Button { Content = "Run next step" };
        WinUiFluentChrome.StyleActionButton(bNext);
        bNext.Click += async (_, _) => await RunNextPlanStepAsync();
        var bSave = new Button { Content = "Save plan as flow" };
        WinUiFluentChrome.StyleActionButton(bSave);
        bSave.Click += (_, _) => SavePlanAsRitual();
        var bClr = new Button { Content = "Clear plan" };
        WinUiFluentChrome.StyleActionButton(bClr);
        bClr.Click += (_, _) =>
        {
            _planPreview.Text = "";
            _planExec.Text = "";
            _planSteps.Clear();
            _planStepIndex = 0;
            _planRisk.Severity = InfoBarSeverity.Informational;
            _planRisk.Message = "No plan yet.";
        };
        var bPanic = new Button { Content = "Panic stop" };
        WinUiFluentChrome.StyleActionButton(bPanic);
        bPanic.Click += (_, _) =>
        {
            _cts?.Cancel();
            NexusShell.Log("panic stop");
        };
        var bSpeak = new Button { Content = "Speak response" };
        WinUiFluentChrome.StyleActionButton(bSpeak);
        bSpeak.Click += async (_, _) => await SpeakAsync();

        static StackPanel HRow(params UIElement[] els)
        {
            var sp = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            foreach (var e in els)
                sp.Children.Add(e);
            return sp;
        }

        var toolInner = new StackPanel { Spacing = 10 };
        toolInner.Children.Add(WinUiFluentChrome.ColumnCaption("Ask, voice, and plans"));
        toolInner.Children.Add(HRow(bAsk, bSmoke, bImport));
        toolInner.Children.Add(HRow(bPtt0, bPtt1, bCancel));
        toolInner.Children.Add(HRow(bRun, bApr, bNext, bSave, bClr, bPanic, bSpeak));
        var opts = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 16 };
        opts.Children.Add(_shots);
        opts.Children.Add(_know);
        toolInner.Children.Add(opts);
        var toolCard = WinUiFluentChrome.WrapCard(toolInner);

        var top = new StackPanel { Spacing = 12 };
        top.Children.Add(WinUiFluentChrome.PageTitle("Ask"));
        var sub = new TextBlock
        {
            Text =
                "Prompt, retrieval (tier shown at top: semantic / FTS / keyword / sequential), plans, push-to-talk. RAG env: docs/Ki-und-RAG-Umgebung.md.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = WinUiFluentChrome.SecondaryTextBrush
        };
        WinUiFluentChrome.ApplyCaptionTextStyle(sub);
        top.Children.Add(sub);
        top.Children.Add(toolCard);
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
        right.Children.Add(WinUiFluentChrome.ColumnCaption("Action plan preview"));
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
        };
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
                await RunCliAsync(s, route, ct).ConfigureAwait(true);
                return;
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
            _planExec.Text = $"({_planSteps.Count} steps — run plan / run next)";
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

    private async Task ExecutePlanAsync(bool dryRun)
    {
        var steps = ActivePlanSteps();
        if (steps.Count == 0)
        {
            _planExec.Text = "No steps — run ask first.";
            return;
        }

        _cts = new CancellationTokenSource();
        _planExec.Text = await SimplePlanSimulator.RunAsync(steps, dryRun, WinUiShellState.Settings, null, _cts.Token)
            .ConfigureAwait(true);
        _planStepIndex = 0;
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
            return;
        }

        _cts ??= new CancellationTokenSource();
        var slice = new List<RecipeStep> { steps[_planStepIndex] };
        var line = await SimplePlanSimulator.RunAsync(slice, false, WinUiShellState.Settings, null, _cts.Token)
            .ConfigureAwait(true);
        _planExec.Text += "\n" + line;
        _planStepIndex++;
    }

    private List<RecipeStep> ActivePlanSteps()
    {
        if (_planSteps.Count > 0)
            return _planSteps;
        return SimplePlanSimulator.ParsePlanPreviewLines(_planPreview.Text ?? "");
    }

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
        _planRisk.Message =
            $"{steps.Count} step(s) · write-like: {writeLike} · AX-like: {axLike} · safety: {WinUiShellState.Settings.Safety.Profile}. " +
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
}
