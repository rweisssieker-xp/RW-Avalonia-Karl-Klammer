using System;
using System.Collections.Generic;

namespace CarolusNexus.Services;

/// <summary>
/// Erweiterungspunkt für Operator-Adapter (Explorer, Outlook, Teams, AX-Fat-Client, …).
/// Aktuell: Katalog + Auflösung nach App-Familie — konkrete UI-Automation folgt pro Adapter.
/// </summary>
public static class OperatorAdapterRegistry
{
    public static IReadOnlyList<string> KnownFamilies { get; } =
    [
        "explorer", "browser", "mail", "outlook", "teams", "word", "excel", "powerpoint",
        "onenote", "editor", "ax2012", "generic"
    ];

    public static string ResolveFamily(string? processName, string? windowTitle)
    {
        var p = (processName ?? "").ToLowerInvariant();
        var t = (windowTitle ?? "").ToLowerInvariant();
        if (p.Contains("dynamics") || p.Contains("ax32") || t.Contains("microsoft dynamics"))
            return "ax2012";
        if (p.Contains("outlook"))
            return "outlook";
        if (p.Contains("teams"))
            return "teams";
        if (p.Contains("winword"))
            return "word";
        if (p.Contains("excel"))
            return "excel";
        if (p.Contains("powerpnt"))
            return "powerpoint";
        if (p.Contains("onenote"))
            return "onenote";
        if (p.Contains("explorer"))
            return "explorer";
        if (p is "chrome" or "msedge" or "firefox")
            return "browser";
        return "generic";
    }
}
