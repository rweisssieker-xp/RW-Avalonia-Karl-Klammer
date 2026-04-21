using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CarolusNexus;
using CarolusNexus.Models;
using CarolusNexus.Services;
using CarolusNexus_WinUI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace CarolusNexus_WinUI.Pages;

/// <summary>Parity with Avalonia <c>RitualsTab</c> — library, governance fields, queue, teach, promote, run.</summary>
public sealed class RitualsShellPage : Page
{
    private List<AutomationRecipe> _all = new();
    private AutomationRecipe? _selected;
    private int _stepCursor;
    private CancellationTokenSource? _runCts;
    private ResponsiveBand? _ritualsLayoutBand;

    private readonly Grid _root = new() { Margin = new Thickness(20, 16, 20, 16) };
    private readonly StackPanel _libraryPane = new() { Spacing = 8 };
    private readonly StackPanel _builderPane = new() { Spacing = 8 };
    private readonly StackPanel _stepsPane = new() { Spacing = 8 };

    private readonly TextBox _libFilter = new() { Header = "Filter library", PlaceholderText = "Name or description…" };
    private readonly StackPanel _recipeButtons = new() { Spacing = 4 };
    private readonly TextBox _ritualName = new() { Header = "Name" };
    private readonly TextBox _ritualCategory = new() { Header = "Category" };
    private readonly TextBox _ritualDesc = new() { Header = "Description", AcceptsReturn = true, MinHeight = 56, TextWrapping = TextWrapping.Wrap };
    private readonly ComboBox _approvalMode = new() { Header = "Approval mode" };
    private readonly ComboBox _riskLevel = new() { Header = "Risk level" };
    private readonly TextBox _adapterAffinity = new() { Header = "Adapter affinity" };
    private readonly TextBox _confidenceSource = new() { Header = "Confidence source" };
    private readonly TextBox _maxAutonomySteps = new() { Header = "Max autonomy steps (0 = unlimited)" };
    private readonly TextBox _stepsEditor = new()
    {
        Header = "Steps (JSON)",
        AcceptsReturn = true,
        MinHeight = 200,
        TextWrapping = TextWrapping.Wrap,
        FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas")
    };
    private readonly TextBlock _queueStatusLine = new() { TextWrapping = TextWrapping.Wrap, FontSize = 12 };
    private readonly TextBox _jobQueueDetail = new()
    {
        IsReadOnly = true,
        AcceptsReturn = true,
        MinHeight = 72,
        TextWrapping = TextWrapping.Wrap,
        FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas"),
        FontSize = 11
    };

    private readonly Button _btnAiSuggest = new() { Content = "AI short title" };
    private readonly Button _btnPromoteWatch = new() { Content = "Promote from watch" };

