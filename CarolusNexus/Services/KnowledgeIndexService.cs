using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CarolusNexus.Services;

public static class KnowledgeIndexService
{
    private static readonly string[] TextExt = [".txt", ".md", ".log", ".json", ".csv", ".xml"];

    public static void Rebuild()
    {
        Directory.CreateDirectory(AppPaths.KnowledgeDir);
        var entries = new List<IndexEntry>();
        foreach (var path in Directory.GetFiles(AppPaths.KnowledgeDir, "*.*", SearchOption.TopDirectoryOnly).OrderBy(Path.GetFileName))
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            var name = Path.GetFileName(path);
            var fi = new FileInfo(path);
            if (!TextExt.Contains(ext))
            {
                entries.Add(new IndexEntry
                {
                    FileName = name,
                    Extension = ext,
                    SizeBytes = fi.Length,
                    Sha256 = HashFile(path),
                    Preview = "",
                    IndexedAt = DateTime.UtcNow
                });
                continue;
            }

            string text;
            try
            {
                text = File.ReadAllText(path);
            }
            catch
            {
                text = "";
            }

            var preview = text.Length > 2000 ? text[..2000] + "…" : text;
            entries.Add(new IndexEntry
            {
                FileName = name,
                Extension = ext,
                SizeBytes = fi.Length,
                Sha256 = HashFile(path),
                CharCount = text.Length,
                Preview = preview,
                IndexedAt = DateTime.UtcNow
            });
        }

        var doc = new IndexDocument { Version = 1, GeneratedAt = DateTime.UtcNow, Entries = entries };
        var json = JsonSerializer.Serialize(doc, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(AppPaths.KnowledgeIndex, json);
    }

    private static string HashFile(string path)
    {
        try
        {
            using var sha = SHA256.Create();
            using var fs = File.OpenRead(path);
            return Convert.ToHexString(sha.ComputeHash(fs));
        }
        catch
        {
            return "";
        }
    }

    private sealed class IndexDocument
    {
        [JsonPropertyName("version")]
        public int Version { get; set; }

        [JsonPropertyName("generatedAt")]
        public DateTime GeneratedAt { get; set; }

        [JsonPropertyName("entries")]
        public List<IndexEntry> Entries { get; set; } = new();
    }

    private sealed class IndexEntry
    {
        [JsonPropertyName("file")]
        public string FileName { get; set; } = "";

        [JsonPropertyName("extension")]
        public string Extension { get; set; } = "";

        [JsonPropertyName("sizeBytes")]
        public long SizeBytes { get; set; }

        [JsonPropertyName("charCount")]
        public int CharCount { get; set; }

        [JsonPropertyName("sha256")]
        public string Sha256 { get; set; } = "";

        [JsonPropertyName("preview")]
        public string Preview { get; set; } = "";

        [JsonPropertyName("indexedAt")]
        public DateTime IndexedAt { get; set; }
    }
}
