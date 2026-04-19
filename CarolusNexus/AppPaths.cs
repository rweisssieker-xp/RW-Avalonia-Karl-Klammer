using System;
using System.IO;

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
    public static string KnowledgeEmbeddings => Path.Combine(DataDir, "knowledge-embeddings.json");
    public static string AutomationRecipes => Path.Combine(DataDir, "automation-recipes.json");
    public static string RitualJobQueue => Path.Combine(DataDir, "ritual-job-queue.json");
    public static string ActionHistory => Path.Combine(DataDir, "action-history.json");
    public static string WatchSessions => Path.Combine(DataDir, "watch-sessions.json");
    public static string RitualStepAudit => Path.Combine(DataDir, "ritual-step-audit.jsonl");
    public static string PlaygroundDir => Path.Combine(RepoRoot, "playground");
    public static string CodexOutputDir => Path.Combine(RepoRoot, "codex output");

    public static void EnsureDataTree()
    {
        Directory.CreateDirectory(DataDir);
        Directory.CreateDirectory(KnowledgeDir);
        Directory.CreateDirectory(PlaygroundDir);
        Directory.CreateDirectory(CodexOutputDir);
    }

    /// <summary>
    /// Findet die Repo-Wurzel mit <c>windows/</c>: nicht <c>C:\Windows</c>, nicht nur <c>bin/.../windows</c>.
    /// Priorität: Vorfahr mit <c>CarolusNexus/CarolusNexus.csproj</c> → Vorfahr mit <c>windows/.env.example</c>
    /// (äußerster Treffer) → äußerster <c>windows</c>-Ordner.
    /// </summary>
    public static void DiscoverRepoRoot()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d != null)
        {
            if (IsRepoWindowsDir(d.FullName, out var winPath)
                && File.Exists(Path.Combine(d.FullName, "CarolusNexus", "CarolusNexus.csproj")))
            {
                RepoRoot = d.FullName;
                return;
            }

            d = d.Parent;
        }

        DirectoryInfo? withEnvExample = null;
        DirectoryInfo? anyWindows = null;
        d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d != null)
        {
            if (IsRepoWindowsDir(d.FullName, out _))
            {
                anyWindows = d;
                if (File.Exists(Path.Combine(d.FullName, "windows", ".env.example")))
                    withEnvExample = d;
            }

            d = d.Parent;
        }

        if (withEnvExample != null)
        {
            RepoRoot = withEnvExample.FullName;
            return;
        }

        if (anyWindows != null)
            RepoRoot = anyWindows.FullName;
    }

    private static bool IsRepoWindowsDir(string ancestor, out string winPath)
    {
        winPath = Path.GetFullPath(Path.Combine(ancestor, "windows"));
        return Directory.Exists(winPath)
               && !string.Equals(winPath, SystemWindowsDir, StringComparison.OrdinalIgnoreCase);
    }
}
