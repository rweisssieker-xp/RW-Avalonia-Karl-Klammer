using System;
using System.IO;
using Avalonia.Controls;
using CarolusNexus;

namespace CarolusNexus.Views;

public partial class DiagnosticsTab : UserControl
{
    public DiagnosticsTab()
    {
        InitializeComponent();
        BtnExport.Click += (_, _) => ExportDiagnostics();
        BtnClear.Click += (_, _) => { DiagLog.Text = ""; NexusShell.Log("Logs geleert (lokal)."); };
    }

    public void Append(string line)
    {
        DiagLog.Text += line + Environment.NewLine;
    }

    public void ExportDiagnostics()
    {
        var name = Path.Combine(AppPaths.DataDir, $"diagnostics-{DateTime.Now:yyyyMMdd-HHmmss}.log");
        File.WriteAllText(name, DiagLog.Text ?? "");
        NexusShell.Log($"export diagnostics → {name}");
    }
}
