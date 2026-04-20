using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using DocumentFormat.OpenXml.Packaging;
using UglyToad.PdfPig;

namespace CarolusNexus.Services;

public static class KnowledgeIndexService
{
    private static readonly string[] TextExt = [".txt", ".md", ".log", ".json", ".csv", ".xml"];
    private const int MaxIndexCharsPerFile = 12 * 1024 * 1024;
    private const int MaxPdfPages = 120;
    private const int ChunkSize = 1200;
    private const int ChunkOverlap = 200;

    public static void Rebuild()
    {
        Directory.CreateDirectory(AppPaths.KnowledgeDir);
        var entries = new List<IndexEntry>();
        var chunks = new List<ChunkEntry>();

        foreach (var path in Directory.GetFiles(AppPaths.KnowledgeDir, "*.*", SearchOption.TopDirectoryOnly).OrderBy(Path.GetFileName))
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            var name = Path.GetFileName(path);
            var fi = new FileInfo(path);

            if (!IsIndexableExtension(ext))
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

            var text = SafeExtractText(path, ext);
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

            if (text.Length > 0)
                chunks.AddRange(SplitChunks(fileName: name, text));
        }

        var doc = new IndexDocument { Version = 2, GeneratedAt = DateTime.UtcNow, Entries = entries };
        File.WriteAllText(AppPaths.KnowledgeIndex,
            JsonSerializer.Serialize(doc, new JsonSerializerOptions { WriteIndented = true }));

        var chunkDoc = new ChunkDocument { Version = 1, GeneratedAt = DateTime.UtcNow, Chunks = chunks };
        File.WriteAllText(AppPaths.KnowledgeChunks,
            JsonSerializer.Serialize(chunkDoc, new JsonSerializerOptions { WriteIndented = true }));

        KnowledgeFtsStore.RebuildFromChunksFile();
    }

    public static bool IsIndexableExtension(string ext) =>
        TextExt.Contains(ext) || ext is ".pdf" or ".docx";

    /// <summary>Text für UI-Vorschau (PDF/DOCX/Plain).</summary>
    public static string ReadDocumentForPreview(string path, int maxChars = 200_000)
    {
        if (!File.Exists(path))
            return "(File missing.)";
        var ext = Path.GetExtension(path).ToLowerInvariant();
        if (!IsIndexableExtension(ext))
            return "(No text preview for this format.)";
        try
        {
            var t = SafeExtractText(path, ext);
            if (t.Length <= maxChars)
                return t;
            return t[..maxChars] + "\n\n…(preview truncated)";
        }
        catch (Exception ex)
        {
            return "Read failed: " + ex.Message;
        }
    }

    private static string SafeExtractText(string path, string ext)
    {
        try
        {
            return ext switch
            {
                ".pdf" => ExtractPdf(path),
                ".docx" => ExtractDocx(path),
                _ => ReadAllTextCapped(path, MaxIndexCharsPerFile)
            };
        }
        catch
        {
            return "";
        }
    }

    private static string ReadAllTextCapped(string path, int maxChars)
    {
        using var sr = new StreamReader(path, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var sb = new StringBuilder();
        var buffer = new char[8192];
        int read;
        while (sb.Length < maxChars && (read = sr.Read(buffer, 0, buffer.Length)) > 0)
        {
            var take = Math.Min(read, maxChars - sb.Length);
            sb.Append(buffer, 0, take);
        }

        return sb.ToString();
    }

    private static string ExtractPdf(string path)
    {
        var sb = new StringBuilder();
        using var pdf = PdfDocument.Open(path);
        var n = 0;
        foreach (var page in pdf.GetPages())
        {
            if (++n > MaxPdfPages)
                break;
            sb.AppendLine(page.Text);
        }

        return sb.ToString();
    }

    private static string ExtractDocx(string path)
    {
        using var doc = WordprocessingDocument.Open(path, false);
        var body = doc.MainDocumentPart?.Document?.Body;
        return body?.InnerText ?? "";
    }

    private static IEnumerable<ChunkEntry> SplitChunks(string fileName, string text)
    {
        var t = text.Replace("\r\n", "\n", StringComparison.Ordinal);
        if (t.Length == 0)
            yield break;

        var i = 0;
        var idx = 0;
        while (i < t.Length)
        {
            var len = Math.Min(ChunkSize, t.Length - i);
            var piece = t.Substring(i, len).Trim();
            if (piece.Length > 40)
                yield return new ChunkEntry { File = fileName, Ordinal = idx++, Text = piece };
            if (t.Length - i <= ChunkSize)
                break;
            i += ChunkSize - ChunkOverlap;
        }
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

    private sealed class ChunkDocument
    {
        [JsonPropertyName("version")]
        public int Version { get; set; }

        [JsonPropertyName("generatedAt")]
        public DateTime GeneratedAt { get; set; }

        [JsonPropertyName("chunks")]
        public List<ChunkEntry> Chunks { get; set; } = new();
    }

    private sealed class ChunkEntry
    {
        [JsonPropertyName("file")]
        public string File { get; set; } = "";

        [JsonPropertyName("ordinal")]
        public int Ordinal { get; set; }

        [JsonPropertyName("text")]
        public string Text { get; set; } = "";
    }
}
