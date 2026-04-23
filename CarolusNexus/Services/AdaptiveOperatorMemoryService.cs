using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CarolusNexus.Services;

public sealed class AdaptiveOperatorMemoryDocument
{
    [JsonPropertyName("version")]
    public int Version { get; set; } = 1;

    [JsonPropertyName("entries")]
    public List<AdaptiveOperatorMemoryEntry> Entries { get; set; } = new();
}

public sealed class AdaptiveOperatorMemoryEntry
{
    [JsonPropertyName("key")]
    public string Key { get; set; } = "";

    [JsonPropertyName("adapterFamily")]
    public string AdapterFamily { get; set; } = "";

    [JsonPropertyName("token")]
    public string Token { get; set; } = "";

    [JsonPropertyName("success")]
    public int Success { get; set; }

    [JsonPropertyName("guarded")]
    public int Guarded { get; set; }

    [JsonPropertyName("unsupported")]
    public int Unsupported { get; set; }

    [JsonPropertyName("error")]
    public int Error { get; set; }

    [JsonPropertyName("lastSeen")]
    public DateTime LastSeenUtc { get; set; }
}

public static class AdaptiveOperatorMemoryService
{
    public static void Record(string adapterFamily, string token, string result)
    {
        try
        {
            Directory.CreateDirectory(AppPaths.DataDir);
            var doc = Load();
            var key = (adapterFamily ?? "generic") + "|" + (token ?? "").Trim();
            var entry = doc.Entries.FirstOrDefault(x => x.Key == key);
            if (entry == null)
            {
                entry = new AdaptiveOperatorMemoryEntry
                {
                    Key = key,
                    AdapterFamily = adapterFamily ?? "generic",
                    Token = token ?? ""
                };
                doc.Entries.Add(entry);
            }

            if (result.StartsWith("[OK]", StringComparison.Ordinal))
                entry.Success++;
            else if (result.StartsWith("[ERR]", StringComparison.Ordinal))
                entry.Error++;
            else if (result.StartsWith("[SKIP]", StringComparison.Ordinal))
                entry.Unsupported++;
            else
                entry.Guarded++;
            entry.LastSeenUtc = DateTime.UtcNow;

            doc.Entries = doc.Entries
                .OrderByDescending(x => x.LastSeenUtc)
                .Take(300)
                .ToList();
            File.WriteAllText(AppPaths.AdaptiveOperatorMemory, JsonSerializer.Serialize(doc, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
            // ignore
        }
    }

    public static string BuildReport(int maxEntries = 20)
    {
        var doc = Load();
        if (doc.Entries.Count == 0)
            return "(no adaptive operator memory yet)";
        var sb = new StringBuilder();
        sb.AppendLine("Adaptive operator memory");
        foreach (var e in doc.Entries.OrderByDescending(x => x.LastSeenUtc).Take(maxEntries))
        {
            sb.AppendLine($"{e.LastSeenUtc.ToLocalTime():yyyy-MM-dd HH:mm:ss} · {e.AdapterFamily}");
            sb.AppendLine($"  token: {e.Token}");
            sb.AppendLine($"  success={e.Success}; guarded={e.Guarded}; unsupported={e.Unsupported}; error={e.Error}");
        }

        return sb.ToString().TrimEnd();
    }

    private static AdaptiveOperatorMemoryDocument Load()
    {
        if (!File.Exists(AppPaths.AdaptiveOperatorMemory))
            return new AdaptiveOperatorMemoryDocument();
        try
        {
            return JsonSerializer.Deserialize<AdaptiveOperatorMemoryDocument>(File.ReadAllText(AppPaths.AdaptiveOperatorMemory))
                   ?? new AdaptiveOperatorMemoryDocument();
        }
        catch
        {
            return new AdaptiveOperatorMemoryDocument();
        }
    }
}
