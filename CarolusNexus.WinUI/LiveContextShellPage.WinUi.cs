using System;
using System.Text;
using CarolusNexus;
using CarolusNexus.Models;
using CarolusNexus.Services;
using CarolusNexus_WinUI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace CarolusNexus_WinUI.Pages;

/// <summary>Parity with Avalonia <c>LiveContextTab</c> (adapter buttons, inspector, three panes).</summary>
public sealed class LiveContextShellPage : Page
{
    private readonly TextBox _snapActive = MkSnap();
    private readonly TextBox _snapAx = MkSnap();
    private readonly TextBox _snapCross = MkSnap();
    private readonly TextBox _inspectorAction = new() { PlaceholderText = "InspectorAction / custom (e.g. ax.read_context)" };
    private DispatcherQueueTimer? _refreshTimer;

    public LiveContextShellPage()
    {
        var root = new StackPanel { Spacing = 14, Margin = new Thickness(20, 16, 20, 16) };
        root.Children.Add(WinUiFluentChrome.PageTitle("Live Context"));
        var liveHint = new TextBlock
        {
            Text = "Desktop inspector · foreground window + adapter heuristics (Handbuch §5.10).",
            TextWrapping = TextWrapping.Wrap,
            Foreground = WinUiFluentChrome.SecondaryTextBrush
        };
        WinUiFluentChrome.ApplyCaptionTextStyle(liveHint);
        root.Children.Add(liveHint);
        root.Children.Add(WinUiFluentChrome.ColumnCaption("Adapter families"));

        var adapters = new GridView { SelectionMode = ListViewSelectionMode.None };
        foreach (var (key, label) in new (string key, string label)[]
                 {
                     ("explorer", "Explorer"),
                     ("browser", "Browser"),
                     ("mail", "Mail"),
                     ("outlook", "Outlook"),
                     ("teams", "Teams"),
                     ("word", "Word"),
                     ("excel", "Excel"),
                     ("powerpoint", "PowerPoint"),
                     ("onenote", "OneNote"),
                     ("editor", "Editor"),
                     ("ax2012", "AX")
                 })
        {
            var b = new Button { Content = label, Margin = new Thickness(0, 0, 6, 6) };
            WinUiFluentChrome.StyleActionButton(b, compact: true);
            var k = key;
            b.Click += (_, _) => OnAdapterClick(k);
            adapters.Items.Add(b);
        }

        root.Children.Add(new ScrollViewer { MaxHeight = 220, Content = adapters });

        root.Children.Add(WinUiFluentChrome.ColumnCaption("Inspector action"));
        var inspectorRow = new Grid();
        inspectorRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        inspectorRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        inspectorRow.Children.Add(_inspectorAction);
        var run = new Button { Content = "Run", Margin = new Thickness(8, 0, 0, 0) };
        WinUiFluentChrome.StyleActionButton(run, accent: true);
        run.Click += (_, _) => RunInspectorCustom();
        Grid.SetColumn(run, 1);
        inspectorRow.Children.Add(run);
        root.Children.Add(inspectorRow);

        root.Children.Add(WinUiFluentChrome.ColumnCaption("Snapshots"));
        var pivot = new Pivot();
        pivot.Items.Add(new PivotItem { Header = "Active Window", Content = _snapActive });
        pivot.Items.Add(new PivotItem { Header = "AX Context", Content = _snapAx });
        pivot.Items.Add(new PivotItem { Header = "Cross-App", Content = _snapCross });
        root.Children.Add(WinUiFluentChrome.WrapCard(pivot, new Thickness(8, 8, 8, 8)));

        Content = new ScrollViewer { Content = root };

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;

        _snapCross.Text =
            "Adapter buttons compare the selected family with the active window.\r\n" +
            "Custom action + “run”: tries executable tokens (power-user + PlanGuard) and shows the result.\r\n" +
            "Teach mode (Operator flows): “run” and adapter clicks create flow steps.";

        RefreshActiveSnapshot();
    }

