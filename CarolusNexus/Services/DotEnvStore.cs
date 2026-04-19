using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CarolusNexus.Services;

/// <summary>Liest windows/.env in ein Dictionary (Werte nur im RAM, nie loggen).</summary>
public static class DotEnvStore
{
    private static IReadOnlyDictionary<string, string>? _cache;
    private static string? _cachePath;

    public static IReadOnlyDictionary<string, string> Load()
    {
        var path = AppPaths.EnvFile;
        if (_cache != null && _cachePath == path)
            return _cache;

        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (File.Exists(path))
        {
            foreach (var line in File.ReadAllLines(path))
            {
                var t = line.Trim();
                if (t.Length == 0 || t.StartsWith('#'))
                    continue;
                var eq = t.IndexOf('=');
                if (eq <= 0)
                    continue;
                var key = t[..eq].Trim();
                var val = t[(eq + 1)..].Trim();
                if (val.StartsWith('"') && val.EndsWith('"') && val.Length >= 2)
                    val = val[1..^1].Replace("\\\"", "\"");
                if (key.Length > 0)
                    dict[key] = val;
            }
        }

        _cachePath = path;
        _cache = dict;
        return dict;
    }

    public static void Invalidate() => _cache = null;

    public static string? Get(string key)
    {
        var d = Load();
        return d.TryGetValue(key, out var v) ? v : null;
    }

    public static bool HasAnyApiKey()
    {
        var d = Load();
        return d.Keys.Any(k => k is "ANTHROPIC_API_KEY" or "OPENAI_API_KEY");
    }

    public static bool HasProviderKey(string provider) =>
        provider switch
        {
            "anthropic" => !string.IsNullOrWhiteSpace(Get("ANTHROPIC_API_KEY")),
            "openai" or "openai-compatible" => !string.IsNullOrWhiteSpace(Get("OPENAI_API_KEY")),
            _ => false
        };
}
