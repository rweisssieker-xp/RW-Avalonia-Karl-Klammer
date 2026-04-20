using System;
using System.IO;
using System.Text.Json;

namespace CarolusNexus.Services;

/// <summary>Vorschlag nach Fehllauf aus Audit-JSONL (Tier B #7, knapp gehalten).</summary>
public static class SelfHealSuggestionService
{
    public static string? TrySuggestFromLastAuditFailure(int tailLines = 80)
    {
        try
        {
            if (!File.Exists(AppPaths.RitualStepAudit))
                return null;
            var lines = File.ReadAllLines(AppPaths.RitualStepAudit);
            if (lines.Length == 0)
                return null;
            var take = Math.Min(tailLines, lines.Length);
            for (var i = lines.Length - 1; i >= lines.Length - take; i--)
            {
                if (i < 0)
                    break;
                var line = lines[i];
                if (string.IsNullOrWhiteSpace(line))
                    continue;
                try
                {
                    using var doc = JsonDocument.Parse(line);
                    var root = doc.RootElement;
                    if (!root.TryGetProperty("result", out var r))
                        continue;
                    var res = r.GetString() ?? "";
                    if (res.Contains("[ERR]", StringComparison.Ordinal) ||
                        res.Contains("[BLOCKED]", StringComparison.Ordinal))
                    {
                        root.TryGetProperty("actionArgument", out var argEl);
                        var arg = argEl.ValueKind == JsonValueKind.String ? argEl.GetString() : "";
                        return
                            "Last failing step suggests: re-run as dry-run first; verify safety profile and denylist; " +
                            $"inspect argument: {arg}. If UIA: confirm automationId/name still matches the live UI.";
                    }
                }
                catch
                {
                    /* skip line */
                }
            }
        }
        catch
        {
            return null;
        }

        return null;
    }
}
