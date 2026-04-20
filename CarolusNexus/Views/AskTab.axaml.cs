using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using CarolusNexus.Models;
using CarolusNexus.Services;

namespace CarolusNexus.Views;

public partial class AskTab : Avalonia.Controls.UserControl
{
    private Func<NexusSettings> _getSettings = () => new();
    private CancellationTokenSource? _cts;
    private List<RecipeStep> _planSteps = new();
    private int _planStepIndex;
    private WindowsMicRecorder? _mic;
    private bool _isRecording;
    private bool _operationBusy;
    private bool _awaitGlobalHotkeyRelease;
    private ResponsiveBand? _askLayoutBand;

    public AskTab()
    {
        InitializeComponent();
        Wire();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        LayoutUpdated += AskOnLayoutUpdated;
        ApplyAskResponsiveLayout();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        LayoutUpdated -= AskOnLayoutUpdated;
        base.OnDetachedFromVisualTree(e);
    }

    private void AskOnLayoutUpdated(object? sender, EventArgs e) => ApplyAskResponsiveLayout();

    private void ApplyAskResponsiveLayout()
    {
        var w = Bounds.Width;
        if (w <= 0)
            return;
        var band = ResponsiveLayout.GetBand(w);
        var twoCol = band != ResponsiveBand.Narrow;
        if (_askLayoutBand == band)
            return;
        _askLayoutBand = band;

        AskRootGrid.ColumnDefinitions.Clear();
        AskRootGrid.RowDefinitions.Clear();
        if (twoCol)
        {
            AskRootGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            AskRootGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            AskRootGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            AskRootGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            AskRootGrid.RowDefinitions.Add(new RowDefinition(GridLength.Star));
            Grid.SetRow(AskToolbarHost, 0);
            Grid.SetColumn(AskToolbarHost, 0);
            Grid.SetColumnSpan(AskToolbarHost, 2);
            Grid.SetRow(AskBusyBar, 1);
            Grid.SetColumn(AskBusyBar, 0);
            Grid.SetColumnSpan(AskBusyBar, 2);
            Grid.SetRow(AskLeftScroll, 2);
            Grid.SetColumn(AskLeftScroll, 0);
            Grid.SetColumnSpan(AskLeftScroll, 1);
            Grid.SetRow(AskRightScroll, 2);
            Grid.SetColumn(AskRightScroll, 1);
            Grid.SetColumnSpan(AskRightScroll, 1);
            AskLeftScroll.Margin = new Thickness(0, 0, 8, 0);
        }
        else
        {
            AskRootGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            AskRootGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            AskRootGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            AskRootGrid.RowDefinitions.Add(new RowDefinition(GridLength.Star));
            AskRootGrid.RowDefinitions.Add(new RowDefinition(GridLength.Star));
            Grid.SetRow(AskToolbarHost, 0);
            Grid.SetColumn(AskToolbarHost, 0);
            Grid.SetColumnSpan(AskToolbarHost, 1);
            Grid.SetRow(AskBusyBar, 1);
            Grid.SetColumn(AskBusyBar, 0);
            Grid.SetColumnSpan(AskBusyBar, 1);
            Grid.SetRow(AskLeftScroll, 2);
            Grid.SetColumn(AskLeftScroll, 0);
            Grid.SetColumnSpan(AskLeftScroll, 1);
            Grid.SetRow(AskRightScroll, 3);
            Grid.SetColumn(AskRightScroll, 0);
            Grid.SetColumnSpan(AskRightScroll, 1);
            AskLeftScroll.Margin = new Thickness(0, 0, 0, 8);
        }
    }

    public void SetSettingsProvider(Func<NexusSettings> getSettings) => _getSettings = getSettings;

    /// <summary>Tray / extern: Prompt setzen und gleichen Ask-Flow wie „ask now“ ausführen.</summary>
    public async Task RunAskFromExternalAsync(string promptText)
    {
        PromptBox.Text = promptText;
        await RunAskAsync().ConfigureAwait(true);
    }

    /// <summary>Loslassen der System-Hotkey-Taste wird über GetAsyncKeyState gepollt (nur dann true).</summary>
    public bool AwaitsGlobalHotkeyRelease => _isRecording && _awaitGlobalHotkeyRelease;

    public void NotifyGlobalPushToTalkPressed()
    {
        if (!OperatingSystem.IsWindows())
            return;
        TextToSpeechService.RequestBargeIn();
        StartMicRecording("(Hotkey)", awaitGlobalHotkeyRelease: true);
    }

    public async Task NotifyGlobalPushToTalkReleasedAsync()
    {
        if (!_isRecording)
            return;
        await StopMicTranscribeAndAskAsync().ConfigureAwait(true);
    }

