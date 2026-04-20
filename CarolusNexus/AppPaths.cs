using System;
using System.IO;
using System.Linq;

namespace CarolusNexus;

public static class AppPaths
{
    private static readonly string SystemWindowsDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
    /// <summary>Repository-Wurzel (Ordner mit Unterordner <c>windows</c>), sonst Verzeichnis der EXE.</summary>
    public static string RepoRoot { get; private set; } = AppContext.BaseDirectory;

    public static string WindowsDir => Path.Combine(RepoRoot, "windows");
    public static string EnvFile => Path.Combine(WindowsDir, ".env");
    public static string EnvExample => Path.Combine(WindowsDir, ".env.example");
    public static string DataDir => Path.Combine(WindowsDir, "data");
    public static string SettingsFile => Path.Combine(DataDir, "settings.json");
    public static string KnowledgeDir => Path.Combine(DataDir, "knowledge");
    public static string KnowledgeIndex => Path.Combine(DataDir, "knowledge-index.json");
    public static string KnowledgeChunks => Path.Combine(DataDir, "knowledge-chunks.json");
    /// <summary>SQLite FTS5 Volltextindex (lokal, nach Reindex aus <see cref="KnowledgeChunks"/>).</summary>
    public static string KnowledgeFtsDb => Path.Combine(DataDir, "knowledge-fts.db");
    public static string KnowledgeEmbeddings => Path.Combine(DataDir, "knowledge-embeddings.json");
    public static string AutomationRecipes => Path.Combine(DataDir, "automation-recipes.json");
    public static string RitualJobQueue => Path.Combine(DataDir, "ritual-job-queue.json");
    public static string ActionHistory => Path.Combine(DataDir, "action-history.json");
    public static string WatchSessions => Path.Combine(DataDir, "watch-sessions.json");
    /// <summary>JPEG thumbnails for watch entries (under <see cref="DataDir"/>).</summary>
    public static string WatchThumbnailsDir => Path.Combine(DataDir, "watch-thumbnails");
    public static string RitualStepAudit => Path.Combine(DataDir, "ritual-step-audit.jsonl");
    public static string ConversationMemory => Path.Combine(DataDir, "conversation-memory.jsonl");
    /// <summary>Recent command palette entry ids (<c>tab:N</c> / <c>action:key</c>).</summary>
    public static string CommandPaletteRecent => Path.Combine(DataDir, "command-palette-recent.json");
    public static string PlaygroundDir => Path.Combine(RepoRoot, "playground");
    public static string CodexOutputDir => Path.Combine(RepoRoot, "codex output");

    /// <summary>Optional: Plugin-DLLs (IOperatorAdapter).</summary>
    public static string PluginsDir => Path.Combine(WindowsDir, "plugins");

    public static void EnsureDataTree()
    {
        Directory.CreateDirectory(DataDir);
        Directory.CreateDirectory(KnowledgeDir);
        Directory.CreateDirectory(WatchThumbnailsDir);
        Directory.CreateDirectory(PlaygroundDir);
        Directory.CreateDirectory(CodexOutputDir);
        Directory.CreateDirectory(PluginsDir);
    }

    /// <summary>
    /// Findet die Repo-Wurzel mit <c>windows/</c>: nicht <c>C:\Windows</c>, nicht <c>bin/obj/.../windows</c> vom Build.
    /// Bewertung: CarolusNexus.csproj, <c>windows/.env.example</c>, <c>windows/.env</c>; starke Abwertung unter <c>bin</c>/<c>obj</c>.
    /// </summary>
    public static void DiscoverRepoRoot()
    {
        var bestPath = "";
        var bestScore = int.MinValue;
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d != null; d = d.Parent)
        {
            if (!IsRepoWindowsDir(d.FullName, out _))
                continue;

            var score = ScoreRepoCandidate(d.FullName);
            if (score > bestScore)
            {
                bestScore = score;
                bestPath = d.FullName;
            }
        }

        if (bestScore > int.MinValue && !string.IsNullOrEmpty(bestPath))
            RepoRoot = bestPath;
    }

    private static int ScoreRepoCandidate(string ancestor)
    {
        var win = Path.Combine(ancestor, "windows");
        var score = 0;
        if (PathContainsBuildSegment(ancestor))
            score -= 10_000;
        if (File.Exists(Path.Combine(ancestor, "CarolusNexus", "CarolusNexus.csproj")))
            score += 1_000;
        if (File.Exists(Path.Combine(ancestor, "CarolusNexus.WinUI", "CarolusNexus.WinUI.csproj")))
            score += 1_000;
        if (File.Exists(Path.Combine(win, ".env.example")))
            score += 100;
        if (File.Exists(Path.Combine(win, ".env")))
            score += 50;
        return score;
    }

    private static bool PathContainsBuildSegment(string fullPath)
    {
        try
        {
            var parts = fullPath.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                StringSplitOptions.RemoveEmptyEntries);
            return parts.Contains("bin", StringComparer.OrdinalIgnoreCase)
                   || parts.Contains("obj", StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static bool IsRepoWindowsDir(string ancestor, out string winPath)
    {
        winPath = Path.GetFullPath(Path.Combine(ancestor, "windows"));
        return Directory.Exists(winPath)
               && !string.Equals(winPath, SystemWindowsDir, StringComparison.OrdinalIgnoreCase);
    }
}
