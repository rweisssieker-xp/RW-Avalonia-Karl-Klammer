using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using CarolusNexus.Models;
using CarolusNexus.Services;
using CarolusNexus;

namespace CarolusNexus.Views;

public partial class RitualsTab : UserControl
{
    private List<AutomationRecipe> _all = new();
    private AutomationRecipe? _selected;
    private int _stepCursor;
    private CancellationTokenSource? _runCts;

    public RitualsTab()
    {
        InitializeComponent();
        ApprovalModeBox.ItemsSource = new[] { "manual", "auto" };
        RiskLevelBox.ItemsSource = new[] { "low", "medium", "high" };
        LibFilter.TextChanged += (_, _) => FilterList();
        RitualList.SelectionChanged += OnRitualSelected;

        BtnSaveRitual.Click += (_, _) => SaveCurrent();
        BtnDeleteRitual.Click += (_, _) => DeleteCurrent();
        BtnClone.Click += (_, _) => CloneCurrent();
        BtnArchive.Click += (_, _) => ArchiveCurrent();
        BtnPublish.Click += (_, _) => PublishCurrent();
        BtnQueue.Click += (_, _) => QueueCurrentForRun();
        BtnApproveJob.Click += async (_, _) => await ApproveNextJobAsync().ConfigureAwait(true);
        BtnDryRun.Click += async (_, _) => await RunSelectedAsync(true);
        BtnRunRitual.Click += async (_, _) => await RunSelectedAsync(false);
        BtnRunNextStep.Click += async (_, _) => await RunNextStepAsync();
        BtnResume.Click += async (_, _) => await RunSelectedAsync(false);
        BtnPromoteHist.Click += (_, _) => PromoteFromHistory();
        BtnPromoteWatch.Click += (_, _) => PromoteFromWatch();
        BtnTeachStart.Click += (_, _) => StartTeach();
        BtnTeachCapture.Click += (_, _) => CaptureForegroundTeachStep();
        BtnTeachStop.Click += (_, _) => StopTeach();
        BtnAiSuggestName.Click += async (_, _) => await SuggestRitualNameWithAiAsync().ConfigureAwait(true);

        RitualRecipeStore.Saved += OnRecipesSaved;
        Unloaded += (_, _) => RitualRecipeStore.Saved -= OnRecipesSaved;

        ReloadLibrary();
    }

    private void PromoteFromHistory()
    {
        var entry = ActionHistoryService.GetLatestPlanRunWithSteps();
        if (entry == null)
        {
            NexusShell.Log("promote from history: kein Plan-Lauf mit Schritten — zuerst „run plan“ / Ritual ausführen.");
            return;
        }

        var recipe = new AutomationRecipe
        {
            Name = $"Promoted History {entry.UtcAt.ToLocalTime():yyyy-MM-dd HH:mm}",
            Description = "Aus letztem action-history plan_run",
            Steps = entry.Steps.Select(s => new RecipeStep
            {
                ActionType = s.ActionType,
                ActionArgument = s.ActionArgument,
                WaitMs = s.WaitMs
            }).ToList()
        };
        RitualRecipeStore.AppendRecipe(recipe);
        NexusShell.Log($"promote from history: {recipe.Name} ({recipe.Steps.Count} Schritte).");
        ReloadLibrary();
    }

    private void PromoteFromWatch()
    {
        var doc = WatchSessionService.LoadOrEmpty();
        if (doc.Entries.Count == 0)
        {
            NexusShell.Log("promote from watch: watch-sessions.json leer (Modus „watch“ nutzen).");
            return;
        }

        var steps = doc.Entries
            .Select(e => new RecipeStep
            {
                ActionType = "token",
                ActionArgument = string.IsNullOrWhiteSpace(e.ScreenHash)
                    ? $"watch|{e.Note}"
                    : $"watch|{e.Note} [screen:{e.ScreenHash}]",
                WaitMs = 0
            })
            .ToList();
        var recipe = new AutomationRecipe
        {
            Name = $"Promoted Watch {DateTime.Now:yyyy-MM-dd HH:mm}",
            Description = $"Aus {doc.Entries.Count} Watch-Einträgen",
            Steps = steps
        };
        RitualRecipeStore.AppendRecipe(recipe);
        NexusShell.Log($"promote from watch: {recipe.Name} ({recipe.Steps.Count} Schritte).");
        ReloadLibrary();
    }