    public RitualsShellPage()
    {
        WinUiFluentChrome.StyleActionButton(_btnAiSuggest, compact: true);
        WinUiFluentChrome.StyleActionButton(_btnPromoteWatch);
        foreach (var x in new[] { "manual", "auto" })
            _approvalMode.Items.Add(x);
        foreach (var x in new[] { "low", "medium", "high" })
            _riskLevel.Items.Add(x);
        _approvalMode.SelectedIndex = 0;
        _riskLevel.SelectedIndex = 1;

        _libFilter.TextChanged += (_, _) => FilterList();

        _libraryPane.Children.Add(WinUiFluentChrome.PageTitle("Operator flows"));
        var flowHint = new TextBlock
        {
            Text = "Library, governance, queue, teach mode — parity with Avalonia Rituals.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = WinUiFluentChrome.SecondaryTextBrush
        };
        WinUiFluentChrome.ApplyCaptionTextStyle(flowHint);
        _libraryPane.Children.Add(flowHint);
        _libraryPane.Children.Add(WinUiFluentChrome.ColumnCaption("Flow library"));
        _libraryPane.Children.Add(_libFilter);
        _libraryPane.Children.Add(new ScrollViewer { Content = _recipeButtons, MaxHeight = 420 });

        var nameRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        nameRow.Children.Add(new StackPanel { Width = 420, Children = { _ritualName } });
        nameRow.Children.Add(_btnAiSuggest);
        _btnAiSuggest.Click += async (_, _) => await SuggestRitualNameWithAiAsync();

        _builderPane.Children.Add(nameRow);
        _builderPane.Children.Add(_ritualCategory);
        _builderPane.Children.Add(_ritualDesc);
        var gov = new Grid();
        gov.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        gov.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        gov.Children.Add(_approvalMode);
        Grid.SetColumn(_riskLevel, 1);
        gov.Children.Add(_riskLevel);
        _builderPane.Children.Add(gov);
        _builderPane.Children.Add(_adapterAffinity);
        _builderPane.Children.Add(_confidenceSource);
        _builderPane.Children.Add(_maxAutonomySteps);

        _builderPane.Children.Add(MkRow(
            MkBtn("Save", async (_, _) => await TrySaveCurrentAsync()),
            MkBtn("Delete", (_, _) => DeleteCurrent()),
            MkBtn("Clone", (_, _) => CloneCurrent()),
            MkBtn("Archive", async (_, _) => await ArchiveCurrentAsync()),
            MkBtn("Publish", async (_, _) => await PublishCurrentAsync())));
        _builderPane.Children.Add(MkRow(
            MkBtn("Queue for run", async (_, _) => await QueueCurrentForRunAsync()),
            MkBtn("Approve next job", async (_, _) => await ApproveNextJobAsync())));
        _builderPane.Children.Add(MkRow(
            MkBtn("Dry run", async (_, _) => await RunSelectedAsync(true)),
            MkBtn("Run", async (_, _) => await RunSelectedAsync(false), accent: true),
            MkBtn("Next step", async (_, _) => await RunNextStepAsync()),
            MkBtn("Resume", async (_, _) => await RunSelectedAsync(false))));
        _builderPane.Children.Add(MkRow(
            MkBtn("Promote from history", (_, _) => PromoteFromHistory()),
            _btnPromoteWatch));
        _btnPromoteWatch.Click += async (_, _) => await PromoteFromWatchAsync();
        _builderPane.Children.Add(MkRow(
            MkBtn("Teach: start", (_, _) => StartTeach()),
            MkBtn("Teach: capture FG", (_, _) => CaptureForegroundTeachStep()),
            MkBtn("Teach: stop", (_, _) => StopTeach())));

        _queueStatusLine.Foreground = WinUiFluentChrome.SecondaryTextBrush;
        WinUiFluentChrome.ApplyCaptionTextStyle(_queueStatusLine);
        _builderPane.Children.Add(_queueStatusLine);
        _builderPane.Children.Add(_jobQueueDetail);

        _stepsPane.Children.Add(WinUiFluentChrome.ColumnCaption("Steps editor"));
        _stepsPane.Children.Add(_stepsEditor);

        Content = _root;
        Loaded += OnPageLoaded;
        Unloaded += OnPageUnloaded;
        SizeChanged += (_, e) => ApplyRitualsLayout(e.NewSize.Width);
    }

    private void OnPageLoaded(object sender, RoutedEventArgs e)
    {
        RitualRecipeStore.Saved += OnRecipesSaved;
        ReloadLibrary();
        ApplyRitualsLayout(ActualWidth);
    }

    private void OnPageUnloaded(object sender, RoutedEventArgs e) => RitualRecipeStore.Saved -= OnRecipesSaved;

    private void OnRecipesSaved() => ReloadLibrary();

    private static StackPanel MkRow(params UIElement[] children)
    {
        var sp = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        foreach (var c in children)
            sp.Children.Add(c);
        return sp;
    }

    private static Button MkBtn(string content, RoutedEventHandler click, bool accent = false)
    {
        var b = new Button { Content = content };
        WinUiFluentChrome.StyleActionButton(b, accent);
        b.Click += click;
        return b;
    }

