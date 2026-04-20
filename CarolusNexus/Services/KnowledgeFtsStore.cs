using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CarolusNexus.Models;
using Microsoft.Data.Sqlite;

namespace CarolusNexus.Services;

/// <summary>
/// Lokaler Volltext-Index (SQLite FTS5, BM25) — keine Cloud, keine nativen Vector-Extensions.
/// Wird bei <see cref="KnowledgeIndexService.Rebuild"/> aus <c>knowledge-chunks.json</c> aufgebaut.
/// </summary>
public static class KnowledgeFtsStore
{
    private const int MaxMatchTerms = 32;
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

    public static bool IsReady() => File.Exists(AppPaths.KnowledgeFtsDb);

    /// <summary>Baut <c>knowledge-fts.db</c> aus der Chunk-JSON neu auf.</summary>
    public static void RebuildFromChunksFile()
    {
        try
        {
            if (!File.Exists(AppPaths.KnowledgeChunks))
            {
                TryDeleteDb();
                return;
            }

            var json = File.ReadAllText(AppPaths.KnowledgeChunks);
            var doc = JsonSerializer.Deserialize<ChunkFileDto>(json);
            var chunks = doc?.Chunks;
            if (chunks == null || chunks.Count == 0)
            {
                TryDeleteDb();
                return;
            }

            Directory.CreateDirectory(AppPaths.DataDir);
            var path = AppPaths.KnowledgeFtsDb;
            if (File.Exists(path))
                File.Delete(path);

            using var conn = new SqliteConnection($"Data Source={path};Mode=ReadWriteCreate;Cache=Shared");
            conn.Open();
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = """
                    CREATE VIRTUAL TABLE k_chunks USING fts5(
                      file UNINDEXED,
                      body,
                      tokenize = 'unicode61 remove_diacritics 1'
                    );
                    """;
                cmd.ExecuteNonQuery();
            }

            using var tx = conn.BeginTransaction();
            using (var ins = conn.CreateCommand())
            {
                ins.Transaction = tx;
                ins.CommandText = "INSERT INTO k_chunks(file, body) VALUES ($f, $b);";
                var pF = ins.Parameters.Add("$f", SqliteType.Text);
                var pB = ins.Parameters.Add("$b", SqliteType.Text);
                foreach (var c in chunks)
                {
                    var body = c.Text?.Trim() ?? "";
                    if (body.Length < 8)
                        continue;
                    pF.Value = c.File ?? "?";
                    pB.Value = body;
                    ins.ExecuteNonQuery();
                }
            }

            tx.Commit();
            NexusShell.Log($"Knowledge FTS5: {chunks.Count} chunks → {path}");
        }
        catch (Exception ex)
        {
            NexusShell.Log("Knowledge FTS5 rebuild failed: " + ex.Message);
            TryDeleteDb();
        }
    }

    private static void TryDeleteDb()
    {
        try
        {
            if (File.Exists(AppPaths.KnowledgeFtsDb))
                File.Delete(AppPaths.KnowledgeFtsDb);
        }
        catch
        {
            /* ignore */
        }
    }

    /// <summary>BM25-Ranking über FTS5; <c>null</c> wenn keine DB oder kein Treffer.</summary>
    public static KnowledgeContextBundle? TrySearchBundle(string? userQuery, int maxChars = 12_000)
    {
        if (string.IsNullOrWhiteSpace(userQuery) || !File.Exists(AppPaths.KnowledgeFtsDb))
            return null;

        var terms = Tokenize(userQuery).Where(t => t.Length >= 2).Distinct(StringComparer.OrdinalIgnoreCase).Take(MaxMatchTerms).ToList();
        if (terms.Count == 0)
            return null;

        var match = string.Join(" OR ", terms.Select(EscapeFts5Token));
        try
        {
            using var conn = new SqliteConnection($"Data Source={AppPaths.KnowledgeFtsDb};Mode=ReadOnly;Cache=Shared");
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                SELECT file, body, bm25(k_chunks) AS rank
                FROM k_chunks
                WHERE k_chunks MATCH @m
                ORDER BY rank ASC
                LIMIT 80;
                """;
            cmd.Parameters.AddWithValue("@m", match);

            var rows = new List<(string File, string Body, double Rank)>();
            using (var r = cmd.ExecuteReader())
            {
                while (r.Read())
                {
                    var f = r.GetString(0);
                    var b = r.GetString(1);
                    var rank = r.GetDouble(2);
                    rows.Add((f, b, rank));
                }
            }

            if (rows.Count == 0)
                return null;

            var sb = new StringBuilder();
            var sources = new List<KnowledgeSourceRef>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (file, body, _) in rows)
            {
                var label = file;
                sb.AppendLine("--- " + label + " ---");
                sb.AppendLine(body.Trim());
                sb.AppendLine();
                if (seen.Add(label))
                    sources.Add(new KnowledgeSourceRef(label, KnowledgeSnippetService.TryResolveKnowledgeFilePath(label)));
                if (sb.Length >= maxChars)
                    break;
            }

            var s = sb.ToString();
            var text = s.Length <= maxChars ? s : s[..maxChars] + "\n…(truncated)";
            return new KnowledgeContextBundle(text, sources);
        }
        catch (Exception ex)
        {
            NexusShell.Log("Knowledge FTS5 search: " + ex.Message);
            return null;
        }
    }

    private static string EscapeFts5Token(string term)
    {
        var t = term.Replace("\"", "\"\"", StringComparison.Ordinal);
        return "\"" + t + "\"";
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
}
