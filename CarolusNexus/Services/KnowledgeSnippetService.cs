using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CarolusNexus.Models;

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

    /// <summary>Absoluter oder knowledge\-relativer Dateipfad (für „Quelle öffnen“).</summary>
    public static string? TryResolveKnowledgeFilePath(string? file)
    {
        if (string.IsNullOrWhiteSpace(file))
            return null;
        var t = file.Trim();
        try
        {
            if (Path.IsPathRooted(t) && File.Exists(t))
                return Path.GetFullPath(t);
            var rel = Path.Combine(AppPaths.KnowledgeDir, t);
            return Path.GetFullPath(rel);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Einbindung für Ask: mit <paramref name="userQuery"/> werden Chunks gerankt, sonst linear aus Dateien.</summary>
    public static string BuildContext(string? userQuery, int maxChars = 12000) =>
        BuildContextBundle(userQuery, maxChars).ContextText;

    /// <summary>Wie <see cref="BuildContext"/> plus strukturierte Quellen für die UI.</summary>
    public static KnowledgeContextBundle BuildContextBundle(string? userQuery, int maxChars = 12000)
    {
        if (!Directory.Exists(AppPaths.KnowledgeDir))
            return new KnowledgeContextBundle("", Array.Empty<KnowledgeSourceRef>());

        var q = userQuery?.Trim();
        if (!string.IsNullOrEmpty(q) && File.Exists(AppPaths.KnowledgeChunks))
        {
            var semantic = EmbeddingRagService.TryGetContextBundle(q, maxChars);
            if (semantic != null && !string.IsNullOrWhiteSpace(semantic.ContextText))
                return semantic;

            var ranked = BuildFromChunksBundle(q, maxChars);
            if (!string.IsNullOrWhiteSpace(ranked.ContextText))
                return ranked;
        }

        return BuildSequentialFilesBundle(maxChars);
    }

    /// <summary>Abwärtskompatibel: gesamter Ordnerscan ohne Query.</summary>
    public static string BuildContext(int maxChars) => BuildContext(null, maxChars);

    private static KnowledgeContextBundle BuildFromChunksBundle(string query, int maxChars)
    {
        try
        {
            var json = File.ReadAllText(AppPaths.KnowledgeChunks);
            var doc = JsonSerializer.Deserialize<ChunkFileDto>(json);
            if (doc?.Chunks == null || doc.Chunks.Count == 0)
                return new KnowledgeContextBundle("", Array.Empty<KnowledgeSourceRef>());

            var terms = Tokenize(query).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            if (terms.Length == 0)
                return BuildSequentialFilesBundle(maxChars);

            var scored = new List<(int Score, ChunkDto C)>();
            foreach (var c in doc.Chunks)
            {
                if (string.IsNullOrEmpty(c.Text))
                    continue;
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
                return new KnowledgeContextBundle("", Array.Empty<KnowledgeSourceRef>());

            scored.Sort((a, b) => b.Score.CompareTo(a.Score));

            var sb = new StringBuilder();
            var sources = new List<KnowledgeSourceRef>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (_, c) in scored)
            {
                var label = c.File ?? "?";
                sb.AppendLine("--- " + label + " ---");
                sb.AppendLine(c.Text!.Trim());
                sb.AppendLine();
                if (seen.Add(label))
                    sources.Add(new KnowledgeSourceRef(label, TryResolveKnowledgeFilePath(c.File)));
                if (sb.Length >= maxChars)
                    break;
            }

            var s = sb.ToString();
            var text = s.Length <= maxChars ? s : s[..maxChars] + "\n…(gekürzt)";
            return new KnowledgeContextBundle(text, sources);
        }
        catch
        {
            return new KnowledgeContextBundle("", Array.Empty<KnowledgeSourceRef>());
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

    private static KnowledgeContextBundle BuildSequentialFilesBundle(int maxChars)
    {
        var sb = new StringBuilder();
        var sources = new List<KnowledgeSourceRef>();
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
                var name = Path.GetFileName(path);
                sb.AppendLine("--- " + name + " ---");
                sb.AppendLine(text.Trim());
                sb.AppendLine();
                sources.Add(new KnowledgeSourceRef(name, Path.GetFullPath(path)));
                if (sb.Length >= maxChars)
                    break;
            }
            catch
            {
                /* skip */
            }
        }

        var s = sb.ToString();
        var ctx = s.Length <= maxChars ? s : s[..maxChars] + "\n…(gekürzt)";
        return new KnowledgeContextBundle(ctx, sources);
    }
}
