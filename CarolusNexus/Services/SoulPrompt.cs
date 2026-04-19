using System.IO;

namespace CarolusNexus.Services;

public static class SoulPrompt
{
    public static string LoadOrDefault()
    {
        var p = Path.Combine(AppPaths.RepoRoot, "SOUL.md");
        if (!File.Exists(p))
            return "Du bist Karl Klammer, ein freundlicher Assistent.";
        try
        {
            var t = File.ReadAllText(p);
            return string.IsNullOrWhiteSpace(t) ? "Du bist Karl Klammer." : t.Trim();
        }
        catch
        {
            return "Du bist Karl Klammer.";
        }
    }
}