    private void StartTeach()
    {
        RitualsTeachSession.Start();
        NexusShell.Log("Teach: gestartet — „capture foreground“, Live-Context „run“ oder Adapter-Klicks erfassen Schritte.");
    }

    private void CaptureForegroundTeachStep()
    {
        if (!RitualsTeachSession.IsActive)
        {
            NexusShell.Log("Teach: zuerst „start teach“.");
            return;
        }

        if (!OperatingSystem.IsWindows())
        {
            NexusShell.Log("Teach: nur unter Windows.");
            return;
        }

        var d = ForegroundWindowInfo.TryReadDetail();
        if (d == null)
        {
            NexusShell.Log("Teach: kein Vordergrundfenster.");
            return;
        }

        var fam = OperatorAdapterRegistry.ResolveFamily(d.Value.ProcessName, d.Value.Title);
        var arg = $"context|{fam}|{d.Value.ProcessName}|{d.Value.Title}";
        RitualsTeachSession.Append(new RecipeStep { ActionType = "token", ActionArgument = arg, WaitMs = 200 });
        NexusShell.Log($"Teach: Schritt {RitualsTeachSession.BufferedCount} — {d.Value.ProcessName} ({fam}).");
    }

    private void StopTeach()
    {
        var steps = RitualsTeachSession.Stop();
        if (steps.Count == 0)
        {
            NexusShell.Log("Teach: keine Schritte gesammelt.");
            return;
        }

        var recipe = new AutomationRecipe
        {
            Name = $"Teach {DateTime.Now:yyyy-MM-dd HH:mm}",
            Description = "Teach-Modus (manuell erfasste Schritte)",
            Steps = steps.ToList()
        };
        RitualRecipeStore.AppendRecipe(recipe);
        _selected = recipe;
        RitualName.Text = recipe.Name;
        RitualDesc.Text = recipe.Description;
        StepsEditor.Text = JsonSerializer.Serialize(recipe.Steps, new JsonSerializerOptions { WriteIndented = true });
        NexusShell.Log($"Teach: Ritual gespeichert — {recipe.Name} ({recipe.Steps.Count} Schritte).");
        ReloadLibrary();
    }

    private void OnRecipesSaved() => ReloadLibrary();

    public void ReloadLibrary()
    {
        _all = RitualRecipeStore.LoadAll();
        FilterList();
        if (_selected != null)
        {
            var match = _all.FirstOrDefault(r => r.Id == _selected.Id);
            if (match != null)
            {
                RitualList.SelectedItem = match;
                ApplySelection(match);
            }
        }

        RefreshQueueStatus();
    }

    private void RefreshQueueStatus()
    {
        var n = RitualJobQueueStore.GetPendingCount();
        QueueStatusLine.Text = $"Job-Warteschlange: {n} ausstehend · {AppPaths.RitualJobQueue}";
        JobQueueDetail.Text = RitualJobQueueStore.FormatDashboardSummary();
    }

