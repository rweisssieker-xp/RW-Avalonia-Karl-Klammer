using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CarolusNexus.Models;
using CarolusNexus.Services;

namespace CarolusNexus.Views;

public partial class AskTab : UserControl
{
    private Func<NexusSettings> _getSettings = () => new();
    private CancellationTokenSource? _cts;
    private List<RecipeStep> _planSteps = new();
    private int _planStepIndex;
    private WindowsMicRecorder? _mic;
    private bool _isRecording;
    private bool _operationBusy;
    private bool _awaitGlobalHotkeyRelease;

    public AskTab()
    {
        InitializeComponent();
        Wire();
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
                NexusShell.Log("Push-to-Talk: nur unter Windows.");
                return;
            }

            StartMicRecording("(Button)", awaitGlobalHotkeyRelease: false);
        };
        BtnPttStop.Click += async (_, _) => await StopMicTranscribeAndAskAsync();
        BtnCancelRec.Click += (_, _) => CancelMicRecording();
        BtnClearConv.Click += (_, _) =>
        {
            AssistantOut.Text = "";
            NexusShell.Log("Konversation geleert.");
        };
        BtnRunPlan.Click += async (_, _) => await ExecutePlanAsync(dryRun: false);
        BtnApproveRun.Click += async (_, _) =>
        {
            NexusShell.Log("approve + run — Safety-Governance noch minimal; starte Lauf.");
            await ExecutePlanAsync(dryRun: false);
        };
        BtnRunNext.Click += async (_, _) => await RunNextPlanStepAsync();
        BtnSaveRitual.Click += (_, _) => SavePlanAsRitual();
        BtnClearPlan.Click += (_, _) =>
        {
            PlanPreview.Text = "";
            PlanExec.Text = "";
            _planSteps.Clear();
            _planStepIndex = 0;
            NexusShell.Log("Plan geleert.");
        };
        BtnPanic.Click += (_, _) =>
        {
            _cts?.Cancel();
            NexusShell.Log("panic stop — Abbruch angefordert.");
        };
        BtnSpeak.Click += async (_, _) => await SpeakAssistantResponseAsync();
        BtnCopyAnswer.Click += async (_, _) => await CopyAssistantAnswerAsync();
    }

    private async Task CopyAssistantAnswerAsync()
    {
        var top = TopLevel.GetTopLevel(this);
        if (top?.Clipboard == null)
        {
            NexusShell.Log("Zwischenablage nicht verfügbar.");
            return;
        }

        var t = AssistantOut.Text ?? "";
        if (string.IsNullOrWhiteSpace(t))
        {
            NexusShell.Log("Keine Assistant-Antwort zum Kopieren.");
            return;
        }

        await top.Clipboard.SetTextAsync(t).ConfigureAwait(true);
        NexusShell.Log("Assistant-Antwort in Zwischenablage kopiert.");
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
            TranscriptOut.Text = $"Aufnahme aktiv {source} — loslassen / „stop + ask“ für Transkript.";
            NexusShell.Log($"Mikrofon Aufnahme gestartet {source}");
            RefreshInputStates();
        }
        catch (Exception ex)
        {
            NexusShell.Log("Mikrofon Start: " + ex.Message);
            TranscriptOut.Text = "Mikrofon: " + ex.Message;
            _awaitGlobalHotkeyRelease = false;
        }
    }

    private void CancelMicRecording()
    {
        if (!_isRecording)
        {
            TranscriptOut.Text = "(keine aktive Aufnahme)";
            return;
        }

        _isRecording = false;
        _awaitGlobalHotkeyRelease = false;
        _mic?.StopSync();
        TranscriptOut.Text = "Aufnahme abgebrochen.";
        NexusShell.Log("Mikrofon Aufnahme abgebrochen.");
        RefreshInputStates();
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
            NexusShell.Log("Mikrofon Stop: " + ex.Message);
            TranscriptOut.Text = "Mikrofon Stop: " + ex.Message;
            RefreshInputStates();
            return;
        }

        RefreshInputStates();
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            TranscriptOut.Text = "Keine Audiodatei erzeugt.";
            return;
        }

        SetBusy(true);
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
            NexusShell.Log("Transkript fertig (" + transcript.Length + " Zeichen).");
        }
        catch (Exception ex)
        {
            TranscriptOut.Text = "Transkription: " + ex.Message;
            NexusShell.Log("Transkription Fehler: " + ex.Message);
        }
        finally
        {
            TryDelete(path);
            SetBusy(false);
        }

        if (!string.IsNullOrWhiteSpace(transcript) && !LooksLikeTranscriptionFailure(transcript))
            await RunAskAsync();
    }

    private static bool LooksLikeTranscriptionFailure(string t)
    {
        var s = t.TrimStart();
        return s.StartsWith("Fehlt ", StringComparison.Ordinal)
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
            Title = "Audio für Transkription",
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
            NexusShell.Log("Import + Transkript fertig.");
        }
        catch (Exception ex)
        {
            TranscriptOut.Text = "Import: " + ex.Message;
            NexusShell.Log("Import Audio Fehler: " + ex.Message);
        }
        finally
        {
            TryDelete(tmp);
            SetBusy(false);
        }
    }

    private async Task SpeakAssistantResponseAsync()
    {
        var text = AssistantOut.Text?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            NexusShell.Log("TTS: keine Assistant-Antwort.");
            return;
        }

        SetBusy(true);
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
                NexusShell.Log("TTS abgeschlossen.");
        }
        catch (Exception ex)
        {
            NexusShell.Log("TTS Fehler: " + ex.Message);
        }
        finally
        {
            SetBusy(false);
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
        try
        {
            _cts = new CancellationTokenSource();
            var s = _getSettings();
            NexusShell.Log($"smoke test · Provider {s.Provider}, Modell {s.Model}");
            var text = await LlmChatService.SmokeAsync(s, _cts.Token).ConfigureAwait(true);
            AssistantOut.Text = text;
            NexusShell.Log("smoke test abgeschlossen.");
        }
        catch (Exception ex)
        {
            AssistantOut.Text = "Fehler: " + ex.Message;
            NexusShell.Log("smoke test Fehler: " + ex.Message);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task RunAskAsync()
    {
        var prompt = PromptBox.Text?.Trim();
        if (string.IsNullOrEmpty(prompt))
        {
            AssistantOut.Text = "Bitte einen Prompt eingeben.";
            return;
        }

        SetBusy(true);
        try
        {
            _cts = new CancellationTokenSource();
            var s = _getSettings();
            var shots = IncludeScreenshots.IsChecked == true;
            var know = UseKnowledgeInAsk.IsChecked == true;

            RetrievalOut.Text = know
                ? KnowledgeSnippetService.BuildContext(prompt, 6000)
                : "(lokales Wissen nicht eingebunden)";

            NexusShell.Log($"ask now · screenshots={shots}, knowledge={know}");
            var text = await LlmChatService.CompleteAsync(s, prompt, shots, know, _cts.Token).ConfigureAwait(true);
            AssistantOut.Text = text;

            var tokens = ActionPlanExtractor.Extract(text);
            _planSteps = ActionPlanExtractor.ToRecipeSteps(tokens);
            _planStepIndex = 0;
            PlanPreview.Text = ActionPlanExtractor.FormatPreview(tokens);
            PlanExec.Text = $"({_planSteps.Count} Schritte erkannt — „run plan“ oder „run next step“)";

            if (s.SpeakResponses)
            {
                var ttsErr = await TextToSpeechService.SpeakAsync(text, _cts.Token).ConfigureAwait(true);
                if (!string.IsNullOrEmpty(ttsErr))
                    NexusShell.Log("TTS (auto): " + ttsErr);
            }

            NexusShell.Log("ask now abgeschlossen.");
        }
        catch (OperationCanceledException)
        {
            AssistantOut.Text += "\n\n[Abgebrochen]";
        }
        catch (Exception ex)
        {
            AssistantOut.Text = "Fehler: " + ex;
            NexusShell.Log("ask Fehler: " + ex.Message);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task ExecutePlanAsync(bool dryRun)
    {
        var steps = ActivePlanSteps();
        if (steps.Count == 0)
        {
            PlanExec.Text = "Keine Schritte — erst „ask now“ oder Plan-Zeilen einfügen.";
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
            NexusShell.Log("Alle Plan-Schritte durch.");
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
            NexusShell.Log("Kein Plan zum Speichern.");
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
            Description = "Aus Ask-Tab / Plan-Extraktion",
            Steps = steps.ToList()
        };
        RitualRecipeStore.AppendRecipe(recipe);
        NexusShell.Log($"Als Ritual gespeichert: {recipe.Name}");
    }

    private void SetBusy(bool busy)
    {
        _operationBusy = busy;
        BtnAskNow.IsEnabled = !busy;
        BtnSmoke.IsEnabled = !busy;
        BtnImportAudio.IsEnabled = !busy;
        BtnSpeak.IsEnabled = !busy;
        BtnCopyAnswer.IsEnabled = !busy;
        RefreshInputStates();
    }

    private void RefreshInputStates()
    {
        BtnPttStart.IsEnabled = !_operationBusy && !_isRecording && OperatingSystem.IsWindows();
        BtnPttStop.IsEnabled = !_operationBusy && _isRecording;
        BtnCancelRec.IsEnabled = !_operationBusy && _isRecording;
    }
}