    private void ApplyRitualsLayout(double w)
    {
        if (w <= 0)
            return;
        var band = ResponsiveLayout.GetBand(w);
        if (band == _ritualsLayoutBand && _root.Children.Count > 0)
            return;
        _ritualsLayoutBand = band;

        _root.Children.Clear();
        _root.ColumnDefinitions.Clear();
        _root.RowDefinitions.Clear();

        switch (band)
        {
            case ResponsiveBand.Narrow:
                _root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(200) });
                _root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(2, GridUnitType.Star) });
                _root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                _root.Children.Add(_libraryPane);
                _root.Children.Add(_builderPane);
                _root.Children.Add(_stepsPane);
                Grid.SetRow(_libraryPane, 0);
                Grid.SetRow(_builderPane, 1);
                Grid.SetRow(_stepsPane, 2);
                _libraryPane.Margin = new Thickness(0, 0, 0, 8);
                _builderPane.Margin = new Thickness(0, 0, 0, 8);
                break;
            case ResponsiveBand.Medium:
                _root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                _root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                _root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                _root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                _root.Children.Add(_libraryPane);
                _root.Children.Add(_builderPane);
                _root.Children.Add(_stepsPane);
                Grid.SetColumn(_libraryPane, 0);
                Grid.SetRow(_libraryPane, 0);
                Grid.SetRowSpan(_libraryPane, 2);
                Grid.SetColumn(_builderPane, 1);
                Grid.SetRow(_builderPane, 0);
                Grid.SetColumn(_stepsPane, 1);
                Grid.SetRow(_stepsPane, 1);
                _libraryPane.Margin = new Thickness(0, 0, 8, 0);
                _stepsPane.Margin = new Thickness(0, 8, 0, 0);
                break;
            default:
                _root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                _root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                _root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                _root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                _root.Children.Add(_libraryPane);
                _root.Children.Add(_builderPane);
                _root.Children.Add(_stepsPane);
                Grid.SetColumn(_libraryPane, 0);
                Grid.SetColumn(_builderPane, 1);
                Grid.SetColumn(_stepsPane, 2);
                _libraryPane.Margin = new Thickness(0, 0, 8, 0);
                _builderPane.Margin = new Thickness(0, 0, 8, 0);
                break;
        }
    }

    public void ReloadLibrary()
    {
        _all = RitualRecipeStore.LoadAll();
        FilterList();
        if (_selected != null)
        {
            var match = _all.FirstOrDefault(r => r.Id == _selected.Id);
            if (match != null)
            {
                _selected = match;
                ApplySelection(match);
            }
        }

        RefreshQueueStatus();
    }

    private void RefreshQueueStatus()
    {
        var n = RitualJobQueueStore.GetPendingCount();
        _queueStatusLine.Text = $"Job queue: {n} pending · {AppPaths.RitualJobQueue}";
        _jobQueueDetail.Text = RitualJobQueueStore.FormatDashboardSummary();
    }

    private void FilterList()
    {
        var q = _libFilter.Text?.Trim() ?? "";
        var src = string.IsNullOrEmpty(q)
            ? _all
            : _all.Where(r =>
                r.Name.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                (r.Description ?? "").Contains(q, StringComparison.OrdinalIgnoreCase) ||
                (r.Category ?? "").Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();

        _recipeButtons.Children.Clear();
        foreach (var r in src.OrderBy(x => x.Name))
        {
            var copy = r;
            var b = new Button
            {
                Content = copy.ListCaption,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Left
            };
            b.Click += (_, _) =>
            {
                _selected = copy;
                ApplySelection(copy);
            };
            _recipeButtons.Children.Add(b);
        }
    }

    private void ApplySelection(AutomationRecipe r)
    {
        _ritualName.Text = r.Name;
        _ritualCategory.Text = r.Category ?? "";
        _ritualDesc.Text = r.Description;
        _approvalMode.SelectedItem = string.IsNullOrWhiteSpace(r.ApprovalMode) ? "manual" : r.ApprovalMode;
        _riskLevel.SelectedItem = string.IsNullOrWhiteSpace(r.RiskLevel) ? "medium" : r.RiskLevel;
        _adapterAffinity.Text = r.AdapterAffinity ?? "";
        _confidenceSource.Text = r.ConfidenceSource ?? "";
        _maxAutonomySteps.Text = r.MaxAutonomySteps > 0 ? r.MaxAutonomySteps.ToString() : "";
        _stepsEditor.Text = JsonSerializer.Serialize(r.Steps, new JsonSerializerOptions { WriteIndented = true });
        _stepCursor = 0;
    }

    private List<RecipeStep> ParseStepsEditor()
    {
        var raw = _stepsEditor.Text?.Trim() ?? "";
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

    private NexusSettings Settings() => NexusContext.GetSettings?.Invoke() ?? WinUiShellState.Settings;

    private async Task AlertAsync(string title, string message)
    {
        if (XamlRoot == null)
        {
            NexusShell.Log(title + ": " + message);
            return;
        }

        await new ContentDialog
        {
            Title = title,
            Content = message,
            CloseButtonText = "OK",
            XamlRoot = XamlRoot
        }.ShowAsync();
    }

    private async Task<bool> TrySaveCurrentAsync()
    {
        var steps = ParseStepsEditor();
        var recipe = _selected ?? new AutomationRecipe();
        if (string.IsNullOrEmpty(recipe.Id))
            recipe.Id = Guid.NewGuid().ToString("n");
        recipe.Name = _ritualName.Text?.Trim() ?? "Untitled";
        recipe.Category = _ritualCategory.Text?.Trim() ?? "";
        recipe.Description = _ritualDesc.Text ?? "";
        recipe.ApprovalMode = _approvalMode.SelectedItem?.ToString()?.Trim() ?? "manual";
        recipe.RiskLevel = _riskLevel.SelectedItem?.ToString()?.Trim() ?? "medium";
        recipe.AdapterAffinity = _adapterAffinity.Text?.Trim() ?? "";
        recipe.ConfidenceSource = _confidenceSource.Text?.Trim() ?? "";
        if (int.TryParse(_maxAutonomySteps.Text?.Trim(), out var maxA) && maxA > 0)
            recipe.MaxAutonomySteps = maxA;
        else
            recipe.MaxAutonomySteps = 0;
        recipe.Steps = steps;
        var qa = RitualQualityGate.Validate(recipe, Settings());
        if (!qa.Ok)
        {
            var msg = string.Join("\n", qa.Issues);
            NexusShell.Log("Flow QA blocked save: " + msg);
            await AlertAsync("Flow QA", msg);
            return false;
        }

        RitualRecipeStore.Upsert(recipe);
        _selected = recipe;
        NexusShell.Log($"Flow saved: {recipe.Name} ({recipe.Steps.Count} steps).");
        ReloadLibrary();
        return true;
    }

    private void DeleteCurrent()
    {
        if (_selected == null)
            return;
        RitualRecipeStore.DeleteById(_selected.Id);
        _selected = null;
        _ritualName.Text = "";
        _ritualCategory.Text = "";
        _ritualDesc.Text = "";
        _approvalMode.SelectedItem = "manual";
        _riskLevel.SelectedItem = "medium";
        _adapterAffinity.Text = "";
        _confidenceSource.Text = "";
        _maxAutonomySteps.Text = "";
        _stepsEditor.Text = "[]";
        NexusShell.Log("Flow deleted.");
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
        NexusShell.Log("Flow cloned.");
        ReloadLibrary();
    }

    private async Task ArchiveCurrentAsync()
    {
        if (_selected == null)
        {
            NexusShell.Log("Archive: no flow selected.");
            return;
        }

        if (!await TrySaveCurrentAsync())
            return;
        _selected!.Archived = true;
        RitualRecipeStore.Upsert(_selected);
        NexusShell.Log($"Flow archived: {_selected.Name}");
        ReloadLibrary();
    }

    private async Task PublishCurrentAsync()
    {
        if (_selected == null)
        {
            NexusShell.Log("Publish: no flow selected.");
            return;
        }

        if (!await TrySaveCurrentAsync())
            return;
        _selected!.PublicationState = "published";
        RitualRecipeStore.Upsert(_selected);
        NexusShell.Log($"Flow published: {_selected.Name}");
        ReloadLibrary();
    }

    private async Task QueueCurrentForRunAsync()
    {
        if (_selected == null)
        {
            NexusShell.Log("Queue: no flow selected — save or pick one first.");
            RefreshQueueStatus();
            return;
        }

        if (!await TrySaveCurrentAsync())
            return;
        if (_selected!.Archived)
        {
            NexusShell.Log("Queue: archived flows are not enqueued.");
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
                    .RunAsync(recipe.Steps, false, Settings(), recipe, _runCts.Token)
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
        var desc = _ritualDesc.Text?.Trim() ?? "";
        var steps = ParseStepsEditor();
        if (string.IsNullOrWhiteSpace(desc) && steps.Count == 0)
        {
            NexusShell.Log("AI short title: enter description or steps.");
            return;
        }

        var settings = Settings();
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

        _btnAiSuggest.IsEnabled = false;
        CompanionHub.Publish(CompanionVisualState.Thinking);
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
            const string sys =
                "Suggest exactly one short factual operator-flow name in English (at most 6 words). " +
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
            _ritualName.Text = oneLine;
            NexusShell.Log("AI short title applied.");
        }
        catch (Exception ex)
        {
            NexusShell.Log("AI short title: " + ex.Message);
            CompanionHub.Publish(CompanionVisualState.Error);
        }
        finally
        {
            _btnAiSuggest.IsEnabled = true;
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
                "Direct run blocked: flow is „published“ with approval „manual“ — use „queue for run“ and „approve next job“.");
            return;
        }

        _runCts = new CancellationTokenSource();
        _stepCursor = 0;
        NexusShell.Log(dryRun ? "Dry-run starting …" : "Plan run starting (simulated) …");
        await SimplePlanSimulator.RunAsync(steps, dryRun, Settings(), _selected, _runCts.Token)
            .ConfigureAwait(true);
        NexusShell.Log(dryRun ? "Dry-run done." : "Run done.");
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
                "Step blocked: published flow + manual approval — use queue + approve.");
            return;
        }

        _runCts ??= new CancellationTokenSource();
        var one = new List<RecipeStep> { steps[_stepCursor] };
        NexusShell.Log($"Next step {_stepCursor + 1}/{steps.Count}");
        await SimplePlanSimulator.RunAsync(one, false, Settings(), _selected, _runCts.Token)
            .ConfigureAwait(true);
        _stepCursor++;
    }

    private void PromoteFromHistory()
    {
        var entry = ActionHistoryService.GetLatestPlanRunWithSteps();
        if (entry == null)
        {
            NexusShell.Log("promote from history: no plan run with steps — run „run plan“ / operator flow first.");
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

    private async Task PromoteFromWatchAsync()
    {
        var doc = WatchSessionService.LoadOrEmpty();
        if (doc.Entries.Count == 0)
        {
            NexusShell.Log("promote from watch: watch-sessions.json empty (use „watch“ mode).");
            return;
        }

        _btnPromoteWatch.IsEnabled = false;
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
            var settings = Settings();
            var steps = await WatchPromoteService
                .BuildPromotedStepsAsync(settings, doc, cts.Token)
                .ConfigureAwait(true);
            if (steps.Count == 0)
            {
                NexusShell.Log("promote from watch: no steps produced.");
                return;
            }

            var recipe = new AutomationRecipe
            {
                Name = $"Promoted Watch {DateTime.Now:yyyy-MM-dd HH:mm}",
                Description = $"From {doc.Entries.Count} watch entries (LLM or token fallback)",
                Steps = steps
            };
            RitualRecipeStore.AppendRecipe(recipe);
            NexusShell.Log($"promote from watch: {recipe.Name} ({recipe.Steps.Count} steps).");
            ReloadLibrary();
        }
        catch (Exception ex)
        {
            NexusShell.Log("promote from watch: " + ex.Message);
        }
        finally
        {
            _btnPromoteWatch.IsEnabled = true;
        }
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
        _ritualName.Text = recipe.Name;
        _ritualCategory.Text = recipe.Category ?? "";
        _ritualDesc.Text = recipe.Description;
        _stepsEditor.Text = JsonSerializer.Serialize(recipe.Steps, new JsonSerializerOptions { WriteIndented = true });
        NexusShell.Log($"Teach: flow saved — {recipe.Name} ({recipe.Steps.Count} steps).");
        ReloadLibrary();
    }
}
