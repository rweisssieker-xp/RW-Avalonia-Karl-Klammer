using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using CarolusNexus;

namespace CarolusNexus.Views;

public partial class DiagnosticsTab : UserControl
{
    public DiagnosticsTab()
    {
        InitializeComponent();
        BtnExport.Click += (_, _) => ExportDiagnostics();
        BtnCopy.Click += async (_, _) => await CopyAllToClipboardAsync();
        BtnClear.Click += (_, _) => { DiagLog.Text = ""; NexusShell.Log("Logs geleert (lokal)."); };
        BtnAudit.Click += (_, _) => LoadRitualAuditTail();
    }

    private void LoadRitualAuditTail()
    {
        if (!File.Exists(AppPaths.RitualStepAudit))
        {
            NexusShell.Log("ritual-step-audit.jsonl existiert noch nicht.");
            DiagLog.Text = "(kein Audit — zuerst Plan/Ritual mit Schritten ausführen)";
            return;
        }

        try
        {
            var lines = File.ReadAllLines(AppPaths.RitualStepAudit);
            var tail = lines.Length <= 120 ? lines : lines.Skip(lines.Length - 120).ToArray();
            DiagLog.Text = string.Join(Environment.NewLine, tail);
            NexusShell.Log($"Diagnostics: Ritual-Audit, letzte {tail.Length} Zeilen.");
        }
        catch (Exception ex)
        {
            NexusShell.Log("Ritual-Audit lesen: " + ex.Message);
        }
    }

    public void Append(string line)
    {
        DiagLog.Text += line + Environment.NewLine;
    }

    public void ExportDiagnostics()
    {
        var name = Path.Combine(AppPaths.DataDir, $"diagnostics-{DateTime.Now:yyyyMMdd-HHmmss}.log");
        var header = AppBuildInfo.Summary + Environment.NewLine + new string('=', 60) + Environment.NewLine;
        File.WriteAllText(name, header + (DiagLog.Text ?? ""));
        NexusShell.Log($"export diagnostics → {name}");
    }

    private async Task CopyAllToClipboardAsync()
    {
        var top = TopLevel.GetTopLevel(this);
        if (top?.Clipboard == null)
        {
            NexusShell.Log("Zwischenablage nicht verfügbar.");
            return;
        }

        await top.Clipboard.SetTextAsync(DiagLog.Text ?? "").ConfigureAwait(true);
        NexusShell.Log("Diagnostics: gesamter Log in Zwischenablage.");
    }
}
