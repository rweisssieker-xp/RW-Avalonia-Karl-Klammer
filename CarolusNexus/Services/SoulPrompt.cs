using System.IO;

namespace CarolusNexus.Services;

public static class SoulPrompt
{
    public static string LoadOrDefault()
    {
        var p = Path.Combine(AppPaths.RepoRoot, "SOUL.md");
        if (!File.Exists(p))
            return "You are Karl Klammer, a friendly assistant.";
        try
        {
            var t = File.ReadAllText(p);
            return string.IsNullOrWhiteSpace(t) ? "You are Karl Klammer." : t.Trim();
        }
        catch
        {
            return "You are Karl Klammer.";
        }
    }
}
