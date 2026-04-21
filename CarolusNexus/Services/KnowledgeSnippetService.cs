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
    public static KnowledgeContextBundle BuildContextBundle(string? userQuery, int maxChars = 12000) =>
        BuildAugmentationResult(userQuery, maxChars).Bundle;

    /// <summary>Wie <see cref="BuildContextBundle"/> plus Retrieval-Tier und Hinweise für transparente „KI“-Anzeige (§7 Phase A).</summary>
    public static KnowledgeAugmentationResult BuildAugmentationResult(string? userQuery, int maxChars = 12000)
    {
        var hints = new List<string>();
        if (!Directory.Exists(AppPaths.KnowledgeDir))
        {
            hints.Add("Knowledge folder missing — add documents under the configured knowledge directory (see AppPaths / windows/knowledge).");
            return new KnowledgeAugmentationResult(
                new KnowledgeContextBundle("", Array.Empty<KnowledgeSourceRef>()),
                KnowledgeRetrievalTier.None,
                hints);
        }

        var q = userQuery?.Trim();
        if (string.IsNullOrEmpty(q))
        {
            hints.Add("No search query — including excerpts from the first text files in the knowledge folder (sequential scan).");
            return new KnowledgeAugmentationResult(
                BuildSequentialFilesBundle(maxChars),
                KnowledgeRetrievalTier.SequentialFiles,
                hints);
        }

        if (!File.Exists(AppPaths.KnowledgeChunks))
        {
            hints.Add("No knowledge-chunks.json — run a Knowledge reindex from the Knowledge tab (or main window) to enable chunk search.");
            hints.Add("Until then, excerpts come from a sequential file scan (not ranked by your prompt).");
            return new KnowledgeAugmentationResult(
                BuildSequentialFilesBundle(maxChars),
                KnowledgeRetrievalTier.SequentialFiles,
                hints);
        }

        AppendSemanticRagHints(hints);

        var semantic = EmbeddingRagService.TryGetContextBundle(q, maxChars);
        if (semantic != null && !string.IsNullOrWhiteSpace(semantic.ContextText))
        {
            hints.Clear();
            hints.Add("Using semantic embedding RAG (knowledge-embeddings.json).");
            return new KnowledgeAugmentationResult(semantic, KnowledgeRetrievalTier.SemanticEmbedding, hints);
        }

        if (hints.Count == 0)
            hints.Add("Semantic RAG did not return chunks — falling back to keyword search.");

        var fts = KnowledgeFtsStore.TrySearchBundle(q, maxChars);
        if (fts != null && !string.IsNullOrWhiteSpace(fts.ContextText))
        {
            hints.Add("Using local FTS5 keyword index (not vector similarity).");
            return new KnowledgeAugmentationResult(fts, KnowledgeRetrievalTier.Fts, hints);
        }

        var ranked = BuildFromChunksBundle(q, maxChars);
        if (!string.IsNullOrWhiteSpace(ranked.ContextText))
        {
            hints.Add("Using overlap-ranked chunks from knowledge-chunks.json (no FTS hit).");
            return new KnowledgeAugmentationResult(ranked, KnowledgeRetrievalTier.KeywordChunks, hints);
        }

        hints.Add("No chunk matched your query — showing sequential file excerpts.");
        return new KnowledgeAugmentationResult(
            BuildSequentialFilesBundle(maxChars),
            KnowledgeRetrievalTier.SequentialFiles,
            hints);
    }

    /// <summary>Human-readable block for Ask „Retrieval + context“ pane (tier, hints, then excerpts).</summary>
    public static string FormatAugmentationForAskPanel(KnowledgeAugmentationResult aug)
    {
        var sb = new StringBuilder();
        sb.Append("[Retrieval · ").Append(TierDisplayName(aug.Tier)).Append(']');
        foreach (var h in aug.Hints)
            sb.AppendLine().Append("· ").Append(h);
        if (!string.IsNullOrWhiteSpace(aug.Bundle.ContextText))
        {
            sb.AppendLine().AppendLine();
            sb.Append(aug.Bundle.ContextText.TrimEnd());
        }
        else
            sb.AppendLine().Append("(no local excerpts)");

        return sb.ToString();
    }

    private static string TierDisplayName(KnowledgeRetrievalTier t) =>
        t switch
        {
            KnowledgeRetrievalTier.SemanticEmbedding => "semantic embeddings",
            KnowledgeRetrievalTier.Fts => "FTS keywords",
            KnowledgeRetrievalTier.KeywordChunks => "keyword overlap",
            KnowledgeRetrievalTier.SequentialFiles => "sequential files",
            _ => "none"
        };

    private static void AppendSemanticRagHints(ICollection<string> hints)
    {
        if (!File.Exists(AppPaths.KnowledgeEmbeddings))
        {
            hints.Add("Semantic RAG: knowledge-embeddings.json missing — rebuild embeddings (requires OPENAI_API_KEY) after chunks exist.");
            return;
        }

        var env = DotEnvStore.Load();
        if (!env.TryGetValue("OPENAI_API_KEY", out var key) || string.IsNullOrWhiteSpace(key))
        {
            hints.Add("Semantic RAG: OPENAI_API_KEY not set in environment — cannot run embedding search.");
            return;
        }

        if (env.TryGetValue("RAG_EMBEDDINGS", out var ragOff) &&
            (ragOff.Trim().Equals("0", StringComparison.OrdinalIgnoreCase) ||
             ragOff.Trim().Equals("false", StringComparison.OrdinalIgnoreCase)))
        {
            hints.Add("Semantic RAG: disabled by RAG_EMBEDDINGS=0/false.");
        }
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
            var text = s.Length <= maxChars ? s : s[..maxChars] + "\n…(truncated)";
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
        var ctx = s.Length <= maxChars ? s : s[..maxChars] + "\n…(truncated)";
        return new KnowledgeContextBundle(ctx, sources);
    }
}
