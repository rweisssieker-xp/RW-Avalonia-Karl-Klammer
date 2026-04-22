using System;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using CarolusNexus.Models;

namespace CarolusNexus.Services;

public static class AuditExportPackageService
{
    public static string Export(AutomationRecipe? recipe, NexusSettings settings)
    {
        Directory.CreateDirectory(AppPaths.DataDir);
        var zip = Path.Combine(AppPaths.DataDir, $"audit-package-{DateTime.Now:yyyyMMdd-HHmmss}.zip");
        if (File.Exists(zip))
            File.Delete(zip);

        using var archive = ZipFile.Open(zip, ZipArchiveMode.Create);
        AddText(archive, "diagnostics.txt", RuntimeDiagnosticsService.BuildReport(settings));
        AddIfExists(archive, AppPaths.ActionHistory, "action-history.json");
        AddIfExists(archive, AppPaths.RitualStepAudit, "ritual-step-audit.jsonl");
        AddIfExists(archive, AppPaths.RitualJobQueue, "ritual-job-queue.json");
        AddIfExists(archive, AppPaths.WatchSessions, "watch-sessions.json");
        if (recipe != null)
        {
            AddText(archive, "flow.json", JsonSerializer.Serialize(recipe, new JsonSerializerOptions { WriteIndented = true }));
            AddText(archive, "flow-test-report.txt", FlowTestStudioService.BuildReport(recipe, settings));
        }
        return zip;
    }

    private static void AddIfExists(ZipArchive archive, string path, string name)
    {
        if (File.Exists(path))
            archive.CreateEntryFromFile(path, name, CompressionLevel.Optimal);
    }

    private static void AddText(ZipArchive archive, string name, string text)
    {
        var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
        using var sw = new StreamWriter(entry.Open());
        sw.Write(text);
    }
}
