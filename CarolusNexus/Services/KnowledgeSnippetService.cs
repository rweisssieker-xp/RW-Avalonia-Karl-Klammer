using System;
using System.IO;
using System.Linq;
using System.Text;

namespace CarolusNexus.Services;

/// <summary>Einfache Kontext-Einbindung: Text aus knowledge\ ohne Vektorindex.</summary>
public static class KnowledgeSnippetService
{
    private static readonly string[] TextExtensions = [".txt", ".md", ".log", ".json", ".csv", ".xml"];

    public static string BuildContext(int maxChars = 12000)
    {
        if (!Directory.Exists(AppPaths.KnowledgeDir))
            return "";

        var sb = new StringBuilder();
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
                sb.AppendLine("--- " + Path.GetFileName(path) + " ---");
                sb.AppendLine(text.Trim());
                sb.AppendLine();
                if (sb.Length >= maxChars)
                    break;
            }
            catch
            {
                /* skip */
            }
        }

        var s = sb.ToString();
        if (s.Length <= maxChars)
            return s;
        return s[..maxChars] + "\n…(gekürzt)";
    }
}