    private void FilterList()
    {
        var q = LibFilter.Text?.Trim() ?? "";
        var src = string.IsNullOrEmpty(q)
            ? _all
            : _all.Where(r =>
                r.Name.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                r.Description.Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();
        RitualList.ItemsSource = src;
    }

    private void OnRitualSelected(object? s, SelectionChangedEventArgs e)
    {
        if (RitualList.SelectedItem is AutomationRecipe r)
        {
            _selected = r;
            ApplySelection(r);
        }
    }

    private void ApplySelection(AutomationRecipe r)
    {
        RitualName.Text = r.Name;
        RitualDesc.Text = r.Description;
        ApprovalModeBox.SelectedItem = string.IsNullOrWhiteSpace(r.ApprovalMode) ? "manual" : r.ApprovalMode;
        RiskLevelBox.SelectedItem = string.IsNullOrWhiteSpace(r.RiskLevel) ? "medium" : r.RiskLevel;
        AdapterAffinityBox.Text = r.AdapterAffinity ?? "";
        ConfidenceSourceBox.Text = r.ConfidenceSource ?? "";
        MaxAutonomyStepsBox.Text = r.MaxAutonomySteps > 0 ? r.MaxAutonomySteps.ToString() : "";
        StepsEditor.Text = JsonSerializer.Serialize(r.Steps, new JsonSerializerOptions { WriteIndented = true });
        _stepCursor = 0;
    }

    private List<RecipeStep> ParseStepsEditor()
    {
        var raw = StepsEditor.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(raw))
            return new List<RecipeStep>();
        try
        {
            return JsonSerializer.Deserialize<List<RecipeStep>>(raw) ?? new List<RecipeStep>();
        }
        catch (Exception ex)
        {
            NexusShell.Log("Steps JSON ungültig: " + ex.Message);
            return new List<RecipeStep>();
        }
    }

    private void SaveCurrent()
    {
        var steps = ParseStepsEditor();
        var recipe = _selected ?? new AutomationRecipe();
        if (string.IsNullOrEmpty(recipe.Id))
            recipe.Id = Guid.NewGuid().ToString("n");
        recipe.Name = RitualName.Text?.Trim() ?? "Unbenannt";
        recipe.Description = RitualDesc.Text ?? "";
        recipe.ApprovalMode = ApprovalModeBox.SelectedItem?.ToString()?.Trim() ?? "manual";
        recipe.RiskLevel = RiskLevelBox.SelectedItem?.ToString()?.Trim() ?? "medium";
        recipe.AdapterAffinity = AdapterAffinityBox.Text?.Trim() ?? "";
        recipe.ConfidenceSource = ConfidenceSourceBox.Text?.Trim() ?? "";
        if (int.TryParse(MaxAutonomyStepsBox.Text?.Trim(), out var maxA) && maxA > 0)
            recipe.MaxAutonomySteps = maxA;
        else
            recipe.MaxAutonomySteps = 0;
        recipe.Steps = steps;
        RitualRecipeStore.Upsert(recipe);
        _selected = recipe;
        NexusShell.Log($"Ritual gespeichert: {recipe.Name} ({recipe.Steps.Count} Schritte).");
        ReloadLibrary();
    }

    private void DeleteCurrent()
    {
        if (_selected == null)
            return;
        RitualRecipeStore.DeleteById(_selected.Id);
        _selected = null;
        RitualName.Text = "";
        RitualDesc.Text = "";
        ApprovalModeBox.SelectedItem = "manual";
        RiskLevelBox.SelectedItem = "medium";
        AdapterAffinityBox.Text = "";
        ConfidenceSourceBox.Text = "";
        MaxAutonomyStepsBox.Text = "";
        StepsEditor.Text = "[]";
        NexusShell.Log("Ritual gelöscht.");
        ReloadLibrary();
    }

    private void CloneCurrent()
    {
        if (_selected == null)
            return;
        var json = JsonSerializer.Serialize(_selected);
        var clone = JsonSerializer.Deserialize<AutomationRecipe>(json);
        if (clone == null)
            return;
        clone.Id = Guid.NewGuid().ToString("n");
        clone.Name = (clone.Name ?? "") + " (Kopie)";
        RitualRecipeStore.AppendRecipe(clone);
        NexusShell.Log("Ritual geklont.");
        ReloadLibrary();
    }

    private void ArchiveCurrent()
    {
        if (_selected == null)
        {
            NexusShell.Log("Archiv: kein Ritual gewählt.");
            return;
        }

        SaveCurrent();
        _selected.Archived = true;
        RitualRecipeStore.Upsert(_selected);
        NexusShell.Log($"Ritual archiviert: {_selected.Name}");
        ReloadLibrary();
    }