    private void Wire()
    {
        BtnAskNow.Click += async (_, _) => await RunAskAsync();
        BtnSmoke.Click += async (_, _) => await RunSmokeAsync();
        BtnImportAudio.Click += async (_, _) => await ImportAudioAndTranscribeAsync();
        BtnPttStart.Click += (_, _) =>
        {
            if (!OperatingSystem.IsWindows())
            {
                NexusShell.Log("Push-to-Talk: Windows only.");
                return;
            }

            StartMicRecording("(Button)", awaitGlobalHotkeyRelease: false);
        };
        BtnPttStop.Click += async (_, _) => await StopMicTranscribeAndAskAsync();
        BtnCancelRec.Click += (_, _) => CancelMicRecording();
        BtnClearConv.Click += (_, _) =>
        {
            AssistantOut.Text = "";
            NexusShell.Log("Conversation cleared.");
        };
        BtnRunPlan.Click += async (_, _) => await ExecutePlanAsync(dryRun: false);
        BtnApproveRun.Click += async (_, _) => await ExecutePlanAfterApprovalGateAsync();
        BtnRunNext.Click += async (_, _) => await RunNextPlanStepAsync();
        BtnSaveRitual.Click += (_, _) => SavePlanAsRitual();
        BtnClearPlan.Click += (_, _) =>
        {
            PlanPreview.Text = "";
            PlanExec.Text = "";
            _planSteps.Clear();
            _planStepIndex = 0;
            NexusShell.Log("Plan cleared.");
        };
        BtnPanic.Click += (_, _) =>
        {
            _cts?.Cancel();
            NexusShell.Log("panic stop — cancel requested.");
        };
        BtnSpeak.Click += async (_, _) => await SpeakAssistantResponseAsync();
        BtnCopyAnswer.Click += async (_, _) => await CopyAssistantAnswerAsync();
        BtnInsertKnowledge.Click += (_, _) => InsertKnowledgeSnippetIntoPrompt();
        BtnPolishPrompt.Click += async (_, _) => await PolishPromptWithLlmAsync();
        BtnPolishAnswer.Click += async (_, _) => await PolishAssistantAnswerAsync();
        BtnParseJsonPlan.Click += (_, _) => ApplyJsonPlanFromAssistant();
        BtnLlmJsonPlan.Click += async (_, _) => await ExtractStructuredPlanWithLlmAsync().ConfigureAwait(true);
        BtnExplainPlan.Click += async (_, _) => await ExplainCurrentPlanAsync().ConfigureAwait(true);
        BtnChipSummary.Click += (_, _) => AppendPromptLine("Summarize the key points briefly in clear language.");
        BtnChipNext.Click += (_, _) =>
            AppendPromptLine("What concrete next steps do you recommend? Number them and make them actionable.");
        BtnChipRisks.Click += (_, _) =>
            AppendPromptLine("What risks do you see — and how can we mitigate them?");
        BtnChipExplain.Click += (_, _) => AppendPromptLine("Explain this in plain language for someone without domain jargon.");
    }

    /// <summary>Enter (und Ctrl+Enter) sendet wie „ask now“; nur Shift+Enter fügt eine neue Zeile ein.</summary>
    private async void OnPromptBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
            return;
        if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
            return;

        if (_operationBusy)
        {
            e.Handled = true;
            return;
        }

