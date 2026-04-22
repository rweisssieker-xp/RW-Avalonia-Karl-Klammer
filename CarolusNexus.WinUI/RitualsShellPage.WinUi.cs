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
using VirtualKey = Windows.System.VirtualKey;
using VirtualKeyModifiers = Windows.System.VirtualKeyModifiers;

namespace CarolusNexus_WinUI.Pages;

/// <summary>Parity with Avalonia <c>RitualsTab</c> — library, governance fields, queue, teach, promote, run.</summary>
public sealed class RitualsShellPage : Page
{
    private List<AutomationRecipe> _all = new();
    private AutomationRecipe? _selected;
    private int _stepCursor;
    private CancellationTokenSource? _runCts;
    private ResponsiveBand? _ritualsLayoutBand;
    private string _executionState = "idle";
    private string _executionMode = "none";
    private string _executionError = "";
    private int _executionDoneSteps;
    private int _executionTotalSteps;
    private string _executionLastStep = "";

    private readonly Grid _root = new() { Margin = new Thickness(20, 16, 20, 16) };
    private readonly StackPanel _libraryPane = new() { Spacing = 8 };
    private readonly StackPanel _builderPane = new() { Spacing = 8 };
    private readonly StackPanel _stepsPane = new() { Spacing = 8 };
    private readonly StackPanel _flowStatus = new() { Orientation = Orientation.Horizontal, Spacing = 10 };
    private readonly Button _nbaPrimary = new();
    private readonly Button _nbaSecondary = new();
    private readonly Button _nbaDismiss = new();
    private Border? _nextBestActionBar;
    private NextBestAction? _nextBestAction;

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
    private readonly TextBlock _executionStatusLine = new() { TextWrapping = TextWrapping.Wrap, FontSize = 12 };
    private readonly TextBox _jobQueueDetail = new()
    {
        IsReadOnly = true,
        AcceptsReturn = true,
        MinHeight = 72,
        TextWrapping = TextWrapping.Wrap,
        FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas"),
        FontSize = 11
    };
    private readonly TextBox _executionDetail = new()
    {
        IsReadOnly = true,
        AcceptsReturn = true,
        MinHeight = 86,
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
        WinUiFluentChrome.SetIconButton(_btnAiSuggest, "AI short title", "\uE9CE", "Ctrl+T");
        WinUiFluentChrome.AddShortcut(_btnAiSuggest, VirtualKey.T, VirtualKeyModifiers.Control, "Ctrl+T");
        WinUiFluentChrome.SetIconButton(_btnPromoteWatch, "Promote from watch", "\uE72A", "Ctrl+W");
        WinUiFluentChrome.AddShortcut(_btnPromoteWatch, VirtualKey.W, VirtualKeyModifiers.Control, "Ctrl+W");
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
        _libraryPane.Children.Add(WinUiFluentChrome.StatusTile("Runtime reality", "local execution state", "flow editing and run state are real; external actions remain safety-gated"));
        _nextBestActionBar = BuildNextBestActionBar();
        _libraryPane.Children.Add(_nextBestActionBar);
        _libraryPane.Children.Add(_flowStatus);
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
            MkBtn("Save", async (_, _) => await TrySaveCurrentAsync(), shortcut: "Ctrl+S", key: VirtualKey.S, modifiers: VirtualKeyModifiers.Control),
            MkBtn("Delete", (_, _) => DeleteCurrent(), shortcut: "Del", key: VirtualKey.Delete),
            MkBtn("Clone", (_, _) => CloneCurrent()),
            MkBtn("Archive", async (_, _) => await ArchiveCurrentAsync()),
            MkBtn("Publish", async (_, _) => await PublishCurrentAsync())));
        _builderPane.Children.Add(MkRow(
            MkBtn("Queue for run", async (_, _) => await QueueCurrentForRunAsync(), shortcut: "Ctrl+Q", key: VirtualKey.Q, modifiers: VirtualKeyModifiers.Control),
            MkBtn("Approve next job", async (_, _) => await ApproveNextJobAsync(), shortcut: "Ctrl+Enter", key: VirtualKey.Enter, modifiers: VirtualKeyModifiers.Control)));
        _builderPane.Children.Add(MkRow(
            MkBtn("Dry run", async (_, _) => await RunSelectedAsync(true), shortcut: "F8", key: VirtualKey.F8),
            MkBtn("Run", async (_, _) => await RunSelectedAsync(false), accent: true, shortcut: "F9", key: VirtualKey.F9),
            MkBtn("Next step", async (_, _) => await RunNextStepAsync(), shortcut: "F10", key: VirtualKey.F10),
            MkBtn("Resume", async (_, _) => await ResumeSelectedAsync()),
            MkBtn("Test report", (_, _) => SaveFlowTestReport()),
            MkBtn("Audit export", (_, _) => ExportSelectedAuditPackage())));
        _builderPane.Children.Add(MkRow(
            MkBtn("Install templates", (_, _) => InstallTemplates()),
            MkBtn("Promote from history", (_, _) => PromoteFromHistory()),
            _btnPromoteWatch));
        _btnPromoteWatch.Click += async (_, _) => await PromoteFromWatchAsync();
        _builderPane.Children.Add(MkRow(
            MkBtn("Teach: start", (_, _) => StartTeach(), shortcut: "Ctrl+Shift+T", key: VirtualKey.T, modifiers: VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift),
            MkBtn("Teach: capture FG", (_, _) => CaptureForegroundTeachStep()),
            MkBtn("Teach: stop", (_, _) => StopTeach(), shortcut: "Ctrl+Shift+X", key: VirtualKey.X, modifiers: VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift)));

        _queueStatusLine.Foreground = WinUiFluentChrome.SecondaryTextBrush;
        WinUiFluentChrome.ApplyCaptionTextStyle(_queueStatusLine);
        _executionStatusLine.Foreground = WinUiFluentChrome.SecondaryTextBrush;
        WinUiFluentChrome.ApplyCaptionTextStyle(_executionStatusLine);
        _builderPane.Children.Add(WinUiFluentChrome.SectionCard("Execution state", "Local state machine for dry-run, run, approval and next-step", new StackPanel
        {
            Spacing = 8,
            Children = { _executionStatusLine, _executionDetail }
        }));
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

    private static Button MkBtn(string content, RoutedEventHandler click, bool accent = false, string? shortcut = null, VirtualKey? key = null, VirtualKeyModifiers modifiers = VirtualKeyModifiers.None)
    {
        var b = new Button { Content = string.IsNullOrWhiteSpace(shortcut) ? content : $"{content}  {shortcut}" };
        WinUiFluentChrome.StyleActionButton(b, accent);
        if (key.HasValue)
            WinUiFluentChrome.AddShortcut(b, key.Value, modifiers, shortcut);
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
        RefreshFlowStatus();
        RefreshExecutionStatus();
        RefreshNextBestActionBar();
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
        RefreshFlowStatus();
    }

    private void RefreshFlowStatus()
    {
        _flowStatus.Children.Clear();
        var selected = _selected?.Name ?? "none";
        var risk = _selected?.RiskLevel ?? "medium";
        var approval = _selected?.ApprovalMode ?? "manual";
        var queue = RitualJobQueueStore.GetPendingCount().ToString();
        var teach = RitualsTeachSession.IsActive ? "active" : "off";
        _flowStatus.Children.Add(WinUiFluentChrome.StatusTile("Selected flow", selected, "library selection"));
        _flowStatus.Children.Add(WinUiFluentChrome.StatusTile("Risk", risk, "governance"));
        _flowStatus.Children.Add(WinUiFluentChrome.StatusTile("Approval", approval, "execution gate"));
        _flowStatus.Children.Add(WinUiFluentChrome.StatusTile("Execution", _executionState, _executionMode));
        _flowStatus.Children.Add(WinUiFluentChrome.StatusTile("Queue", queue, "pending jobs"));
        _flowStatus.Children.Add(WinUiFluentChrome.StatusTile("Teach", teach, "capture mode"));
    }

    private void SetExecutionState(string state, string mode, int doneSteps, int totalSteps, string? error = null)
    {
        _executionState = state;
        _executionMode = mode;
        _executionDoneSteps = Math.Max(0, doneSteps);
        _executionTotalSteps = Math.Max(0, totalSteps);
        _executionError = error ?? "";
        RefreshExecutionStatus();
        RefreshFlowStatus();
    }

    private void SetExecutionLastStep(string? lastStep)
    {
        _executionLastStep = (lastStep ?? "").Trim();
        RefreshExecutionStatus();
        RefreshFlowStatus();
    }

    private void RefreshExecutionStatus()
    {
        var selected = _selected?.Name ?? (_ritualName.Text?.Trim() ?? "unsaved flow");
        _executionStatusLine.Text =
            $"State: {_executionState} · Mode: {_executionMode} · Steps: {_executionDoneSteps}/{_executionTotalSteps}";
        _executionDetail.Text =
            $"flow: {selected}\n" +
            $"state: {_executionState}\n" +
            $"mode: {_executionMode}\n" +
            $"cursor: {_stepCursor}\n" +
            $"steps: {_executionDoneSteps}/{_executionTotalSteps}\n" +
            $"approval: {_approvalMode.SelectedItem ?? "manual"}\n" +
            $"risk: {_riskLevel.SelectedItem ?? "medium"}\n" +
            (string.IsNullOrWhiteSpace(_executionError) ? "error: none" : $"error: {_executionError}") +
            "\n" +
            $"last step: {(_executionLastStep.Length == 0 ? "none" : _executionLastStep)}";
    }

    private Border BuildNextBestActionBar()
    {
        _nbaPrimary.Click += async (_, _) =>
        {
            if (_nextBestAction?.Intent == "rituals.promote_watch")
                await PromoteFromWatchAsync();
            else
                NexusShell.Log("Next action: select or teach a flow before running.");
            RefreshNextBestActionBar();
        };
        _nbaSecondary.Click += (_, _) =>
        {
            if (_nextBestAction != null)
                NexusShell.Log("Next action suggestion: " + _nextBestAction.Message);
        };
        _nbaDismiss.Click += (_, _) =>
        {
            if (_nextBestActionBar != null)
                _nextBestActionBar.Visibility = Visibility.Collapsed;
        };
        _nextBestAction = NextBestActionService.Build(WinUiShellState.Settings, WinUiShellState.LiveContextLine);
        return WinUiFluentChrome.NextBestActionBar(_nextBestAction, _nbaPrimary, _nbaSecondary, _nbaDismiss);
    }

    private void RefreshNextBestActionBar()
    {
        _nextBestAction = NextBestActionService.Build(WinUiShellState.Settings, WinUiShellState.LiveContextLine);
        NexusShell.Log("Rituals next action refreshed: " + _nextBestAction.Message);
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
            SetExecutionState("blocked", "queue", 0, 0, "No flow selected");
            RefreshQueueStatus();
            return;
        }

        if (!await TrySaveCurrentAsync())
            return;
        if (_selected!.Archived)
        {
            NexusShell.Log("Queue: archived flows are not enqueued.");
            SetExecutionState("blocked", "queue", 0, _selected.Steps.Count, "Archived flows are not enqueued");
            RefreshQueueStatus();
            return;
        }

        RitualJobQueueStore.Enqueue(_selected.Id, _selected.Name);
        var n = RitualJobQueueStore.GetPendingCount();
        NexusShell.Log($"Enqueued: {_selected.Name} (pending: {n}).");
        SetExecutionState("queued", "approval queue", 0, _selected.Steps.Count);
        RefreshQueueStatus();
    }

    private async Task ApproveNextJobAsync()
    {
        try
        {
            if (!RitualJobQueueStore.TryDequeuePending(out var job) || job == null)
            {
                NexusShell.Log("Job queue: no pending jobs.");
                SetExecutionState("idle", "queue", 0, 0);
                return;
            }

            var all = RitualRecipeStore.LoadAll();
            var recipe = all.FirstOrDefault(r => string.Equals(r.Id, job.RecipeId, StringComparison.Ordinal));
            if (recipe == null)
            {
                RitualJobQueueStore.RecordHistory(job, "failed", "Recipe not found");
                NexusShell.Log($"Job cancelled: recipe {job.RecipeId} missing.");
                SetExecutionState("failed", "queue", 0, 0, "Recipe not found");
                return;
            }

            if (recipe.Archived)
            {
                RitualJobQueueStore.RecordHistory(job, "failed", "Recipe archived");
                NexusShell.Log($"Job skipped: {recipe.Name} is archived.");
                SetExecutionState("blocked", "queue", 0, recipe.Steps.Count, "Recipe archived");
                return;
            }

            _runCts?.Cancel();
            _runCts = new CancellationTokenSource();
            NexusShell.Log($"Job approved — run: {recipe.Name} …");
            SetExecutionState("running", "approved queue", 0, recipe.Steps.Count);
            try
            {
                var log = await SimplePlanSimulator
                    .RunAsync(recipe.Steps, false, Settings(), recipe, _runCts.Token)
                    .ConfigureAwait(true);
                RitualJobQueueStore.RecordHistory(job, "completed", null);
                NexusShell.Log($"Job completed: {recipe.Name}");
                SetExecutionState("completed", "approved queue", recipe.Steps.Count, recipe.Steps.Count);
                SetExecutionLastStep(LastResultFromSimulator(log));
            }
            catch (OperationCanceledException)
            {
                RitualJobQueueStore.RecordHistory(job, "cancelled", null);
                NexusShell.Log("Job cancelled.");
                SetExecutionState("cancelled", "approved queue", 0, recipe.Steps.Count);
                SetExecutionLastStep("cancelled");
            }
            catch (Exception ex)
            {
                RitualJobQueueStore.RecordHistory(job, "failed", ex.Message);
                NexusShell.Log("Job failed: " + ex.Message);
                SetExecutionState("failed", "approved queue", 0, recipe.Steps.Count, ex.Message);
                SetExecutionLastStep(ex.Message);
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
            SetExecutionState("blocked", dryRun ? "dry-run" : "run", 0, 0, "No steps to run");
            return;
        }

        if (_selected != null
            && !dryRun
            && string.Equals(_selected.PublicationState, "published", StringComparison.OrdinalIgnoreCase)
            && string.Equals(_selected.ApprovalMode, "manual", StringComparison.OrdinalIgnoreCase))
        {
            NexusShell.Log(
                "Direct run blocked: flow is „published“ with approval „manual“ — use „queue for run“ and „approve next job“.");
            SetExecutionState("blocked", "run", 0, steps.Count, "Published manual flow requires queue approval");
            return;
        }

        _runCts = new CancellationTokenSource();
        _stepCursor = 0;
        var mode = dryRun ? "dry-run" : "guarded run";
        NexusShell.Log(dryRun ? "Dry-run starting …" : "Plan run starting (guarded) …");
        SetExecutionState("running", mode, 0, steps.Count);
        try
        {
            var log = await SimplePlanSimulator.RunAsync(steps, dryRun, Settings(), _selected, _runCts.Token)
                .ConfigureAwait(true);
            _stepCursor = steps.Count;
            SetExecutionState("completed", mode, steps.Count, steps.Count);
            SetExecutionLastStep(LastResultFromSimulator(log));
            NexusShell.Log(dryRun ? "Dry-run done." : "Run done.");
        }
        catch (OperationCanceledException)
        {
            SetExecutionState("cancelled", mode, _stepCursor, steps.Count);
            SetExecutionLastStep("cancelled");
            NexusShell.Log("Run cancelled.");
        }
        catch (Exception ex)
        {
            SetExecutionState("failed", mode, _stepCursor, steps.Count, ex.Message);
            SetExecutionLastStep(ex.Message);
            NexusShell.Log("Run failed: " + ex.Message);
        }
    }

    private async Task ResumeSelectedAsync()
    {
        var recipe = _selected;
        var steps = recipe != null ? recipe.Steps : ParseStepsEditor();
        if (steps.Count == 0)
        {
            NexusShell.Log("Resume: no steps.");
            SetExecutionState("blocked", "resume", 0, 0, "No steps");
            return;
        }

        var start = 0;
        if (recipe != null)
        {
            var state = FlowResumeStore.Get(recipe.Id);
            if (state != null)
                start = Math.Clamp(state.NextStepIndex, 0, steps.Count);
        }

        if (start >= steps.Count)
        {
            NexusShell.Log("Resume: flow already completed.");
            SetExecutionState("completed", "resume", steps.Count, steps.Count);
            return;
        }

        _runCts = new CancellationTokenSource();
        _stepCursor = start;
        var remaining = steps.Skip(start).ToList();
        NexusShell.Log($"Resume: starting at step {start + 1}/{steps.Count}.");
        SetExecutionState("running", "resume", start, steps.Count);
        try
        {
            var log = await SimplePlanSimulator.RunAsync(remaining, false, Settings(), recipe, _runCts.Token)
                .ConfigureAwait(true);
            _stepCursor = steps.Count;
            SetExecutionLastStep(LastResultFromSimulator(log));
            SetExecutionState("completed", "resume", steps.Count, steps.Count);
        }
        catch (OperationCanceledException)
        {
            SetExecutionState("cancelled", "resume", _stepCursor, steps.Count);
            SetExecutionLastStep("cancelled");
        }
        catch (Exception ex)
        {
            SetExecutionState("failed", "resume", _stepCursor, steps.Count, ex.Message);
            SetExecutionLastStep(ex.Message);
        }
    }

    private AutomationRecipe CurrentRecipeSnapshot()
    {
        return _selected ?? new AutomationRecipe
        {
            Name = _ritualName.Text?.Trim() ?? "Unsaved Flow",
            Description = _ritualDesc.Text ?? "",
            ApprovalMode = _approvalMode.SelectedItem?.ToString()?.Trim() ?? "manual",
            RiskLevel = _riskLevel.SelectedItem?.ToString()?.Trim() ?? "medium",
            AdapterAffinity = _adapterAffinity.Text?.Trim() ?? "",
            ConfidenceSource = _confidenceSource.Text?.Trim() ?? "",
            Steps = ParseStepsEditor()
        };
    }

    private void SaveFlowTestReport()
    {
        var recipe = CurrentRecipeSnapshot();
        if (recipe.Steps.Count == 0)
        {
            NexusShell.Log("Flow test: no steps.");
            SetExecutionState("blocked", "test", 0, 0, "No steps");
            return;
        }

        var path = FlowTestStudioService.SaveReport(recipe, Settings());
        NexusShell.Log("Flow test report → " + path);
        SetExecutionState("tested", "flow test", recipe.Steps.Count, recipe.Steps.Count);
        SetExecutionLastStep(path);
    }

    private void ExportSelectedAuditPackage()
    {
        var recipe = CurrentRecipeSnapshot();
        var path = AuditExportPackageService.Export(recipe.Steps.Count == 0 ? null : recipe, Settings());
        NexusShell.Log("Audit export → " + path);
        SetExecutionState("exported", "audit", recipe.Steps.Count, recipe.Steps.Count);
        SetExecutionLastStep(path);
    }

    private void InstallTemplates()
    {
        var added = FlowTemplateCatalogService.EnsureDefaultTemplates();
        NexusShell.Log($"Templates installed: {added} new.");
        ReloadLibrary();
    }

    private async Task RunNextStepAsync()
    {
        var steps = _selected != null ? _selected.Steps : ParseStepsEditor();
        if (_stepCursor >= steps.Count)
        {
            NexusShell.Log("No further step.");
            SetExecutionState("completed", "next-step", steps.Count, steps.Count);
            return;
        }

        if (_selected != null
            && string.Equals(_selected.PublicationState, "published", StringComparison.OrdinalIgnoreCase)
            && string.Equals(_selected.ApprovalMode, "manual", StringComparison.OrdinalIgnoreCase))
        {
            NexusShell.Log(
                "Step blocked: published flow + manual approval — use queue + approve.");
            SetExecutionState("blocked", "next-step", _stepCursor, steps.Count, "Published manual flow requires queue approval");
            return;
        }

        _runCts ??= new CancellationTokenSource();
        var one = new List<RecipeStep> { steps[_stepCursor] };
        NexusShell.Log($"Next step {_stepCursor + 1}/{steps.Count}");
        SetExecutionState("running", "next-step", _stepCursor, steps.Count);
        try
        {
            var log = await SimplePlanSimulator.RunAsync(one, false, Settings(), _selected, _runCts.Token)
                .ConfigureAwait(true);
            _stepCursor++;
            SetExecutionLastStep(LastResultFromSimulator(log));
            SetExecutionState(_stepCursor >= steps.Count ? "completed" : "paused", "next-step", _stepCursor, steps.Count);
        }
        catch (OperationCanceledException)
        {
            SetExecutionState("cancelled", "next-step", _stepCursor, steps.Count);
            SetExecutionLastStep("cancelled");
            NexusShell.Log("Step cancelled.");
        }
        catch (Exception ex)
        {
            SetExecutionState("failed", "next-step", _stepCursor, steps.Count, ex.Message);
            SetExecutionLastStep(ex.Message);
            NexusShell.Log("Step failed: " + ex.Message);
        }
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
