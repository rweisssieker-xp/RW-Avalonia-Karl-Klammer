using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CarolusNexus.Services;

/// <summary>Liest nur Schlüssel aus <c>windows/.env</c> (keine Werte in Logs ausgeben).</summary>
public static class DotEnvSummary
{
    public static IReadOnlyList<string> ListKeys(string path)
    {
        if (!File.Exists(path))
            return Array.Empty<string>();

        var keys = new List<string>();
        foreach (var line in File.ReadAllLines(path))
        {
            var t = line.Trim();
            if (t.Length == 0 || t.StartsWith('#'))
                continue;
            var eq = t.IndexOf('=');
            if (eq > 0)
                keys.Add(t[..eq].Trim());
        }
        return keys.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(k => k).ToList();
    }

    public static bool FileExists => File.Exists(AppPaths.EnvFile);
}