        e.Handled = true;
        await RunAskAsync().ConfigureAwait(true);
    }

    private void AppendPromptLine(string line)
    {
        var cur = PromptBox.Text ?? "";
        var prefix = string.IsNullOrWhiteSpace(cur) ? "" : (cur.EndsWith("\n", StringComparison.Ordinal) ? "" : "\n\n");
        PromptBox.Text = cur + prefix + line;
    }

    private void InsertKnowledgeSnippetIntoPrompt()
    {
        var q = PromptBox.Text?.Trim();
        var bundle = KnowledgeSnippetService.BuildContextBundle(string.IsNullOrEmpty(q) ? "." : q, 3500);
        if (string.IsNullOrWhiteSpace(bundle.ContextText))
        {
            NexusShell.Log("Insert knowledge: knowledge\\ empty or no match.");
            ClearRetrievalSources();
            return;
        }

        AppendPromptLine("[Context from local knowledge]\n" + bundle.ContextText.TrimEnd());
        RetrievalOut.Text = bundle.ContextText;
        RenderRetrievalSources(bundle.Sources);
        NexusShell.Log("Knowledge excerpt appended to prompt.");
    }

    private async Task PolishPromptWithLlmAsync()
    {
        var raw = PromptBox.Text?.Trim();
        if (string.IsNullOrEmpty(raw))
        {
            NexusShell.Log("Polish prompt: enter text in the prompt field first.");
            return;
        }

        var s = _getSettings();
        if (!DotEnvStore.HasProviderKey(s.Provider))
        {
            NexusShell.Log("Polish prompt: API key missing for selected provider (.env).");
            return;
        }

        SetBusy(true);
        CompanionHub.Publish(CompanionVisualState.Thinking);
        try
        {
            _cts = new CancellationTokenSource();
            const string sys =
                "You refine technical prompts. Make the user's text clearer and tighter in English. " +
                "Do not invent facts, no greeting. At most a few sentences or short bullets. " +
                "Output only the improved wording.";
            var polished = await LlmChatService.CompleteUtilityAsync(s, sys, raw, _cts.Token).ConfigureAwait(true);
            if (LooksLikeLlmErrorPrefix(polished))
            {
                NexusShell.Log("Polish prompt: " + polished);
                return;
            }

            PromptBox.Text = polished.Trim();
            NexusShell.Log("Prompt polished.");
        }
        catch (Exception ex)
        {
            NexusShell.Log("Polish prompt: " + ex.Message);
            CompanionHub.Publish(CompanionVisualState.Error);
        }
        finally
        {
            SetBusy(false);
            CompanionHub.Publish(CompanionVisualState.Ready);
        }
    }

    private async Task PolishAssistantAnswerAsync()
    {
        var raw = AssistantOut.Text?.Trim();
        if (string.IsNullOrEmpty(raw))
        {
            NexusShell.Log("Polish answer: no assistant reply.");
            return;
        }

        var s = _getSettings();
        if (!DotEnvStore.HasProviderKey(s.Provider))
        {
            NexusShell.Log("Polish answer: API key missing for provider (.env).");
            return;
        }

        SetBusy(true);
        CompanionHub.Publish(CompanionVisualState.Thinking);
        try
        {
            _cts = new CancellationTokenSource();
            const string sys =
                "You rewrite an assistant reply for the user: clearer structure, tighter sentences, " +
                "same facts — invent nothing. English. No new greeting. Output only the improved text.";
            var polished = await LlmChatService.CompleteUtilityAsync(s, sys, raw, _cts.Token).ConfigureAwait(true);
            if (LooksLikeLlmErrorPrefix(polished))
            {
                NexusShell.Log("Polish answer: " + polished);
                return;
            }

            AssistantOut.Text = polished.Trim();
            NexusShell.Log("Assistant reply polished.");
        }
        catch (Exception ex)
        {
            NexusShell.Log("Polish answer: " + ex.Message);
            CompanionHub.Publish(CompanionVisualState.Error);
        }
        finally
        {
            SetBusy(false);
            CompanionHub.Publish(CompanionVisualState.Ready);
        }
    }

    private void ApplyJsonPlanFromAssistant()
    {
        var t = AssistantOut.Text ?? "";
        if (!PlanJsonParser.TryParseRecipeStepsFromText(t, out var steps) || steps.Count == 0)
        {
            NexusShell.Log("JSON plan: nothing parseable in assistant reply.");
            return;
        }

        _planSteps = steps;
        _planStepIndex = 0;
        PlanPreview.Text = FormatStepsForPreview(_planSteps);
        PlanExec.Text = $"({_planSteps.Count} steps from JSON)";
        NexusShell.Log($"JSON plan: {_planSteps.Count} steps applied.");
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

    private async Task ExtractStructuredPlanWithLlmAsync()
    {
        var t = AssistantOut.Text?.Trim();
        if (string.IsNullOrEmpty(t))
        {
            NexusShell.Log("AI JSON: no assistant reply.");
            return;
        }

        var s = _getSettings();
        if (!DotEnvStore.HasProviderKey(s.Provider))
        {
            NexusShell.Log("AI JSON: API key missing (.env).");
            return;
        }

        SetBusy(true);
        CompanionHub.Publish(CompanionVisualState.Thinking);
        try
        {
            _cts = new CancellationTokenSource();
            var steps = await LlmStructuredPlanService.TryExtractStepsJsonAsync(s, t, _cts.Token).ConfigureAwait(true);
            if (steps == null || steps.Count == 0)
            {
                NexusShell.Log("AI JSON: no steps extracted.");
                return;
            }

            _planSteps = steps;
            _planStepIndex = 0;
            PlanPreview.Text = FormatStepsForPreview(_planSteps);
            PlanExec.Text = $"({_planSteps.Count} steps via AI JSON)";
            NexusShell.Log($"AI JSON: {_planSteps.Count} steps applied.");
        }
        catch (Exception ex)
        {
            NexusShell.Log("AI JSON: " + ex.Message);
            CompanionHub.Publish(CompanionVisualState.Error);
        }
        finally
        {
            SetBusy(false);
            CompanionHub.Publish(CompanionVisualState.Ready);
        }
    }

    private async Task ExplainCurrentPlanAsync()
    {
        var steps = ActivePlanSteps();
        if (steps.Count == 0)
        {
            NexusShell.Log("Explain plan: no steps.");
            SafetyOut.Text = "(no plan)";
            return;
        }

        var s = _getSettings();
        if (!DotEnvStore.HasProviderKey(s.Provider))
        {
            NexusShell.Log("Explain plan: API key missing (.env).");
            return;
        }

        SetBusy(true);
        CompanionHub.Publish(CompanionVisualState.Thinking);
        try
        {
            _cts = new CancellationTokenSource();
            var blob = FormatStepsForPreview(steps);
            const string sys =
                "Explain the following automation plan to an operator: briefly step by step, " +
                "then 3–5 possible risks and a short dry-run recommendation. English, no greeting, markdown optional.";
            var r = await LlmChatService.CompleteUtilityAsync(s, sys, blob, _cts.Token).ConfigureAwait(true);
            if (LooksLikeLlmErrorPrefix(r))
            {
                NexusShell.Log("Explain plan: " + r);
                return;
            }

            SafetyOut.Text = r.Trim();
            NexusShell.Log("Explain plan: text under „Safety / Recovery“.");
        }
        catch (Exception ex)
        {
            NexusShell.Log("Explain plan: " + ex.Message);
            CompanionHub.Publish(CompanionVisualState.Error);
        }
        finally
        {
            SetBusy(false);
            CompanionHub.Publish(CompanionVisualState.Ready);
        }
    }

    private async Task ExecutePlanAfterApprovalGateAsync()
    {
        var steps = ActivePlanSteps();
        if (steps.Count == 0)
        {
            PlanExec.Text = "No steps — run „ask now“ or pick a plan first.";
            NexusShell.Log("approve + run: no plan.");
            return;
        }

        if (OperatingSystem.IsWindows())
        {
            var s = _getSettings();
            var risk = RiskScoreService.ClassifyPlan(steps, s);
            var r = System.Windows.Forms.MessageBox.Show(
                $"Run the detected plan? Risk heuristic: {risk}. (Depending on safety profile: simulation or real Win32 steps.)",
                "Carolus Nexus — approve plan",
                System.Windows.Forms.MessageBoxButtons.OKCancel,
                System.Windows.Forms.MessageBoxIcon.Warning);
            if (r != System.Windows.Forms.DialogResult.OK)
            {
                NexusShell.Log("Execution cancelled (not approved).");
                return;
            }

            if (string.Equals(risk, "high", StringComparison.OrdinalIgnoreCase) && s.HighRiskSecondConfirm)
            {
                var r2 = System.Windows.Forms.MessageBox.Show(
                    "High-risk plan — confirm a second time to execute or simulate.",
                    "Carolus Nexus — high risk gate",
                    System.Windows.Forms.MessageBoxButtons.OKCancel,
                    System.Windows.Forms.MessageBoxIcon.Warning);
                if (r2 != System.Windows.Forms.DialogResult.OK)
                {
                    NexusShell.Log("Execution cancelled (high-risk second gate).");
                    return;
                }
            }
        }
        else
            NexusShell.Log("approve + run: no dialog (not Windows).");

        SafetyOut.Text = $"Approved {DateTime.Now:T}: {steps.Count} steps — execution starting.";
        await ExecutePlanAsync(dryRun: false).ConfigureAwait(true);
    }

    private async Task MaybeAppendAutomationSuggestionsAsync(NexusSettings s, string assistantText, CancellationToken ct)
    {
        if (!s.SuggestAutomations || string.IsNullOrWhiteSpace(assistantText))
            return;
        if (!DotEnvStore.HasProviderKey(s.Provider))
            return;

        try
        {
            const string sys =
                "From the following assistant reply: suggest 1–3 concrete short automation steps in English " +
                "(e.g. as token lines or [ACTION:…]), without repeating the content. No greeting, at most one paragraph or numbered list.";
            var clip = assistantText.Length > 5000 ? assistantText[..5000] + "…" : assistantText;
            var extra = await LlmChatService.CompleteUtilityAsync(s, sys, clip, ct).ConfigureAwait(true);
            if (LooksLikeLlmErrorPrefix(extra))
                return;
            AssistantOut.Text += "\n\n--- Automation suggestions ---\n" + extra.Trim();
            NexusShell.Log("SuggestAutomations: suggestions appended to reply.");
        }
        catch (Exception ex)
        {
            NexusShell.Log("SuggestAutomations: " + ex.Message);
        }
    }

    private static bool LooksLikeLlmErrorPrefix(string text)
    {
        var t = text.TrimStart();
        return t.StartsWith("Missing ", StringComparison.Ordinal)
               || t.StartsWith("Fehlt ", StringComparison.Ordinal)
               || t.StartsWith("Unknown provider", StringComparison.Ordinal)
               || t.StartsWith("Unbekannter Provider", StringComparison.Ordinal)
               || t.StartsWith("Anthropic HTTP", StringComparison.Ordinal)
               || t.StartsWith("OpenAI", StringComparison.Ordinal)
               || t.StartsWith("OpenAI-compatible", StringComparison.Ordinal)
               || t.StartsWith("OpenAI-kompatibel", StringComparison.Ordinal);
    }

    private void ClearRetrievalSources()
    {
        RetrievalSourcesPanel.Children.Clear();
        RetrievalSourcesHeader.IsVisible = false;
        RetrievalSourcesPanel.IsVisible = false;
    }

    private void RenderRetrievalSources(IReadOnlyList<KnowledgeSourceRef> sources)
    {
        RetrievalSourcesPanel.Children.Clear();
        if (sources == null || sources.Count == 0)
        {
            RetrievalSourcesHeader.IsVisible = false;
            RetrievalSourcesPanel.IsVisible = false;
            return;
        }

        RetrievalSourcesHeader.IsVisible = true;
        RetrievalSourcesPanel.IsVisible = true;
        foreach (var src in sources)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            row.Children.Add(new TextBlock
            {
                Text = src.Label,
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center,
                MaxWidth = 420
            });
            if (!string.IsNullOrEmpty(src.FullPath))
            {
                var btn = new Button { Content = "Open", Tag = src.FullPath, Padding = new Thickness(10, 4) };
                btn.Click += OnOpenKnowledgeSourceClick;
                row.Children.Add(btn);
            }

            RetrievalSourcesPanel.Children.Add(row);
        }
    }

    private void OnOpenKnowledgeSourceClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button b || b.Tag is not string path)
            return;
        TryOpenKnowledgePath(path);
    }

    private static void TryOpenKnowledgePath(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
                return;
            }

            if (Directory.Exists(path))
            {
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
                return;
            }

            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                Process.Start(new ProcessStartInfo("explorer.exe", dir) { UseShellExecute = true });
            else
                NexusShell.Log("Source not found: " + path);
        }
        catch (Exception ex)
        {
            NexusShell.Log("Open source: " + ex.Message);
        }
    }

    private async Task CopyAssistantAnswerAsync()
    {
        var top = TopLevel.GetTopLevel(this);
        if (top?.Clipboard == null)
        {
            NexusShell.Log("Clipboard not available.");
            return;
        }

        var t = AssistantOut.Text ?? "";
        if (string.IsNullOrWhiteSpace(t))
        {
            NexusShell.Log("No assistant reply to copy.");
            return;
        }

        await top.Clipboard.SetTextAsync(t).ConfigureAwait(true);
        NexusShell.Log("Assistant reply copied to clipboard.");
    }

    private void StartMicRecording(string source, bool awaitGlobalHotkeyRelease)
    {
        if (_isRecording)
            return;
        try
        {
            _mic ??= new WindowsMicRecorder();
            _mic.Start();
            _isRecording = true;
            _awaitGlobalHotkeyRelease = awaitGlobalHotkeyRelease;
            TranscriptOut.Text = $"Recording active {source} — release key or „stop + ask“ for transcript.";
            NexusShell.Log($"Microphone recording started {source}");
            RefreshInputStates();
            CompanionHub.Publish(CompanionVisualState.Listening);
        }
        catch (Exception ex)
        {
            NexusShell.Log("Microphone start: " + ex.Message);
            TranscriptOut.Text = "Microphone: " + ex.Message;
            _awaitGlobalHotkeyRelease = false;
        }
    }

    private void CancelMicRecording()
    {
        if (!_isRecording)
        {
            TranscriptOut.Text = "(no active recording)";
            return;
        }

        _isRecording = false;
        _awaitGlobalHotkeyRelease = false;
        _mic?.StopSync();
        TranscriptOut.Text = "Recording cancelled.";
        NexusShell.Log("Microphone recording cancelled.");
        RefreshInputStates();
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
            NexusShell.Log("Microphone stop: " + ex.Message);
            TranscriptOut.Text = "Microphone stop: " + ex.Message;
            RefreshInputStates();
            CompanionHub.Publish(CompanionVisualState.Ready);
            return;
        }

        RefreshInputStates();
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            TranscriptOut.Text = "No audio file produced.";
            CompanionHub.Publish(CompanionVisualState.Ready);
            return;
        }

        SetBusy(true);
        CompanionHub.Publish(CompanionVisualState.Transcribing);
        string? transcript = null;
        try
        {
            _cts = new CancellationTokenSource();
            transcript = await Task.Run(async () =>
                    await SpeechTranscriptionService.TranscribeFileAsync(path, _cts.Token).ConfigureAwait(false))
                .ConfigureAwait(true);
            TranscriptOut.Text = transcript;
            if (!string.IsNullOrWhiteSpace(transcript))
                PromptBox.Text = transcript;
            NexusShell.Log("Transcript ready (" + transcript.Length + " chars).");
        }
        catch (Exception ex)
        {
            TranscriptOut.Text = "Transcription: " + ex.Message;
            NexusShell.Log("Transcription error: " + ex.Message);
            CompanionHub.Publish(CompanionVisualState.Ready);
        }
        finally
        {
            TryDelete(path);
            SetBusy(false);
        }

        if (!string.IsNullOrWhiteSpace(transcript) && !LooksLikeTranscriptionFailure(transcript))
            await RunAskAsync();
        else if (transcript != null)
            CompanionHub.Publish(CompanionVisualState.Ready);
    }

    private static bool LooksLikeTranscriptionFailure(string t)
    {
        var s = t.TrimStart();
        return s.StartsWith("Missing ", StringComparison.Ordinal)
               || s.StartsWith("Fehlt ", StringComparison.Ordinal)
               || s.StartsWith("No audio file", StringComparison.Ordinal)
               || s.StartsWith("Keine Audiodatei", StringComparison.Ordinal)
               || s.StartsWith("Whisper ", StringComparison.Ordinal)
               || s.StartsWith("Whisper:", StringComparison.Ordinal)
               || s.StartsWith("ElevenLabs STT HTTP", StringComparison.Ordinal)
               || s.StartsWith("ElevenLabs:", StringComparison.Ordinal);
    }

    private async Task ImportAudioAndTranscribeAsync()
    {
        var top = TopLevel.GetTopLevel(this);
        if (top == null)
            return;

        var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Audio for transcription",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Audio")
                {
                    Patterns = ["*.wav", "*.mp3", "*.m4a", "*.flac", "*.ogg", "*.webm"]
                }
            ]
        }).ConfigureAwait(true);

        var f = files.FirstOrDefault();
        if (f == null)
            return;

        string? tmp = null;
        SetBusy(true);
        try
        {
            _cts = new CancellationTokenSource();
            await using var stream = await f.OpenReadAsync().ConfigureAwait(true);
            tmp = Path.Combine(Path.GetTempPath(), "carolus-import-" + Guid.NewGuid().ToString("N") + Path.GetExtension(f.Name));
            await using (var fs = File.Create(tmp))
                await stream.CopyToAsync(fs, _cts.Token).ConfigureAwait(true);

            var transcript = await Task.Run(async () =>
                    await SpeechTranscriptionService.TranscribeFileAsync(tmp, _cts.Token).ConfigureAwait(false))
                .ConfigureAwait(true);
            TranscriptOut.Text = transcript;
            if (!string.IsNullOrWhiteSpace(transcript))
                PromptBox.Text = transcript;
            NexusShell.Log("Import + transcript done.");
        }
        catch (Exception ex)
        {
            TranscriptOut.Text = "Import: " + ex.Message;
            NexusShell.Log("Import audio error: " + ex.Message);
        }
        finally
        {
            TryDelete(tmp);
            SetBusy(false);
            CompanionHub.Publish(CompanionVisualState.Ready);
        }
    }

    private async Task SpeakAssistantResponseAsync()
    {
        var text = AssistantOut.Text?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            NexusShell.Log("TTS: no assistant reply.");
            return;
        }

        SetBusy(true);
        CompanionHub.Publish(CompanionVisualState.Speaking);
        try
        {
            _cts = new CancellationTokenSource();
            var err = await TextToSpeechService.SpeakAsync(text, _cts.Token).ConfigureAwait(true);
            if (!string.IsNullOrEmpty(err))
            {
                NexusShell.Log("TTS: " + err);
                TranscriptOut.Text = err;
            }
            else
                NexusShell.Log("TTS finished.");
        }
        catch (Exception ex)
        {
            NexusShell.Log("TTS error: " + ex.Message);
        }
        finally
        {
            SetBusy(false);
            CompanionHub.Publish(CompanionVisualState.Ready);
        }
    }

    private static void TryDelete(string? path)
    {
        if (string.IsNullOrEmpty(path))
            return;
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // ignore
        }
    }

    private async Task RunSmokeAsync()
    {
        SetBusy(true);
        CompanionHub.Publish(CompanionVisualState.Thinking);
        try
        {
            _cts = new CancellationTokenSource();
            var s = _getSettings();
            NexusShell.Log($"smoke test · provider {s.Provider}, model {s.Model}");
            var text = await LlmChatService.SmokeAsync(s, _cts.Token).ConfigureAwait(true);
            AssistantOut.Text = text;
            NexusShell.Log("smoke test finished.");
        }
        catch (Exception ex)
        {
            AssistantOut.Text = "Error: " + ex.Message;
            NexusShell.Log("smoke test error: " + ex.Message);
            CompanionHub.Publish(CompanionVisualState.Error);
        }
        finally
        {
            SetBusy(false);
            CompanionHub.Publish(CompanionVisualState.Ready);
        }
    }

    private async Task RunAskAsync()
    {
        var prompt = PromptBox.Text?.Trim();
        if (string.IsNullOrEmpty(prompt))
        {
            AssistantOut.Text = "Please enter a prompt.";
            return;
        }

        SetBusy(true);
        CompanionHub.Publish(CompanionVisualState.Thinking);
        try
        {
            _cts = new CancellationTokenSource();
            var s = _getSettings();
            var ct = _cts.Token;

            if (AskPromptRouter.TryPersonaGreeting(prompt, out var greet))
            {
                AssistantOut.Text = greet;
                PlanPreview.Text = "(Persona)";
                PlanExec.Text = "";
                _planSteps.Clear();
                NexusShell.Log("ask · persona greeting.");
                return;
            }

            if (AskPromptRouter.TryParseCliRoute(prompt, out var route) && route != null)
            {
                await RunCliHandoffFromAskAsync(s, route, ct).ConfigureAwait(true);
                return;
            }

            var shots = IncludeScreenshots.IsChecked == true;
            var know = UseKnowledgeInAsk.IsChecked == true;

            string? knowledgeOverride = null;
            if (know)
            {
                var bundle = KnowledgeSnippetService.BuildContextBundle(prompt, 6000);
                knowledgeOverride = bundle.ContextText;
                RetrievalOut.Text = string.IsNullOrWhiteSpace(bundle.ContextText)
                    ? "(no local match)"
                    : bundle.ContextText;
                RenderRetrievalSources(bundle.Sources);
            }
            else
            {
                RetrievalOut.Text = "(local knowledge not included)";
                ClearRetrievalSources();
            }

            var fusion = UiAutomationVisionFusion.BuildAskAugmentation(s, shots);
            var adapt = OperatorAdapterRegistry.TryEnrichForegroundContext();
            if (!string.IsNullOrWhiteSpace(adapt))
                fusion = string.IsNullOrWhiteSpace(fusion) ? adapt : fusion + "\n\n" + adapt;
            var effectivePrompt = string.IsNullOrWhiteSpace(fusion)
                ? prompt
                : fusion + "\n\n---\n" + prompt;

            NexusShell.Log($"ask now · screenshots={shots}, knowledge={know}, uia+fusion={s.IncludeUiaContextInAsk || shots}");
            var text = await LlmChatService
                .CompleteAsync(s, effectivePrompt, shots, know, ct, knowledgeContextOverride: know ? knowledgeOverride : null)
                .ConfigureAwait(true);
            AssistantOut.Text = text;

            var tokens = ActionPlanExtractor.Extract(text);
            var fromRegex = ActionPlanExtractor.ToRecipeSteps(tokens);
            _planSteps = fromRegex;
            if (_planSteps.Count == 0 && PlanJsonParser.TryParseRecipeStepsFromText(text, out var jsonSteps) &&
                jsonSteps.Count > 0)
                _planSteps = jsonSteps;
            _planStepIndex = 0;
            PlanPreview.Text = fromRegex.Count > 0
                ? ActionPlanExtractor.FormatPreview(tokens)
                : (_planSteps.Count > 0
                    ? FormatStepsForPreview(_planSteps)
                    : ActionPlanExtractor.FormatPreview(tokens));
            PlanExec.Text = $"({_planSteps.Count} steps detected — „run plan“ or „run next step“)";

            await MaybeAppendAutomationSuggestionsAsync(s, text, ct).ConfigureAwait(true);

            if (s.SpeakResponses)
            {
                CompanionHub.Publish(CompanionVisualState.Speaking);
                var ttsErr = await TextToSpeechService.SpeakAsync(text, ct).ConfigureAwait(true);
                if (!string.IsNullOrEmpty(ttsErr))
                    NexusShell.Log("TTS (auto): " + ttsErr);
            }

            NexusShell.Log("ask now finished.");
        }
        catch (OperationCanceledException)
        {
            AssistantOut.Text += "\n\n[Cancelled]";
        }
        catch (Exception ex)
        {
            AssistantOut.Text = "Error: " + ex;
            NexusShell.Log("ask error: " + ex.Message);
            CompanionHub.Publish(CompanionVisualState.Error);
        }
        finally
        {
            SetBusy(false);
            CompanionHub.Publish(CompanionVisualState.Ready);
        }
    }

    /// <summary>Vision + lokaler CLI-Agent in einem Prompt (USP: Dev+Ops in einer Shell).</summary>
    private async Task RunCliHandoffFromAskAsync(NexusSettings s, CliAskRoute route, CancellationToken ct)
    {
        var payload = route.Payload?.Trim() ?? "";
        if (string.IsNullOrEmpty(payload))
        {
            AssistantOut.Text =
                "Missing task text after the trigger. Example: „use codex … describe the files in playground.“";
            NexusShell.Log("CLI handoff: empty payload.");
            return;
        }

        RetrievalOut.Text = "(CLI handoff — logs under „codex output“)";
        ClearRetrievalSources();

        var combined = payload;
        if (route.WithScreenSummary)
        {
            if (OperatingSystem.IsWindows())
            {
                NexusShell.Log("CLI handoff: vision summary (multi-monitor) for Codex …");
                CompanionHub.Publish(CompanionVisualState.Thinking);
                var visionPrompt =
                    "In 6–10 short bullets in English, describe what is visible on all monitors (main windows, visible apps, readable titles). No greeting.";
                var summary = await LlmChatService.CompleteAsync(s, visionPrompt, true, false, ct).ConfigureAwait(true);
                combined =
                    "[Auto: multi-monitor snapshot, briefly described by assistant]\n" + summary.Trim() +
                    "\n\n---\n[Operator / Codex task]\n" + payload;
            }
            else
            {
                NexusShell.Log("CLI handoff: vision Windows-only — Codex without screen context.");
            }
        }

        NexusShell.Log($"CLI handoff: {route.Agent}");
        var (logPath, excerpt) = await CliAgentRunner.RunAsync(route.Agent, combined, ct).ConfigureAwait(true);
        AssistantOut.Text = excerpt + "\n\n— Log file —\n" + logPath;
        PlanPreview.Text = "(CLI output — create a new plan manually or via the next Ask if needed)";
        PlanExec.Text = "";
        _planSteps.Clear();
        _planStepIndex = 0;
    }

    private async Task ExecutePlanAsync(bool dryRun)
    {
        var steps = ActivePlanSteps();
        if (steps.Count == 0)
        {
            PlanExec.Text = "No steps — run „ask now“ or paste plan lines first.";
            return;
        }

        _cts = new CancellationTokenSource();
        PlanExec.Text = await SimplePlanSimulator.RunAsync(steps, dryRun, _getSettings(), _cts.Token).ConfigureAwait(true);
        _planStepIndex = 0;
    }

    private async Task RunNextPlanStepAsync()
    {
        var steps = ActivePlanSteps();
        if (steps.Count == 0)
            return;
        if (_planStepIndex >= steps.Count)
        {
            NexusShell.Log("All plan steps done.");
            return;
        }

        _cts ??= new CancellationTokenSource();
        var slice = new List<RecipeStep> { steps[_planStepIndex] };
        var line = await SimplePlanSimulator.RunAsync(slice, false, _getSettings(), _cts.Token).ConfigureAwait(true);
        PlanExec.Text += "\n" + line;
        _planStepIndex++;
    }

    private List<RecipeStep> ActivePlanSteps()
    {
        if (_planSteps.Count > 0)
            return _planSteps;
        return SimplePlanSimulator.ParsePlanPreviewLines(PlanPreview.Text ?? "");
    }

    private void SavePlanAsRitual()
    {
        var steps = ActivePlanSteps();
        if (steps.Count == 0)
        {
            NexusShell.Log("No plan to save.");
            return;
        }

        var title = PromptBox.Text?.Trim() ?? "";
        if (title.Length > 120)
            title = title[..120];
        if (string.IsNullOrWhiteSpace(title))
            title = "Ritual " + DateTime.Now.ToString("yyyy-MM-dd HH:mm");

        var recipe = new AutomationRecipe
        {
            Name = title,
            Description = "From Ask tab / plan extraction",
            Steps = steps.ToList()
        };
        RitualRecipeStore.AppendRecipe(recipe);
        NexusShell.Log($"Saved as ritual: {recipe.Name}");
    }

    private void SetBusy(bool busy)
    {
        _operationBusy = busy;
        AskBusyBar.IsVisible = busy;
        BtnAskNow.IsEnabled = !busy;
        BtnSmoke.IsEnabled = !busy;
        BtnImportAudio.IsEnabled = !busy;
        BtnSpeak.IsEnabled = !busy;
        BtnCopyAnswer.IsEnabled = !busy;
        BtnInsertKnowledge.IsEnabled = !busy;
        BtnPolishPrompt.IsEnabled = !busy;
        BtnPolishAnswer.IsEnabled = !busy;
        BtnChipSummary.IsEnabled = !busy;
        BtnChipNext.IsEnabled = !busy;
        BtnChipRisks.IsEnabled = !busy;
        BtnChipExplain.IsEnabled = !busy;
        BtnParseJsonPlan.IsEnabled = !busy;
        BtnLlmJsonPlan.IsEnabled = !busy;
        BtnExplainPlan.IsEnabled = !busy;
        RefreshInputStates();
    }

    private void RefreshInputStates()
    {
        BtnPttStart.IsEnabled = !_operationBusy && !_isRecording && OperatingSystem.IsWindows();
        BtnPttStop.IsEnabled = !_operationBusy && _isRecording;
        BtnCancelRec.IsEnabled = !_operationBusy && _isRecording;
    }
}
