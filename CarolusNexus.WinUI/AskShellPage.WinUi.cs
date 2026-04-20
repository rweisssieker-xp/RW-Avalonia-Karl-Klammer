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
    private readonly Grid _root = new() { Padding = new Thickness(12) };
    private CancellationTokenSource? _cts;
    private readonly List<RecipeStep> _planSteps = new();
    private int _planStepIndex;
    private WindowsMicRecorder? _mic;
    private bool _isRecording;
    private bool _operationBusy;
    private bool _awaitGlobalHotkeyRelease;
    public AskShellPage()
    {
        var tools = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };

        var bAsk = new Button { Content = "ask now" };
        bAsk.Click += async (_, _) => await RunAskAsync();
        var bSmoke = new Button { Content = "smoke test" };
        bSmoke.Click += async (_, _) => await RunSmokeAsync();
        var bImport = new Button { Content = "import audio + transcribe" };
        bImport.Click += async (_, _) => await ImportAudioAsync();
        var bPtt0 = new Button { Content = "start push-to-talk" };
        bPtt0.Click += (_, _) => StartMic("(button)", false);
        var bPtt1 = new Button { Content = "stop + ask" };
        bPtt1.Click += async (_, _) => await StopMicTranscribeAndAskAsync();
        var bCancel = new Button { Content = "cancel recording" };
        bCancel.Click += (_, _) => CancelMic();
        var bRun = new Button { Content = "run plan" };
        bRun.Click += async (_, _) => await ExecutePlanAsync(false);
        var bApr = new Button { Content = "approve + run" };
        bApr.Click += async (_, _) => await ExecutePlanAfterConfirmAsync();
        var bNext = new Button { Content = "run next step" };
        bNext.Click += async (_, _) => await RunNextPlanStepAsync();
        var bSave = new Button { Content = "save plan as ritual" };
        bSave.Click += (_, _) => SavePlanAsRitual();
        var bClr = new Button { Content = "clear plan" };
        bClr.Click += (_, _) =>
        {
            _planPreview.Text = "";
            _planExec.Text = "";
            _planSteps.Clear();
            _planStepIndex = 0;
        };
        var bPanic = new Button { Content = "panic stop" };
        bPanic.Click += (_, _) =>
        {
            _cts?.Cancel();
            NexusShell.Log("panic stop");
        };
        var bSpeak = new Button { Content = "speak response" };
        bSpeak.Click += async (_, _) => await SpeakAsync();

        tools.Children.Add(bAsk);
        tools.Children.Add(bSmoke);
        tools.Children.Add(bImport);
        tools.Children.Add(bPtt0);
        tools.Children.Add(bPtt1);
        tools.Children.Add(bCancel);
        tools.Children.Add(bRun);
        tools.Children.Add(bApr);
        tools.Children.Add(bNext);
        tools.Children.Add(bSave);
        tools.Children.Add(bClr);
        tools.Children.Add(bPanic);
        tools.Children.Add(bSpeak);
        tools.Children.Add(_shots);
        tools.Children.Add(_know);

        var left = new StackPanel { Spacing = 8 };
        left.Children.Add(new TextBlock { Text = "Prompt", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        left.Children.Add(_prompt);
        left.Children.Add(new TextBlock { Text = "Assistant", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        left.Children.Add(_assistant);
        left.Children.Add(new TextBlock { Text = "Retrieval + context", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, FontSize = 12 });
        left.Children.Add(_retrieval);

        var right = new StackPanel { Spacing = 8 };
        right.Children.Add(new TextBlock { Text = "Action plan preview", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        right.Children.Add(_planPreview);
        right.Children.Add(new TextBlock { Text = "Plan execution log", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        right.Children.Add(_planExec);

        var mid = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        mid.Content = tools;

        _root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        _root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        _root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetRow(mid, 0);
        Grid.SetColumnSpan(mid, 2);
        Grid.SetRow(_busy, 1);
        Grid.SetColumnSpan(_busy, 2);
        var body = new Grid();
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
            WinUiShellState.SetStatus("Ask ready");
        };
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

        _busy.IsOpen = true;
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
            _busy.IsOpen = false;
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
        _busy.IsOpen = true;
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
            _busy.IsOpen = false;
        }
    }

    private async Task RunSmokeAsync()
    {
        _busy.IsOpen = true;
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
            _busy.IsOpen = false;
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

        _busy.IsOpen = true;
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
                var bundle = KnowledgeSnippetService.BuildContextBundle(prompt, 6000);
                knowOverride = bundle.ContextText;
                _retrieval.Text = bundle.ContextText ?? "(no match)";
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
            _busy.IsOpen = false;
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

    private void SavePlanAsRitual()
    {
        var steps = ActivePlanSteps();
        if (steps.Count == 0)
            return;
        var title = _prompt.Text?.Trim();
        if (string.IsNullOrWhiteSpace(title))
            title = "Ritual " + DateTime.Now.ToString("yyyy-MM-dd HH:mm");
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
        NexusShell.Log("Saved ritual: " + recipe.Name);
    }

    private async Task SpeakAsync()
    {
        var t = _assistant.Text?.Trim();
        if (string.IsNullOrEmpty(t))
            return;
        _busy.IsOpen = true;
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
            _busy.IsOpen = false;
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
