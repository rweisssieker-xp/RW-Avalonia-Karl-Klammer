using System;
using System.IO;

namespace CarolusNexus;

public static class AppPaths
{
    /// <summary>Repository-Wurzel (Ordner mit Unterordner <c>windows</c>), sonst Verzeichnis der EXE.</summary>
    public static string RepoRoot { get; private set; } = AppContext.BaseDirectory;

    public static string WindowsDir => Path.Combine(RepoRoot, "windows");
    public static string EnvFile => Path.Combine(WindowsDir, ".env");
    public static string EnvExample => Path.Combine(WindowsDir, ".env.example");
    public static string DataDir => Path.Combine(WindowsDir, "data");
    public static string SettingsFile => Path.Combine(DataDir, "settings.json");
    public static string KnowledgeDir => Path.Combine(DataDir, "knowledge");
    public static string KnowledgeIndex => Path.Combine(DataDir, "knowledge-index.json");
    public static string AutomationRecipes => Path.Combine(DataDir, "automation-recipes.json");
    public static string ActionHistory => Path.Combine(DataDir, "action-history.json");
    public static string WatchSessions => Path.Combine(DataDir, "watch-sessions.json");
    public static string PlaygroundDir => Path.Combine(RepoRoot, "playground");
    public static string CodexOutputDir => Path.Combine(RepoRoot, "codex output");

    public static void EnsureDataTree()
    {
        Directory.CreateDirectory(DataDir);
        Directory.CreateDirectory(KnowledgeDir);
        Directory.CreateDirectory(PlaygroundDir);
        Directory.CreateDirectory(CodexOutputDir);
    }

    public static void DiscoverRepoRoot()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d != null)
        {
            var win = Path.Combine(d.FullName, "windows");
            if (Directory.Exists(win))
            {
                RepoRoot = d.FullName;
                return;
            }
            d = d.Parent;
        }
    }
}