    private void PublishCurrent()
    {
        if (_selected == null)
        {
            NexusShell.Log("Publish: kein Ritual gewählt.");
            return;
        }

        SaveCurrent();
        _selected.PublicationState = "published";
        RitualRecipeStore.Upsert(_selected);
        NexusShell.Log($"Flow veröffentlicht (published): {_selected.Name}");
        ReloadLibrary();
    }

    private void QueueCurrentForRun()
    {
        if (_selected == null)
        {
            NexusShell.Log("Warteschlange: kein Ritual gewählt — zuerst speichern oder auswählen.");
            RefreshQueueStatus();
            return;
        }

        SaveCurrent();
        if (_selected.Archived)
        {
            NexusShell.Log("Warteschlange: archivierte Rituale werden nicht eingereiht.");
            RefreshQueueStatus();
            return;
        }

        RitualJobQueueStore.Enqueue(_selected.Id, _selected.Name);
        var n = RitualJobQueueStore.GetPendingCount();
        NexusShell.Log($"Eingereiht: {_selected.Name} (ausstehend: {n}).");
        RefreshQueueStatus();
    }

    private async Task ApproveNextJobAsync()
    {
        try
        {
            if (!RitualJobQueueStore.TryDequeuePending(out var job) || job == null)
            {
                NexusShell.Log("Job-Warteschlange: keine ausstehenden Jobs.");
                return;
            }

            var all = RitualRecipeStore.LoadAll();
            var recipe = all.FirstOrDefault(r => string.Equals(r.Id, job.RecipeId, StringComparison.Ordinal));
            if (recipe == null)
            {
                RitualJobQueueStore.RecordHistory(job, "failed", "Rezept nicht gefunden");
                NexusShell.Log($"Job abgebrochen: Rezept {job.RecipeId} fehlt.");
                return;
            }

            if (recipe.Archived)
            {
                RitualJobQueueStore.RecordHistory(job, "failed", "Rezept archiviert");
                NexusShell.Log($"Job übersprungen: {recipe.Name} ist archiviert.");
                return;
            }

            _runCts?.Cancel();
            _runCts = new CancellationTokenSource();
            NexusShell.Log($"Job genehmigt — Lauf: {recipe.Name} …");
            try
            {
                await SimplePlanSimulator
                    .RunAsync(recipe.Steps, false, NexusContext.GetSettings(), _runCts.Token)
                    .ConfigureAwait(true);
                RitualJobQueueStore.RecordHistory(job, "completed", null);
                NexusShell.Log($"Job abgeschlossen: {recipe.Name}");
            }
            catch (OperationCanceledException)
            {
                RitualJobQueueStore.RecordHistory(job, "cancelled", null);
                NexusShell.Log("Job abgebrochen.");
            }
            catch (Exception ex)
            {
                RitualJobQueueStore.RecordHistory(job, "failed", ex.Message);
                NexusShell.Log("Job fehlgeschlagen: " + ex.Message);
            }
        }
        finally
        {
            RefreshQueueStatus();
        }
    }

