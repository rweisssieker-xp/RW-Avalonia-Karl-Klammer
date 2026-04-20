using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
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
    private ResponsiveBand? _ritualsLayoutBand;

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

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        LayoutUpdated += RitualsOnLayoutUpdated;
        ApplyRitualsResponsiveLayout();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        LayoutUpdated -= RitualsOnLayoutUpdated;
        base.OnDetachedFromVisualTree(e);
    }

    private void RitualsOnLayoutUpdated(object? sender, EventArgs e) => ApplyRitualsResponsiveLayout();

    private void ApplyRitualsResponsiveLayout()
    {
        var w = Bounds.Width;
        if (w <= 0)
            return;
        var band = ResponsiveLayout.GetBand(w);
        if (band == _ritualsLayoutBand)
            return;
        _ritualsLayoutBand = band;

        Grid.SetRowSpan(RitualsLibraryPane, 1);
        Grid.SetRowSpan(RitualsBuilderPane, 1);
        Grid.SetRowSpan(RitualsStepsPane, 1);

        RitualsRootGrid.ColumnDefinitions.Clear();
        RitualsRootGrid.RowDefinitions.Clear();

        switch (band)
        {
            case ResponsiveBand.Narrow:
                RitualsRootGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
                RitualsRootGrid.RowDefinitions.Add(new RowDefinition(new GridLength(200)));
                RitualsRootGrid.RowDefinitions.Add(new RowDefinition(2, GridUnitType.Star));
                RitualsRootGrid.RowDefinitions.Add(new RowDefinition(1, GridUnitType.Star));
                Grid.SetColumn(RitualsLibraryPane, 0);
                Grid.SetRow(RitualsLibraryPane, 0);
                Grid.SetColumn(RitualsBuilderPane, 0);
                Grid.SetRow(RitualsBuilderPane, 1);
                Grid.SetColumn(RitualsStepsPane, 0);
                Grid.SetRow(RitualsStepsPane, 2);
                RitualsLibraryPane.Margin = new Thickness(0, 0, 0, 8);
                RitualsBuilderPane.Margin = new Thickness(0, 0, 0, 8);
                RitualsStepsPane.Margin = new Thickness(0);
                break;
            case ResponsiveBand.Medium:
                RitualsRootGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
                RitualsRootGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
                RitualsRootGrid.RowDefinitions.Add(new RowDefinition(GridLength.Star));
                RitualsRootGrid.RowDefinitions.Add(new RowDefinition(GridLength.Star));
                Grid.SetColumn(RitualsLibraryPane, 0);
                Grid.SetRow(RitualsLibraryPane, 0);
                Grid.SetRowSpan(RitualsLibraryPane, 2);
                Grid.SetColumn(RitualsBuilderPane, 1);
                Grid.SetRow(RitualsBuilderPane, 0);
                Grid.SetColumn(RitualsStepsPane, 1);
                Grid.SetRow(RitualsStepsPane, 1);
                RitualsLibraryPane.Margin = new Thickness(0, 0, 8, 0);
                RitualsBuilderPane.Margin = new Thickness(0, 0, 0, 0);
                RitualsStepsPane.Margin = new Thickness(0, 8, 0, 0);
                break;
            default:
                RitualsRootGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
                RitualsRootGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
                RitualsRootGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
                RitualsRootGrid.RowDefinitions.Add(new RowDefinition(GridLength.Star));
                Grid.SetColumn(RitualsLibraryPane, 0);
                Grid.SetRow(RitualsLibraryPane, 0);
                Grid.SetColumn(RitualsBuilderPane, 1);
                Grid.SetRow(RitualsBuilderPane, 0);
                Grid.SetColumn(RitualsStepsPane, 2);
                Grid.SetRow(RitualsStepsPane, 0);
                RitualsLibraryPane.Margin = new Thickness(0, 0, 8, 0);
                RitualsBuilderPane.Margin = new Thickness(0, 0, 8, 0);
                RitualsStepsPane.Margin = new Thickness(0);
                break;
        }
    }

    private void PromoteFromHistory()
    {
        var entry = ActionHistoryService.GetLatestPlanRunWithSteps();
        if (entry == null)
        {
            NexusShell.Log("promote from history: no plan run with steps — run „run plan“ / ritual first.");
            return;
        }

        var recipe = new AutomationRecipe
        {
            Name = $"Promoted History {entry.UtcAt.ToLocalTime():yyyy-MM-dd HH:mm}",
            Description = "From last action-history plan_run",
            Steps = entry.Steps.Select(s => new RecipeStep
            {
                ActionType = s.ActionType,
                ActionArgument = s.ActionArgument,
                WaitMs = s.WaitMs
            }).ToList()
        };
        RitualRecipeStore.AppendRecipe(recipe);
        NexusShell.Log($"promote from history: {recipe.Name} ({recipe.Steps.Count} steps).");
        ReloadLibrary();
    }

    private void PromoteFromWatch()
    {
        var doc = WatchSessionService.LoadOrEmpty();
        if (doc.Entries.Count == 0)
        {
            NexusShell.Log("promote from watch: watch-sessions.json empty (use „watch“ mode).");
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
            Description = $"From {doc.Entries.Count} watch entries",
            Steps = steps
        };
        RitualRecipeStore.AppendRecipe(recipe);
        NexusShell.Log($"promote from watch: {recipe.Name} ({recipe.Steps.Count} steps).");
        ReloadLibrary();
    }

    private void StartTeach()
    {
        RitualsTeachSession.Start();
        NexusShell.Log("Teach: started — „capture foreground“, Live Context „run“, or adapter clicks record steps.");
    }

    private void CaptureForegroundTeachStep()
    {
        if (!RitualsTeachSession.IsActive)
        {
            NexusShell.Log("Teach: start „start teach“ first.");
            return;
        }

        if (!OperatingSystem.IsWindows())
        {
            NexusShell.Log("Teach: Windows only.");
            return;
        }

        var d = ForegroundWindowInfo.TryReadDetail();
        if (d == null)
        {
            NexusShell.Log("Teach: no foreground window.");
            return;
        }

        var fam = OperatorAdapterRegistry.ResolveFamily(d.Value.ProcessName, d.Value.Title);
        var arg = $"context|{fam}|{d.Value.ProcessName}|{d.Value.Title}";
        RitualsTeachSession.Append(new RecipeStep { ActionType = "token", ActionArgument = arg, WaitMs = 200 });
        NexusShell.Log($"Teach: step {RitualsTeachSession.BufferedCount} — {d.Value.ProcessName} ({fam}).");
    }

    private void StopTeach()
    {
        var steps = RitualsTeachSession.Stop();
        if (steps.Count == 0)
        {
            NexusShell.Log("Teach: no steps collected.");
            return;
        }

        var recipe = new AutomationRecipe
        {
            Name = $"Teach {DateTime.Now:yyyy-MM-dd HH:mm}",
            Description = "Teach mode (manually captured steps)",
            Steps = steps.ToList()
        };
        RitualRecipeStore.AppendRecipe(recipe);
        _selected = recipe;
        RitualName.Text = recipe.Name;
        RitualDesc.Text = recipe.Description;
        StepsEditor.Text = JsonSerializer.Serialize(recipe.Steps, new JsonSerializerOptions { WriteIndented = true });
        NexusShell.Log($"Teach: ritual saved — {recipe.Name} ({recipe.Steps.Count} steps).");
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
        QueueStatusLine.Text = $"Job queue: {n} pending · {AppPaths.RitualJobQueue}";
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
            NexusShell.Log("Steps JSON invalid: " + ex.Message);
            return new List<RecipeStep>();
        }
    }

    private void SaveCurrent()
    {
        var steps = ParseStepsEditor();
        var recipe = _selected ?? new AutomationRecipe();
        if (string.IsNullOrEmpty(recipe.Id))
            recipe.Id = Guid.NewGuid().ToString("n");
        recipe.Name = RitualName.Text?.Trim() ?? "Untitled";
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
        NexusShell.Log($"Ritual saved: {recipe.Name} ({recipe.Steps.Count} steps).");
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
        NexusShell.Log("Ritual deleted.");
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
        clone.Name = (clone.Name ?? "") + " (copy)";
        RitualRecipeStore.AppendRecipe(clone);
        NexusShell.Log("Ritual cloned.");
        ReloadLibrary();
    }

    private void ArchiveCurrent()
    {
        if (_selected == null)
        {
            NexusShell.Log("Archive: no ritual selected.");
            return;
        }

        SaveCurrent();
        _selected.Archived = true;
        RitualRecipeStore.Upsert(_selected);
        NexusShell.Log($"Ritual archived: {_selected.Name}");
        ReloadLibrary();
    }

    private void PublishCurrent()
    {
        if (_selected == null)
        {
            NexusShell.Log("Publish: no ritual selected.");
            return;
        }

        SaveCurrent();
        _selected.PublicationState = "published";
        RitualRecipeStore.Upsert(_selected);
        NexusShell.Log($"Flow published: {_selected.Name}");
        ReloadLibrary();
    }

    private void QueueCurrentForRun()
    {
        if (_selected == null)
        {
            NexusShell.Log("Queue: no ritual selected — save or pick one first.");
            RefreshQueueStatus();
            return;
        }

        SaveCurrent();
        if (_selected.Archived)
        {
            NexusShell.Log("Queue: archived rituals are not enqueued.");
            RefreshQueueStatus();
            return;
        }

        RitualJobQueueStore.Enqueue(_selected.Id, _selected.Name);
        var n = RitualJobQueueStore.GetPendingCount();
        NexusShell.Log($"Enqueued: {_selected.Name} (pending: {n}).");
        RefreshQueueStatus();
    }

    private async Task ApproveNextJobAsync()
    {
        try
        {
            if (!RitualJobQueueStore.TryDequeuePending(out var job) || job == null)
            {
                NexusShell.Log("Job queue: no pending jobs.");
                return;
            }

            var all = RitualRecipeStore.LoadAll();
            var recipe = all.FirstOrDefault(r => string.Equals(r.Id, job.RecipeId, StringComparison.Ordinal));
            if (recipe == null)
            {
                RitualJobQueueStore.RecordHistory(job, "failed", "Recipe not found");
                NexusShell.Log($"Job cancelled: recipe {job.RecipeId} missing.");
                return;
            }

            if (recipe.Archived)
            {
                RitualJobQueueStore.RecordHistory(job, "failed", "Recipe archived");
                NexusShell.Log($"Job skipped: {recipe.Name} is archived.");
                return;
            }

            _runCts?.Cancel();
            _runCts = new CancellationTokenSource();
            NexusShell.Log($"Job approved — run: {recipe.Name} …");
            try
            {
                await SimplePlanSimulator
                    .RunAsync(recipe.Steps, false, NexusContext.GetSettings(), _runCts.Token)
                    .ConfigureAwait(true);
                RitualJobQueueStore.RecordHistory(job, "completed", null);
                NexusShell.Log($"Job completed: {recipe.Name}");
            }
            catch (OperationCanceledException)
            {
                RitualJobQueueStore.RecordHistory(job, "cancelled", null);
                NexusShell.Log("Job cancelled.");
            }
            catch (Exception ex)
            {
                RitualJobQueueStore.RecordHistory(job, "failed", ex.Message);
                NexusShell.Log("Job failed: " + ex.Message);
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
            NexusShell.Log("AI short title: enter description or steps.");
            return;
        }

        var settings = NexusContext.GetSettings();
        if (!DotEnvStore.HasProviderKey(settings.Provider))
        {
            NexusShell.Log("AI short title: API key missing for provider (.env).");
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
            "Description:\n" + (string.IsNullOrWhiteSpace(desc) ? "(none)" : desc) +
            "\n\nSteps (excerpt):\n" + (stepSummary.Length > 0 ? stepSummary : "(none)");

        BtnAiSuggestName.IsEnabled = false;
        CompanionHub.Publish(CompanionVisualState.Thinking);
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
            const string sys =
                "Suggest exactly one short factual ritual name in English (at most 6 words). " +
                "Output only the name, no quotes, no extra text.";
            var name = await LlmChatService.CompleteUtilityAsync(settings, sys, userBlob, cts.Token).ConfigureAwait(true);
            if (name.StartsWith("Missing ", StringComparison.Ordinal) ||
                name.StartsWith("Fehlt ", StringComparison.Ordinal) ||
                name.StartsWith("Unknown provider", StringComparison.Ordinal) ||
                name.StartsWith("Unbekannter Provider", StringComparison.Ordinal) ||
                name.StartsWith("Anthropic HTTP", StringComparison.Ordinal) ||
                name.StartsWith("OpenAI", StringComparison.Ordinal))
            {
                NexusShell.Log("AI short title: " + name);
                return;
            }

            var oneLine = name.Replace('\r', ' ').Replace('\n', ' ').Trim();
            if (oneLine.Length > 120)
                oneLine = oneLine[..120].TrimEnd();
            RitualName.Text = oneLine;
            NexusShell.Log("AI short title applied.");
        }
        catch (Exception ex)
        {
            NexusShell.Log("AI short title: " + ex.Message);
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
            NexusShell.Log("No steps to run.");
            return;
        }

        if (_selected != null
            && !dryRun
            && string.Equals(_selected.PublicationState, "published", StringComparison.OrdinalIgnoreCase)
            && string.Equals(_selected.ApprovalMode, "manual", StringComparison.OrdinalIgnoreCase))
        {
            NexusShell.Log(
                "Direct run blocked: ritual is „published“ with approval „manual“ — use „queue for run“ and „approve next job“.");
            return;
        }

        _runCts = new CancellationTokenSource();
        _stepCursor = 0;
        NexusShell.Log(dryRun ? "Dry-run starting …" : "Plan run starting (simulated) …");
        var log = await SimplePlanSimulator.RunAsync(steps, dryRun, NexusContext.GetSettings(), _runCts.Token)
            .ConfigureAwait(true);
        NexusShell.Log(dryRun ? "Dry-run done." : "Run done.");
        _ = log;
    }

    private async Task RunNextStepAsync()
    {
        var steps = _selected != null ? _selected.Steps : ParseStepsEditor();
        if (_stepCursor >= steps.Count)
        {
            NexusShell.Log("No further step.");
            return;
        }

        if (_selected != null
            && string.Equals(_selected.PublicationState, "published", StringComparison.OrdinalIgnoreCase)
            && string.Equals(_selected.ApprovalMode, "manual", StringComparison.OrdinalIgnoreCase))
        {
            NexusShell.Log(
                "Step blocked: „published“ + „manual“ — use queue + approve.");
            return;
        }

        _runCts ??= new CancellationTokenSource();
        var one = new List<RecipeStep> { steps[_stepCursor] };
        NexusShell.Log($"Next step {_stepCursor + 1}/{steps.Count}");
        await SimplePlanSimulator.RunAsync(one, false, NexusContext.GetSettings(), _runCts.Token)
            .ConfigureAwait(true);
        _stepCursor++;
    }
}
