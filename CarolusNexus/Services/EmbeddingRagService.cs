using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using CarolusNexus.Models;

namespace CarolusNexus.Services;

/// <summary>OpenAI-kompatible Embedding-Suche über knowledge-chunks.json (KI-RAG).</summary>
public static class EmbeddingRagService
{
    private static readonly SemaphoreSlim RebuildGate = new(1, 1);

    public static bool IsEmbeddingIndexReady() =>
        File.Exists(AppPaths.KnowledgeEmbeddings) && File.Exists(AppPaths.KnowledgeChunks);

    public static string TryGetContext(string userQuery, int maxChars = 12_000) =>
        TryGetContextBundle(userQuery, maxChars)?.ContextText ?? "";

    /// <summary>Semantische RAG mit Quellenliste für die UI; <c>null</c> wenn nicht anwendbar oder kein Treffer.</summary>
    public static KnowledgeContextBundle? TryGetContextBundle(string userQuery, int maxChars = 12_000)
    {
        if (string.IsNullOrWhiteSpace(userQuery) || !IsEmbeddingIndexReady())
            return null;

        var env = DotEnvStore.Load();
        if (!env.TryGetValue("OPENAI_API_KEY", out var key) || string.IsNullOrWhiteSpace(key))
            return null;
        if (env.TryGetValue("RAG_EMBEDDINGS", out var ragOff))
        {
            var r = ragOff.Trim();
            if (r.Equals("0", StringComparison.OrdinalIgnoreCase) ||
                r.Equals("false", StringComparison.OrdinalIgnoreCase))
                return null;
        }

        try
        {
            var doc = JsonSerializer.Deserialize<EmbeddingFileDto>(
                File.ReadAllText(AppPaths.KnowledgeEmbeddings),
                JsonOpts());
            if (doc?.Items == null || doc.Items.Count == 0)
                return null;

            var chunksJson = File.ReadAllText(AppPaths.KnowledgeChunks);
            var fp = Fingerprint(chunksJson);
            if (!string.IsNullOrEmpty(doc.SourceFingerprint) && doc.SourceFingerprint != fp)
                return null;

            var chunkTexts = LoadChunkLookup(chunksJson);
            var model = env.TryGetValue("OPENAI_EMBEDDING_MODEL", out var m) && !string.IsNullOrWhiteSpace(m)
                ? m.Trim()
                : "text-embedding-3-small";
            var baseUrl = env.TryGetValue("OPENAI_BASE_URL", out var bu) && !string.IsNullOrWhiteSpace(bu)
                ? bu.TrimEnd('/')
                : "https://api.openai.com/v1";

            var qVec = LlmEmbeddingClient.EmbedBatchAsync(
                new[] { userQuery },
                model,
                baseUrl,
                key,
                CancellationToken.None).GetAwaiter().GetResult();
            if (qVec.Count == 0)
                return null;
            var q = qVec[0];

            var scored = new List<(double Sim, EmbeddingItemDto It)>();
            foreach (var it in doc.Items)
            {
                if (it.V == null || it.V.Length == 0)
                    continue;
                var sim = Cosine(q, it.V);
                scored.Add((sim, it));
            }

            if (scored.Count == 0)
                return null;

            scored.Sort((a, b) => b.Sim.CompareTo(a.Sim));
            var topK = int.TryParse(env.TryGetValue("RAG_TOP_K", out var tk) ? tk : null, out var kParsed)
                ? Math.Clamp(kParsed, 1, 24)
                : 8;

            var sb = new StringBuilder();
            sb.AppendLine("(RAG: semantische Chunk-Suche)");
            var used = 0;
            var sources = new List<KnowledgeSourceRef>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (_, it) in scored.Take(topK))
            {
                if (!chunkTexts.TryGetValue((it.File ?? "", it.Ordinal), out var text) || string.IsNullOrEmpty(text))
                    continue;
                sb.AppendLine("--- " + it.File + " [#" + it.Ordinal + "] ---");
                sb.AppendLine(text.Trim());
                sb.AppendLine();
                var label = (it.File ?? "?") + " [#" + it.Ordinal + "]";
                if (seen.Add(label))
                    sources.Add(new KnowledgeSourceRef(label, KnowledgeSnippetService.TryResolveKnowledgeFilePath(it.File)));
                used++;
                if (sb.Length >= maxChars)
                    break;
            }

            if (used == 0)
                return null;
            var s = sb.ToString();
            var ctx = s.Length <= maxChars ? s : s[..maxChars] + "\n…(gekürzt)";
            return new KnowledgeContextBundle(ctx, sources);
        }
        catch (Exception ex)
        {
            NexusShell.Log("Embedding-RAG: " + ex.Message);
            return null;
        }
    }

    public static async Task RebuildIfConfiguredAsync(CancellationToken ct)
    {
        if (!await RebuildGate.WaitAsync(0, ct).ConfigureAwait(false))
        {
            NexusShell.Log("Embedding-Rebuild übersprungen (läuft bereits).");
            return;
        }

        try
        {
            await RebuildCoreAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            RebuildGate.Release();
        }
    }

    private static async Task RebuildCoreAsync(CancellationToken ct)
    {
        var env = DotEnvStore.Load();
        if (!env.TryGetValue("OPENAI_API_KEY", out var key) || string.IsNullOrWhiteSpace(key))
        {
            NexusShell.Log("Embeddings: kein OPENAI_API_KEY — semantisches RAG deaktiviert.");
            return;
        }

        if (env.TryGetValue("RAG_EMBEDDINGS", out var rag) &&
            (rag.Trim().Equals("0", StringComparison.OrdinalIgnoreCase) ||
             rag.Trim().Equals("false", StringComparison.OrdinalIgnoreCase)))
        {
            NexusShell.Log("Embeddings: RAG_EMBEDDINGS=0 — übersprungen.");
            return;
        }

        if (!File.Exists(AppPaths.KnowledgeChunks))
        {
            NexusShell.Log("Embeddings: knowledge-chunks.json fehlt — zuerst reindex.");
            return;
        }

        var chunksJson = await File.ReadAllTextAsync(AppPaths.KnowledgeChunks, ct).ConfigureAwait(false);
        var fp = Fingerprint(chunksJson);
        using var chunkDoc = JsonDocument.Parse(chunksJson);
        if (!chunkDoc.RootElement.TryGetProperty("chunks", out var chEl) || chEl.ValueKind != JsonValueKind.Array)
            return;

        var rows = new List<(string File, int Ordinal, string Text)>();
        foreach (var c in chEl.EnumerateArray())
        {
            var f = c.TryGetProperty("file", out var fe) ? fe.GetString() ?? "" : "";
            var ord = c.TryGetProperty("ordinal", out var oe) ? oe.GetInt32() : 0;
            var tx = c.TryGetProperty("text", out var te) ? te.GetString() ?? "" : "";
            if (f.Length > 0 && tx.Length > 20)
                rows.Add((f, ord, tx));
        }

        var max = 800;
        if (int.TryParse(env.TryGetValue("MAX_EMBED_CHUNKS", out var mc) ? mc : null, out var mp))
            max = Math.Clamp(mp, 50, 4000);
        if (rows.Count > max)
            rows = rows.Take(max).ToList();

        var model = env.TryGetValue("OPENAI_EMBEDDING_MODEL", out var m) && !string.IsNullOrWhiteSpace(m)
            ? m.Trim()
            : "text-embedding-3-small";
        var baseUrl = env.TryGetValue("OPENAI_BASE_URL", out var bu) && !string.IsNullOrWhiteSpace(bu)
            ? bu.TrimEnd('/')
            : "https://api.openai.com/v1";

        NexusShell.Log($"Embeddings: {rows.Count} Chunks · Modell {model} …");
        var items = new List<EmbeddingItemDto>();
        const int batch = 24;
        for (var i = 0; i < rows.Count; i += batch)
        {
            ct.ThrowIfCancellationRequested();
            var slice = rows.Skip(i).Take(batch).ToList();
            var texts = slice.Select(s => s.Text).ToList();
            var vecs = await LlmEmbeddingClient.EmbedBatchAsync(texts, model, baseUrl, key, ct)
                .ConfigureAwait(false);
            for (var j = 0; j < slice.Count; j++)
            {
                items.Add(new EmbeddingItemDto
                {
                    File = slice[j].File,
                    Ordinal = slice[j].Ordinal,
                    V = vecs[j]
                });
            }
        }

        var outDoc = new EmbeddingFileDto
        {
            Version = 1,
            Model = model,
            GeneratedAt = DateTime.UtcNow,
            SourceFingerprint = fp,
            Items = items
        };
        await File.WriteAllTextAsync(
            AppPaths.KnowledgeEmbeddings,
            JsonSerializer.Serialize(outDoc, JsonOpts()),
            ct).ConfigureAwait(false);
        NexusShell.Log($"Embeddings: gespeichert → knowledge-embeddings.json ({items.Count} Vektoren).");
    }

    private static Dictionary<(string File, int Ordinal), string> LoadChunkLookup(string chunksJson)
    {
        var d = new Dictionary<(string, int), string>();
        using var doc = JsonDocument.Parse(chunksJson);
        if (!doc.RootElement.TryGetProperty("chunks", out var ch) || ch.ValueKind != JsonValueKind.Array)
            return d;
        foreach (var c in ch.EnumerateArray())
        {
            var f = c.TryGetProperty("file", out var fe) ? fe.GetString() ?? "" : "";
            var ord = c.TryGetProperty("ordinal", out var oe) ? oe.GetInt32() : 0;
            var tx = c.TryGetProperty("text", out var te) ? te.GetString() ?? "" : "";
            if (f.Length > 0)
                d[(f, ord)] = tx;
        }

        return d;
    }

    private static string Fingerprint(string content)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(bytes);
    }

    private static double Cosine(float[] a, float[] b)
    {
        if (a.Length != b.Length || a.Length == 0)
            return 0;
        double dot = 0, na = 0, nb = 0;
        for (var i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            na += a[i] * a[i];
            nb += b[i] * b[i];
        }

        var den = Math.Sqrt(na) * Math.Sqrt(nb);
        return den < 1e-9 ? 0 : dot / den;
    }

    private static JsonSerializerOptions JsonOpts() => new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private sealed class EmbeddingFileDto
    {
        [JsonPropertyName("version")]
        public int Version { get; set; }

        [JsonPropertyName("model")]
        public string? Model { get; set; }

        [JsonPropertyName("generatedAt")]
        public DateTime GeneratedAt { get; set; }

        [JsonPropertyName("sourceFingerprint")]
        public string? SourceFingerprint { get; set; }

        [JsonPropertyName("items")]
        public List<EmbeddingItemDto>? Items { get; set; }
    }

    private sealed class EmbeddingItemDto
    {
        [JsonPropertyName("file")]
        public string? File { get; set; }

        [JsonPropertyName("ordinal")]
        public int Ordinal { get; set; }

        [JsonPropertyName("v")]
        public float[]? V { get; set; }
    }
}