    private static TextBox MkSnap() =>
        new()
        {
            IsReadOnly = true,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            MinHeight = 200,
            FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas")
        };

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var dq = DispatcherQueue.GetForCurrentThread();
        _refreshTimer = dq.CreateTimer();
        _refreshTimer.Interval = TimeSpan.FromSeconds(1.5);
        _refreshTimer.Tick += (_, _) => RefreshActiveSnapshot();
        _refreshTimer.Start();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_refreshTimer != null)
        {
            _refreshTimer.Stop();
            _refreshTimer = null;
        }
    }

    private void OnAdapterClick(string familyKey)
    {
        var d = ForegroundWindowInfo.TryReadDetail();
        var curFam = d == null
            ? "generic"
            : OperatorAdapterRegistry.ResolveFamily(d.Value.ProcessName, d.Value.Title);
        var targetFam = familyKey == "ax2012" ? "ax2012" : familyKey;
        var matches = string.Equals(curFam, targetFam, StringComparison.OrdinalIgnoreCase)
                      || (familyKey == "ax2012" && curFam == "ax2012");
        var sb = new StringBuilder();
        sb.AppendLine($"Adapter button: “{familyKey}”");
        sb.AppendLine($"Active family (heuristic): {curFam}");
        sb.AppendLine(matches ? "→ Window matches the selected family (heuristic)." : "→ Foreground window differs from the button — context hints still usable.");
        if (d != null)
        {
            sb.AppendLine();
            sb.AppendLine($"Window: “{d.Value.Title}”");
            sb.AppendLine($"Process: {d.Value.ProcessName} (PID {d.Value.ProcessId})");
        }

        _snapCross.Text = sb.ToString();
        NexusShell.Log($"Live Context · Adapter {familyKey} · aktiv={curFam}");

        if (RitualsTeachSession.IsActive)
        {
            RitualsTeachSession.Append(new RecipeStep
            {
                ActionType = "token",
                ActionArgument = $"adapter|{familyKey}",
                WaitMs = 200
            });
            NexusShell.Log("Teach: adapter step captured.");
        }
    }

    private void RunInspectorCustom()
    {
        var action = _inspectorAction.Text?.Trim() ?? "";
        var settings = NexusContext.GetSettings?.Invoke() ?? new NexusSettings();
        var step = new RecipeStep { ActionType = "token", ActionArgument = action, WaitMs = 0 };

        var execNote = "";
        if (!string.IsNullOrEmpty(action) && OperatingSystem.IsWindows())
        {
            execNote = Win32AutomationExecutor.Execute(step, settings);
            NexusShell.Log($"inspector custom: {action} → {execNote}");
        }
        else if (string.IsNullOrEmpty(action))
            NexusShell.Log("inspector custom: (empty)");

        var d = ForegroundWindowInfo.TryReadDetail();
        var sb = new StringBuilder();
        sb.AppendLine($"@ {DateTime.Now:O}");
        sb.AppendLine($"action={action}");
        if (!string.IsNullOrEmpty(execNote))
            sb.AppendLine($"try_execute: {execNote}");
        if (d != null)
        {
            sb.AppendLine();
            sb.AppendLine($"foreground: {d.Value.ProcessName} · „{d.Value.Title}“");
            sb.AppendLine(
                $"class={d.Value.WindowClass} bounds={d.Value.Left},{d.Value.Top} {d.Value.Width}×{d.Value.Height}");
        }

        _snapActive.Text = sb.ToString();

        if (RitualsTeachSession.IsActive && !string.IsNullOrWhiteSpace(action))
        {
            RitualsTeachSession.Append(new RecipeStep
            {
                ActionType = "token",
                ActionArgument = action,
                WaitMs = 300
            });
            NexusShell.Log("Teach: inspector step captured.");
        }
    }

    private void RefreshActiveSnapshot()
    {
        if (!OperatingSystem.IsWindows())
        {
            _snapActive.Text = "Live Context: Windows only.";
            _snapAx.Text = "—";
            _snapCross.Text = "—";
            return;
        }

        var d = ForegroundWindowInfo.TryReadDetail();
        if (d == null)
        {
            _snapActive.Text = "(no foreground window)";
            _snapAx.Text = "—";
            return;
        }

        var fam = OperatorAdapterRegistry.ResolveFamily(d.Value.ProcessName, d.Value.Title);
        var sb = new StringBuilder();
        sb.AppendLine($"Title: {d.Value.Title}");
        sb.AppendLine($"Process: {d.Value.ProcessName} (PID {d.Value.ProcessId})");
        sb.AppendLine($"Class: {d.Value.WindowClass}");
        sb.AppendLine($"Bounds: {d.Value.Left}, {d.Value.Top} · {d.Value.Width}×{d.Value.Height}");
        sb.AppendLine($"Adapter family (heuristic): {fam}");
        sb.AppendLine($"Known families: {string.Join(", ", OperatorAdapterRegistry.KnownFamilies)}");
        _snapActive.Text = sb.ToString();

        if (fam == "ax2012")
        {
            _snapAx.Text =
                "AX / Dynamics fat client detected (title/process heuristic).\r\n" +
                "Golden path: foreground context above; deep form/grid UI automation is roadmap — use Vision+Plan on the Ask tab.\r\n" +
                $"Snapshot: “{d.Value.Title}” · {d.Value.ProcessName}";
        }
        else
        {
            _snapAx.Text =
                $"No AX window in the foreground (current: {fam}).\r\n" +
                "Switch to the AX client or use the AX button for context hints.";
        }
    }
}
