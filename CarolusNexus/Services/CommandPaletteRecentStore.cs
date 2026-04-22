using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CarolusNexus.Services;

/// <summary>Last-used command palette entry ids (tab:/action:) for ordering and "recent" surfacing.</summary>
public static class CommandPaletteRecentStore
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    private sealed class RecentDoc
    {
        [JsonPropertyName("recent")]
        public List<string> Recent { get; set; } = new();
    }

    public static IReadOnlyList<string> Load()
    {
        try
        {
            var path = AppPaths.CommandPaletteRecent;
            if (!File.Exists(path))
                return [];
            var json = File.ReadAllText(path);
            var doc = JsonSerializer.Deserialize<RecentDoc>(json);
            return doc?.Recent?.Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().Take(16).ToList() ?? [];
        }
        catch
        {
            return [];
        }
    }

    public static void Touch(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return;
        try
        {
            var list = Load().ToList();
            list.RemoveAll(x => string.Equals(x, id, StringComparison.Ordinal));
            list.Insert(0, id);
            while (list.Count > 16)
                list.RemoveAt(list.Count - 1);
            var path = AppPaths.CommandPaletteRecent;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(new RecentDoc { Recent = list }, JsonOpts));
        }
        catch
        {
            /* ignore */
        }
    }

    public static void Clear()
    {
        try
        {
            var path = AppPaths.CommandPaletteRecent;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(new RecentDoc { Recent = new List<string>() }, JsonOpts));
        }
        catch
        {
            /* ignore */
        }
    }
}
