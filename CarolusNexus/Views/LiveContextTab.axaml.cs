using System;
using System.Text;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using CarolusNexus.Models;
using CarolusNexus.Services;

namespace CarolusNexus.Views;

public partial class LiveContextTab : UserControl
{
    private readonly DispatcherTimer _refreshTimer = new() { Interval = TimeSpan.FromSeconds(1.5) };

    public LiveContextTab()
    {
        InitializeComponent();
        BExplorer.Click += (_, _) => OnAdapterClick("explorer");
        BBrowser.Click += (_, _) => OnAdapterClick("browser");
        BMail.Click += (_, _) => OnAdapterClick("mail");
        BOutlook.Click += (_, _) => OnAdapterClick("outlook");
        BTeams.Click += (_, _) => OnAdapterClick("teams");
        BWord.Click += (_, _) => OnAdapterClick("word");
        BExcel.Click += (_, _) => OnAdapterClick("excel");
        BPowerPoint.Click += (_, _) => OnAdapterClick("powerpoint");
        BOneNote.Click += (_, _) => OnAdapterClick("onenote");
        BEditor.Click += (_, _) => OnAdapterClick("editor");
        BAx.Click += (_, _) => OnAdapterClick("ax2012");
        BtnRunInspector.Click += (_, _) => RunInspectorCustom();
        BtnCreateFlowFromContext.Click += (_, _) => CreateFlowFromContext();
        BtnExportUspPack.Click += (_, _) => ExportUspPack();

        _refreshTimer.Tick += (_, _) => RefreshActiveSnapshot();
        Loaded += (_, _) => _refreshTimer.Start();
        Unloaded += (_, _) => _refreshTimer.Stop();

        SnapCross.Text =
            "Adapter buttons compare the selected family with the active window.\r\n" +
            "Custom action + “run”: tries executable tokens (power-user + PlanGuard) and shows the result.\r\n" +
            "Teach mode (Operator flows): “run” and adapter clicks create flow steps.";

        RefreshActiveSnapshot();
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

        SnapCross.Text = sb.ToString();
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
        var action = InspectorAction.Text?.Trim() ?? "";
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

        SnapActive.Text = sb.ToString();

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

    private void CreateFlowFromContext()
    {
        try
        {
            var settings = NexusContext.GetSettings?.Invoke() ?? new NexusSettings();
            var recipe = OperatorUspPackService.CreateFlowFromForeground(settings);
            SnapCross.Text = "Context-to-Flow created\n"
                + $"Name: {recipe.Name}\n"
                + $"Adapter: {recipe.AdapterAffinity}\n"
                + $"Risk: {recipe.RiskLevel}\n"
                + $"Approval: {recipe.ApprovalMode}\n"
                + $"Steps: {recipe.Steps.Count}\n\n"
                + "Open Rituals to review, publish, queue, or run it.";
            NexusShell.Log("Live Context: context flow created: " + recipe.Name);
        }
        catch (Exception ex)
        {
            SnapCross.Text = "Context-to-Flow failed: " + ex.Message;
            NexusShell.Log("Live Context: context flow failed: " + ex.Message);
        }
    }

    private void ExportUspPack()
    {
        try
        {
            var settings = NexusContext.GetSettings?.Invoke() ?? new NexusSettings();
            var path = OperatorUspPackService.ExportProofPack(settings);
            SnapCross.Text = "USP Proof Pack exported\n" + path + "\n\n" + OperatorUspPackService.BuildUspRadar(settings);
            NexusShell.Log("Live Context: USP proof pack exported: " + path);
        }
        catch (Exception ex)
        {
            SnapCross.Text = "USP Proof Pack export failed: " + ex.Message;
            NexusShell.Log("Live Context: USP proof pack failed: " + ex.Message);
        }
    }

    private void RefreshActiveSnapshot()
    {
        if (!OperatingSystem.IsWindows())
        {
            SnapActive.Text = "Live Context: Windows only.";
            SnapAx.Text = "—";
            SnapCross.Text = "—";
            return;
        }

        var d = ForegroundWindowInfo.TryReadDetail();
        if (d == null)
        {
            SnapActive.Text = "(no foreground window)";
            SnapAx.Text = "—";
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
        var settings = NexusContext.GetSettings?.Invoke() ?? new NexusSettings();
        if (OperatingSystem.IsWindows()
            && string.Equals(settings.Safety.Profile, "power-user", StringComparison.OrdinalIgnoreCase))
        {
            sb.AppendLine();
            sb.AppendLine("--- UIA form summary (power-user) ---");
            sb.AppendLine(ForegroundUiAutomationContext.BuildFormSummary(settings, 40, 12));
            var selHint = ForegroundUiAutomationContext.TryReadSelectionHint(settings);
            if (!string.IsNullOrEmpty(selHint))
            {
                sb.AppendLine("---");
                sb.AppendLine(selHint);
            }
        }

        SnapActive.Text = sb.ToString();

        if (fam == "ax2012")
        {
            var form = ForegroundUiAutomationContext.BuildFormSummary(settings, 56, 14);
            var deep = ForegroundUiAutomationContext.BuildDeepSelectionSummary(settings);
            SnapAx.Text =
                "[AX] Deep UIA snapshot\r\n" +
                (string.IsNullOrWhiteSpace(form) ? "(no form summary)" : form) +
                (string.IsNullOrWhiteSpace(deep) ? "" : "\r\n---\r\n" + deep);
        }
        else
        {
            SnapAx.Text =
                $"No AX window in the foreground (current: {fam}).\r\n" +
                "Switch to the AX client or use the AX button for context hints.";
        }
    }
}
