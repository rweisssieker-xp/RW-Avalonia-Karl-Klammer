using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using CarolusNexus;
using CarolusNexus.Services;

namespace CarolusNexus.Views;

public partial class DiagnosticsTab : UserControl
{
    public DiagnosticsTab()
    {
        InitializeComponent();
        BtnExport.Click += (_, _) => ExportDiagnostics();
        BtnCopy.Click += async (_, _) => await CopyAllToClipboardAsync();
        BtnClear.Click += (_, _) => { DiagLog.Text = ""; NexusShell.Log("Logs cleared (local)."); };
        BtnAudit.Click += (_, _) => LoadRitualAuditTail();
        BtnRuntimeReport.Click += (_, _) => LoadRuntimeReport();
        BtnAuditPackage.Click += (_, _) => ExportAuditPackage();
    }

    private void LoadRitualAuditTail()
    {
        if (!File.Exists(AppPaths.RitualStepAudit))
        {
            NexusShell.Log("ritual-step-audit.jsonl does not exist yet.");
            DiagLog.Text = "(no audit — run a plan/ritual with steps first)";
            return;
        }

        try
        {
            var lines = File.ReadAllLines(AppPaths.RitualStepAudit);
            var tail = lines.Length <= 120 ? lines : lines.Skip(lines.Length - 120).ToArray();
            DiagLog.Text = string.Join(Environment.NewLine, tail);
            NexusShell.Log($"Diagnostics: ritual audit, last {tail.Length} lines.");
        }
        catch (Exception ex)
        {
            NexusShell.Log("Read ritual audit: " + ex.Message);
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

    private void LoadRuntimeReport()
    {
        var settings = NexusContext.GetSettings?.Invoke() ?? new CarolusNexus.Models.NexusSettings();
        DiagLog.Text = RuntimeDiagnosticsService.BuildReport(settings);
        var path = RuntimeDiagnosticsService.SaveReport(settings);
        NexusShell.Log("Diagnostics runtime report → " + path);
    }

    private void ExportAuditPackage()
    {
        var settings = NexusContext.GetSettings?.Invoke() ?? new CarolusNexus.Models.NexusSettings();
        var path = AuditExportPackageService.Export(null, settings);
        NexusShell.Log("Audit package → " + path);
        DiagLog.Text = "Audit package exported:\n" + path + "\n\n" + RuntimeDiagnosticsService.BuildReport(settings);
    }

    private async Task CopyAllToClipboardAsync()
    {
        var top = TopLevel.GetTopLevel(this);
        if (top?.Clipboard == null)
        {
            NexusShell.Log("Clipboard not available.");
            return;
        }

        await top.Clipboard.SetTextAsync(DiagLog.Text ?? "").ConfigureAwait(true);
        NexusShell.Log("Diagnostics: full log copied to clipboard.");
    }
}
