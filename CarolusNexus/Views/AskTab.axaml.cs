using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using CarolusNexus.Models;
using CarolusNexus.Services;

namespace CarolusNexus.Views;

public partial class AskTab : UserControl
{
    private Func<NexusSettings> _getSettings = () => new();
    private CancellationTokenSource? _cts;
    private List<RecipeStep> _planSteps = new();
    private int _planStepIndex;

    public AskTab()
    {
        InitializeComponent();
        Wire();
    }

    public void SetSettingsProvider(Func<NexusSettings> getSettings) => _getSettings = getSettings;

    private void Wire()
    {
        BtnAskNow.Click += async (_, _) => await RunAskAsync();
        BtnSmoke.Click += async (_, _) => await RunSmokeAsync();
        BtnImportAudio.Click += (_, _) => Stub("import audio + transcribe");
        BtnPttStart.Click += (_, _) => Stub("start push-to-talk");
        BtnPttStop.Click += (_, _) => Stub("stop + ask");
        BtnCancelRec.Click += (_, _) => Stub("cancel recording");
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
        BtnSpeak.Click += (_, _) => Stub("speak response");
    }

    public async Task RunAskFromHotkeyAsync()
    {
        await RunAskAsync();
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
                ? KnowledgeSnippetService.BuildContext(6000)
                : "(lokales Wissen nicht eingebunden)";

            NexusShell.Log($"ask now · screenshots={shots}, knowledge={know}");
            var text = await LlmChatService.CompleteAsync(s, prompt, shots, know, _cts.Token);
            AssistantOut.Text = text;

            var tokens = ActionPlanExtractor.Extract(text);
            _planSteps = ActionPlanExtractor.ToRecipeSteps(tokens);
            _planStepIndex = 0;
            PlanPreview.Text = ActionPlanExtractor.FormatPreview(tokens);
            PlanExec.Text = $"({_planSteps.Count} Schritte erkannt — „run plan“ oder „run next step“)";

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
        PlanExec.Text = await SimplePlanSimulator.RunAsync(steps, dryRun, _getSettings(), _cts.Token);
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
        var line = await SimplePlanSimulator.RunAsync(slice, false, _getSettings(), _cts.Token);
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
        BtnAskNow.IsEnabled = !busy;
        BtnSmoke.IsEnabled = !busy;
    }

    private void Stub(string action)
    {
        NexusShell.LogStub(action);
        AssistantOut.Text =
            $"(Stub) Anfrage: „{PromptBox.Text?.Trim()}“\r\nAktion: {action}\r\n— noch nicht angebunden.";
    }
}
