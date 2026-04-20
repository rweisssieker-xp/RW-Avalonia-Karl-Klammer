using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CarolusNexus.Services;

/// <summary>Opt-in, dateibasiertes Kurzgedächtnis (JSONL unter data/).</summary>
public static class ConversationMemoryStore
{
    private static readonly object Gate = new();

    private sealed class RowDto
    {
        [JsonPropertyName("at")]
        public DateTime UtcAt { get; set; }

        [JsonPropertyName("role")]
        public string Role { get; set; } = "";

        [JsonPropertyName("text")]
        public string Text { get; set; } = "";
    }

    public static void Append(string role, string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;
        try
        {
            Directory.CreateDirectory(AppPaths.DataDir);
            var row = new RowDto
            {
                UtcAt = DateTime.UtcNow,
                Role = role.Trim().ToLowerInvariant(),
                Text = text.Trim()
            };
            var line = JsonSerializer.Serialize(row);
            lock (Gate)
                File.AppendAllText(AppPaths.ConversationMemory, line + Environment.NewLine);
        }
        catch (Exception ex)
        {
            NexusShell.Log("Conversation memory: " + ex.Message);
        }
    }

    /// <summary>Chronologische Turns; wenn zu lang, werden ältere Zeilen verworfen.</summary>
    public static string BuildPromptBlock(int maxChars)
    {
        if (maxChars < 200)
            maxChars = 200;

        List<RowDto> rows;
        lock (Gate)
        {
            rows = new List<RowDto>();
            if (!File.Exists(AppPaths.ConversationMemory))
                return "";
            foreach (var line in File.ReadAllLines(AppPaths.ConversationMemory))
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;
                try
                {
                    var r = JsonSerializer.Deserialize<RowDto>(line);
                    if (r != null && !string.IsNullOrWhiteSpace(r.Text))
                        rows.Add(r);
                }
                catch
                {
                    /* ignore bad line */
                }
            }
        }

        if (rows.Count == 0)
            return "";

        var sb = new StringBuilder();
        sb.AppendLine("Recent conversation (trimmed, oldest may be dropped):");
        var budget = maxChars - sb.Length;
        var chunks = new List<string>();
        foreach (var r in rows)
        {
            var role = r.Role is "user" or "assistant" ? r.Role : "note";
            var oneLine = r.Text.Replace('\r', ' ').Replace('\n', ' ').Trim();
            var chunk = $"{role}: {oneLine}";
            if (chunk.Length > 2000)
                chunk = chunk[..2000] + "…";
            chunks.Add(chunk);
        }

        var start = 0;
        while (start < chunks.Count)
        {
            sb.Clear();
            sb.AppendLine("Recent conversation (trimmed, oldest may be dropped):");
            for (var i = start; i < chunks.Count; i++)
                sb.AppendLine(chunks[i]);
            if (sb.Length <= maxChars)
                return sb.ToString().TrimEnd();
            start++;
        }

        return "";
    }

    public static void Clear()
    {
        try
        {
            lock (Gate)
            {
                if (File.Exists(AppPaths.ConversationMemory))
                    File.Delete(AppPaths.ConversationMemory);
            }
        }
        catch (Exception ex)
        {
            NexusShell.Log("Conversation memory clear: " + ex.Message);
        }
    }
}