    private async Task SuggestRitualNameWithAiAsync()
    {
        var desc = RitualDesc.Text?.Trim() ?? "";
        var steps = ParseStepsEditor();
        if (string.IsNullOrWhiteSpace(desc) && steps.Count == 0)
        {
            NexusShell.Log("KI-Kurztitel: Beschreibung oder Schritte eingeben.");
            return;
        }

        var settings = NexusContext.GetSettings();
        if (!DotEnvStore.HasProviderKey(settings.Provider))
        {
            NexusShell.Log("KI-Kurztitel: API-Key für Provider fehlt (.env).");
            return;
        }

        var stepSummary = string.Join("\n", steps.Take(6).Select(s =>
        {
            var arg = s.ActionArgument ?? "";
            if (arg.Length > 160)
                arg = arg[..160] + "…";
            return "- " + s.ActionType + ": " + arg;
        }));

        var userBlob =
            "Beschreibung:\n" + (string.IsNullOrWhiteSpace(desc) ? "(keine)" : desc) +
            "\n\nSchritte (Auszug):\n" + (stepSummary.Length > 0 ? stepSummary : "(keine)");

        BtnAiSuggestName.IsEnabled = false;
        CompanionHub.Publish(CompanionVisualState.Thinking);
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
            const string sys =
                "Vorschlage genau einen kurzen, sachlichen Ritual-Namen auf Deutsch (maximal 6 Wörter). " +
                "Nur den Namen ausgeben, keine Anführungszeichen, kein Zusatz.";
            var name = await LlmChatService.CompleteUtilityAsync(settings, sys, userBlob, cts.Token).ConfigureAwait(true);
            if (name.StartsWith("Fehlt ", StringComparison.Ordinal) ||
                name.StartsWith("Unbekannter Provider", StringComparison.Ordinal) ||
                name.StartsWith("Anthropic HTTP", StringComparison.Ordinal) ||
                name.StartsWith("OpenAI", StringComparison.Ordinal))
            {
                NexusShell.Log("KI-Kurztitel: " + name);
                return;
            }

            var oneLine = name.Replace('\r', ' ').Replace('\n', ' ').Trim();
            if (oneLine.Length > 120)
                oneLine = oneLine[..120].TrimEnd();
            RitualName.Text = oneLine;
            NexusShell.Log("KI-Kurztitel übernommen.");
        }
        catch (Exception ex)
        {
            NexusShell.Log("KI-Kurztitel: " + ex.Message);
            CompanionHub.Publish(CompanionVisualState.Error);
        }
        finally
        {
            BtnAiSuggestName.IsEnabled = true;
            CompanionHub.Publish(CompanionVisualState.Ready);
        }
    }

    private async Task RunSelectedAsync(bool dryRun)
    {
        var steps = _selected != null ? _selected.Steps : ParseStepsEditor();
        if (steps.Count == 0)
        {
            NexusShell.Log("Keine Schritte zum Ausführen.");
            return;
        }

        if (_selected != null
            && !dryRun
            && string.Equals(_selected.PublicationState, "published", StringComparison.OrdinalIgnoreCase)
            && string.Equals(_selected.ApprovalMode, "manual", StringComparison.OrdinalIgnoreCase))
        {
            NexusShell.Log(
                "Direktausführung blockiert: Ritual ist „published“ mit Freigabe „manual“ — bitte „queue for run“ und „approve next job“ nutzen.");
            return;
        }

        _runCts = new CancellationTokenSource();
        _stepCursor = 0;
        NexusShell.Log(dryRun ? "Dry-run startet …" : "Plan-Lauf startet (simuliert) …");
        var log = await SimplePlanSimulator.RunAsync(steps, dryRun, NexusContext.GetSettings(), _runCts.Token)
            .ConfigureAwait(true);
        NexusShell.Log(dryRun ? "Dry-run fertig." : "Lauf fertig.");
        _ = log;
    }

    private async Task RunNextStepAsync()
    {
        var steps = _selected != null ? _selected.Steps : ParseStepsEditor();
        if (_stepCursor >= steps.Count)
        {
            NexusShell.Log("Kein weiterer Schritt.");
            return;
        }

        if (_selected != null
            && string.Equals(_selected.PublicationState, "published", StringComparison.OrdinalIgnoreCase)
            && string.Equals(_selected.ApprovalMode, "manual", StringComparison.OrdinalIgnoreCase))
        {
            NexusShell.Log(
                "Schritt blockiert: „published“ + „manual“ — Warteschlange + Approve nutzen.");
            return;
        }

        _runCts ??= new CancellationTokenSource();
        var one = new List<RecipeStep> { steps[_stepCursor] };
        NexusShell.Log($"Nächster Schritt {_stepCursor + 1}/{steps.Count}");
        await SimplePlanSimulator.RunAsync(one, false, NexusContext.GetSettings(), _runCts.Token)
            .ConfigureAwait(true);
        _stepCursor++;
    }
}
