using System;
using System.Collections.Generic;
using System.Text;

namespace CarolusNexus;

/// <summary>Zentrales Shell-Log: Diagnostics-Tab + kurzer Ringpuffer fürs Dashboard.</summary>
public static class NexusShell
{
    private const int RecentCap = 48;
    private static readonly object RecentLock = new();
    private static readonly Queue<string> Recent = new();

    public static Action<string>? AppendGlobalLog { get; set; }

    public static void Log(string message)
    {
        var line = $"[{DateTime.Now:HH:mm:ss}] {message}";
        lock (RecentLock)
        {
            while (Recent.Count >= RecentCap)
                Recent.Dequeue();
            Recent.Enqueue(line);
        }

        AppendGlobalLog?.Invoke(line);
    }

    public static void LogStub(string action) =>
        Log($"{action} — stub (no provider/automation backend wired).");

    /// <summary>Letzte Zeilen für Dashboard / Kurzkontext (neueste unten).</summary>
    public static string FormatRecentLogForDashboard(int maxLines = 10, int maxTotalChars = 960, int maxLineChars = 132)
    {
        string[] snap;
        lock (RecentLock)
            snap = Recent.ToArray();
        if (snap.Length == 0)
            return "";

        var take = Math.Min(maxLines, snap.Length);
        var start = snap.Length - take;
        var sb = new StringBuilder();
        var total = 0;
        for (var i = start; i < snap.Length; i++)
        {
            var line = snap[i];
            if (line.Length > maxLineChars)
                line = line[..maxLineChars] + "…";
            var add = line.Length + (sb.Length > 0 ? 1 : 0);
            if (total + add > maxTotalChars)
                break;
            if (sb.Length > 0)
            {
                sb.Append('\n');
                total++;
            }

            sb.Append(line);
            total += line.Length;
        }

        return sb.ToString();
    }

    /// <summary>Kompakte letzte Zeilen für LLM-Prompts (eine Zeile pro Eintrag, gekürzt).</summary>
    public static string FormatRecentLogForPrompt(int maxLines = 6, int maxLineChars = 96)
    {
        string[] snap;
        lock (RecentLock)
            snap = Recent.ToArray();
        if (snap.Length == 0)
            return "";

        var take = Math.Min(maxLines, snap.Length);
        var start = snap.Length - take;
        var sb = new StringBuilder();
        for (var i = start; i < snap.Length; i++)
        {
            var line = snap[i].Replace('\r', ' ').Replace('\n', ' ').Trim();
            if (line.Length > maxLineChars)
                line = line[..maxLineChars] + "…";
            if (sb.Length > 0)
                sb.Append(" | ");
            sb.Append(line);
        }

        return sb.ToString();
    }
}
