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

        _refreshTimer.Tick += (_, _) => RefreshActiveSnapshot();
        Loaded += (_, _) => _refreshTimer.Start();
        Unloaded += (_, _) => _refreshTimer.Stop();

        SnapCross.Text =
            "Adapter-Schaltflächen vergleichen die gewählte Familie mit dem aktiven Fenster.\r\n" +
            "Custom action + „run“: versucht ausführbare Tokens (power-user + PlanGuard) und zeigt das Ergebnis.\r\n" +
            "Teach-Modus (Rituals): „run“ und Adapter-Klicks erzeugen Ritual-Schritte.";

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
        sb.AppendLine($"Adapter-Schaltfläche: „{familyKey}“");
        sb.AppendLine($"Aktive Familie (Heuristik): {curFam}");
        sb.AppendLine(matches ? "→ Fenster passt zur gewählten Familie (heuristisch)." : "→ Vordergrundfenster weicht von der Schaltfläche ab — trotzdem Kontext-Hinweis nutzbar.");
        if (d != null)
        {
            sb.AppendLine();
            sb.AppendLine($"Fenster: „{d.Value.Title}“");
            sb.AppendLine($"Prozess: {d.Value.ProcessName} (PID {d.Value.ProcessId})");
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
            NexusShell.Log("Teach: Adapter-Schritt erfasst.");
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
            NexusShell.Log("inspector custom: (leer)");

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
            NexusShell.Log("Teach: Inspector-Schritt erfasst.");
        }
    }

    private void RefreshActiveSnapshot()
    {
        if (!OperatingSystem.IsWindows())
        {
            SnapActive.Text = "Live Context: nur unter Windows.";
            SnapAx.Text = "—";
            SnapCross.Text = "—";
            return;
        }

        var d = ForegroundWindowInfo.TryReadDetail();
        if (d == null)
        {
            SnapActive.Text = "(kein Vordergrundfenster)";
            SnapAx.Text = "—";
            return;
        }

        var fam = OperatorAdapterRegistry.ResolveFamily(d.Value.ProcessName, d.Value.Title);
        var sb = new StringBuilder();
        sb.AppendLine($"Titel: {d.Value.Title}");
        sb.AppendLine($"Prozess: {d.Value.ProcessName} (PID {d.Value.ProcessId})");
        sb.AppendLine($"Klasse: {d.Value.WindowClass}");
        sb.AppendLine($"Bounds: {d.Value.Left}, {d.Value.Top} · {d.Value.Width}×{d.Value.Height}");
        sb.AppendLine($"Adapter-Familie (Heuristik): {fam}");
        sb.AppendLine($"Bekannte Familien: {string.Join(", ", OperatorAdapterRegistry.KnownFamilies)}");
        SnapActive.Text = sb.ToString();

        if (fam == "ax2012")
        {
            SnapAx.Text =
                "AX / Dynamics-Fat-Client erkannt (Titel/Prozess-Heuristik).\r\n" +
                "Golden Path: Vordergrund-Kontext oben; tiefe Form/Grid-UIAutomation ist roadmap — nutzen Sie Vision+Plan im Ask-Tab.\r\n" +
                $"Snapshot: „{d.Value.Title}“ · {d.Value.ProcessName}";
        }
        else
        {
            SnapAx.Text =
                $"Kein AX-Fenster im Vordergrund (aktuell: {fam}).\r\n" +
                "Wechseln Sie in den AX-Client oder nutzen Sie die AX-Schaltfläche für Kontext-Hinweise.";
        }
    }
}
