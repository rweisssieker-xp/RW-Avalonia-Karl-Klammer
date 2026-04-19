using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CarolusNexus.Services;

/// <summary>Watch-Modus: periodisch Session-Einträge (Stub-Metadaten + optional Bildschirm-Hash).</summary>
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
    }

    public sealed class WatchDocument
    {
        [JsonPropertyName("version")]
        public int Version { get; set; } = 1;

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

    public static void AppendSnapshot(string note, string? screenHash = null)
    {
        try
        {
            WatchDocument doc;
            if (File.Exists(AppPaths.WatchSessions))
            {
                var json = File.ReadAllText(AppPaths.WatchSessions);
                doc = JsonSerializer.Deserialize<WatchDocument>(json) ?? new WatchDocument();
            }
            else
            {
                doc = new WatchDocument();
            }

            doc.Entries.Add(new WatchEntry
            {
                UtcAt = DateTime.UtcNow,
                Note = note,
                ScreenHash = screenHash
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
}
