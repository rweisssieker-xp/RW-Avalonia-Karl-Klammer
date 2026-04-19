using Avalonia.Controls;
using Avalonia.Interactivity;
using CarolusNexus;

namespace CarolusNexus.Views;

public partial class LiveContextTab : UserControl
{
    public LiveContextTab()
    {
        InitializeComponent();
        BExplorer.Click += (_, _) => StubAdapter("explorer");
        BBrowser.Click += (_, _) => StubAdapter("browser");
        BMail.Click += (_, _) => StubAdapter("mail");
        BOutlook.Click += (_, _) => StubAdapter("outlook");
        BTeams.Click += (_, _) => StubAdapter("teams");
        BWord.Click += (_, _) => StubAdapter("word");
        BExcel.Click += (_, _) => StubAdapter("excel");
        BPowerPoint.Click += (_, _) => StubAdapter("powerpoint");
        BOneNote.Click += (_, _) => StubAdapter("onenote");
        BEditor.Click += (_, _) => StubAdapter("editor");
        BAx.Click += (_, _) => StubAdapter("ax");
        BtnRunInspector.Click += (_, _) =>
        {
            var a = InspectorAction.Text?.Trim() ?? "";
            NexusShell.LogStub($"inspector custom: {a}");
            SnapActive.Text = $"Stub read_context @ {System.DateTime.Now:O}\r\naction={a}";
        };
        SnapAx.Text = "(AxClientAutomationService — Stub)";
        SnapCross.Text = "(Cross-App Adapter — Stub)";
    }

    private void StubAdapter(string name)
    {
        NexusShell.LogStub($"adapter snapshot: {name}");
        SnapActive.Text = $"Adapter „{name}“ — UIAutomation/AX-Runtime nicht angebunden.";
    }
}
