using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CarolusNexus.Services;

/// <summary>Watch-Modus: periodisch Session-Einträge (Kontext + optional Bildschirm-Hash / Thumbnail).</summary>
public static class WatchSessionService
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public sealed class WatchEntry
    {
        [JsonPropertyName("at")]
        public DateTime UtcAt { get; set; }

        [JsonPropertyName("note")]
        public string Note { get; set; } = "";

        [JsonPropertyName("screenHash")]
        public string? ScreenHash { get; set; }

        [JsonPropertyName("process")]
        public string? ProcessName { get; set; }

        [JsonPropertyName("windowTitle")]
        public string? WindowTitle { get; set; }

        [JsonPropertyName("adapterFamily")]
        public string? AdapterFamily { get; set; }

        /// <summary>Relativ zu <see cref="AppPaths.DataDir"/>, z. B. <c>watch-thumbnails/…</c>.</summary>
        [JsonPropertyName("thumbnailPath")]
        public string? ThumbnailPath { get; set; }

        [JsonPropertyName("source")]
        public string? Source { get; set; }
    }

    public sealed class WatchDocument
    {
        [JsonPropertyName("version")]
        public int Version { get; set; } = 2;

        [JsonPropertyName("entries")]
        public List<WatchEntry> Entries { get; set; } = new();
    }

    public static WatchDocument LoadOrEmpty()
    {
        if (!File.Exists(AppPaths.WatchSessions))
            return new WatchDocument();
        try
        {
            var json = File.ReadAllText(AppPaths.WatchSessions);
            return JsonSerializer.Deserialize<WatchDocument>(json) ?? new WatchDocument();
        }
        catch
        {
            return new WatchDocument();
        }
    }

    public static void AppendSnapshot(
        string note,
        string? screenHash = null,
        string? processName = null,
        string? windowTitle = null,
        string? adapterFamily = null,
        string? thumbnailRelativePath = null,
        string? source = null)
    {
        try
        {
            var doc = LoadOrEmpty();
            if (doc.Version < 2)
                doc.Version = 2;

            doc.Entries.Add(new WatchEntry
            {
                UtcAt = DateTime.UtcNow,
                Note = note,
                ScreenHash = screenHash,
                ProcessName = processName,
                WindowTitle = windowTitle,
                AdapterFamily = adapterFamily,
                ThumbnailPath = thumbnailRelativePath,
                Source = source
            });
            while (doc.Entries.Count > 500)
                doc.Entries.RemoveAt(0);

            File.WriteAllText(AppPaths.WatchSessions, JsonSerializer.Serialize(doc, JsonOpts));
        }
        catch
        {
            // ignore
        }
    }

    public static string FormatDashboardSummary(int maxEntries = 14)
    {
        var doc = LoadOrEmpty();
        if (doc.Entries.Count == 0)
            return "(no watch entries yet — switch to watch mode)";

        var sb = new StringBuilder();
        sb.AppendLine(
            $"Entries: {doc.Entries.Count} · v{doc.Version} · {Path.GetFileName(AppPaths.WatchSessions)}");
        foreach (var e in doc.Entries.Skip(Math.Max(0, doc.Entries.Count - maxEntries)))
        {
            var local = e.UtcAt.ToLocalTime();
            var proc = string.IsNullOrEmpty(e.ProcessName) ? "?" : e.ProcessName;
            var title = e.WindowTitle ?? "";
            if (title.Length > 42)
                title = title[..42] + "…";
            var hash = e.ScreenHash ?? "—";
            var fam = string.IsNullOrEmpty(e.AdapterFamily) ? "" : $" · {e.AdapterFamily}";
            var thumb = string.IsNullOrEmpty(e.ThumbnailPath) ? "" : " · thumb";
            sb.AppendLine($"{local:HH:mm:ss} · {proc}{fam} · „{title}“ · {hash[..Math.Min(8, hash.Length)]}{thumb}");
        }

        return sb.ToString().TrimEnd();
    }

    public static void PruneThumbnails(int maxFiles = 200)
    {
        try
        {
            if (!Directory.Exists(AppPaths.WatchThumbnailsDir))
                return;
            var files = new DirectoryInfo(AppPaths.WatchThumbnailsDir).GetFiles("*.jpg")
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .Skip(Math.Max(0, maxFiles))
                .ToList();
            foreach (var f in files)
            {
                try
                {
                    f.Delete();
                }
                catch
                {
                    // ignore
                }
            }
        }
        catch
        {
            // ignore
        }
    }
}
