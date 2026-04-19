using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CarolusNexus.Services;

/// <summary>RAG-light: Chunk-Index (nach Rebuild) + Überlappungs-Scoring gegen Nutzer-Prompt; sonst Dateiscan.</summary>
public static class KnowledgeSnippetService
{
    private static readonly string[] TextExtensions = [".txt", ".md", ".log", ".json", ".csv", ".xml"];
    private static readonly char[] TokenSplit = [' ', '\t', '\n', '\r', '.', ',', ';', ':', '!', '?', '(', ')', '[', ']', '{', '}', '/', '\\', '"', '\'', '-', '_'];

    private sealed class ChunkFileDto
    {
        [JsonPropertyName("chunks")]
        public List<ChunkDto>? Chunks { get; set; }
    }

    private sealed class ChunkDto
    {
        [JsonPropertyName("file")]
        public string? File { get; set; }

        [JsonPropertyName("text")]
        public string? Text { get; set; }
    }

    /// <summary>Einbindung für Ask: mit <paramref name="userQuery"/> werden Chunks gerankt, sonst linear aus Dateien.</summary>
    public static string BuildContext(string? userQuery, int maxChars = 12000)
    {
        if (!Directory.Exists(AppPaths.KnowledgeDir))
            return "";

        var q = userQuery?.Trim();
        if (!string.IsNullOrEmpty(q) && File.Exists(AppPaths.KnowledgeChunks))
        {
            var semantic = EmbeddingRagService.TryGetContext(q, maxChars);
            if (!string.IsNullOrEmpty(semantic))
                return semantic;

            var ranked = BuildFromChunks(q, maxChars);
            if (!string.IsNullOrEmpty(ranked))
                return ranked;
        }

        return BuildSequentialFiles(maxChars);
    }

    /// <summary>Abwärtskompatibel: gesamter Ordnerscan ohne Query.</summary>
    public static string BuildContext(int maxChars) => BuildContext(null, maxChars);

    private static string BuildFromChunks(string query, int maxChars)
    {
        try
        {
            var json = File.ReadAllText(AppPaths.KnowledgeChunks);
            var doc = JsonSerializer.Deserialize<ChunkFileDto>(json);
            if (doc?.Chunks == null || doc.Chunks.Count == 0)
                return "";

            var terms = Tokenize(query).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            if (terms.Length == 0)
                return BuildSequentialFiles(maxChars);

            var scored = new List<(int Score, ChunkDto C)>();
            foreach (var c in doc.Chunks)
            {
                if (string.IsNullOrEmpty(c.Text))
                    continue;
                var tlow = c.Text.AsSpan();
                var score = 0;
                var hay = c.Text;
                foreach (var term in terms)
                {
                    if (term.Length < 2)
                        continue;
                    var idx = 0;
                    while ((idx = hay.IndexOf(term, idx, StringComparison.OrdinalIgnoreCase)) >= 0)
                    {
                        score++;
                        idx += term.Length;
                    }
                }

                if (score > 0)
                    scored.Add((score, c));
            }

            if (scored.Count == 0)
                return "";

            scored.Sort((a, b) => b.Score.CompareTo(a.Score));

            var sb = new StringBuilder();
            foreach (var (_, c) in scored)
            {
                sb.AppendLine("--- " + (c.File ?? "?") + " ---");
                sb.AppendLine(c.Text!.Trim());
                sb.AppendLine();
                if (sb.Length >= maxChars)
                    break;
            }

            var s = sb.ToString();
            return s.Length <= maxChars ? s : s[..maxChars] + "\n…(gekürzt)";
        }
        catch
        {
            return "";
        }
    }

    private static List<string> Tokenize(string text)
    {
        var parts = text.Split(TokenSplit, StringSplitOptions.RemoveEmptyEntries);
        var list = new List<string>();
        foreach (var p in parts)
        {
            var t = p.Trim().ToLowerInvariant();
            if (t.Length >= 2)
                list.Add(t);
        }

        return list;
    }

    private static string BuildSequentialFiles(int maxChars)
    {
        var sb = new StringBuilder();
        foreach (var path in Directory.GetFiles(AppPaths.KnowledgeDir, "*.*", SearchOption.TopDirectoryOnly)
                     .OrderBy(Path.GetFileName))
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (!TextExtensions.Contains(ext))
                continue;
            try
            {
                var text = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(text))
                    continue;
                sb.AppendLine("--- " + Path.GetFileName(path) + " ---");
                sb.AppendLine(text.Trim());
                sb.AppendLine();
                if (sb.Length >= maxChars)
                    break;
            }
            catch
            {
                /* skip */
            }
        }

        var s = sb.ToString();
        if (s.Length <= maxChars)
            return s;
        return s[..maxChars] + "\n…(gekürzt)";
    }
}
