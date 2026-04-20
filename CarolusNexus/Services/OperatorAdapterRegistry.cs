using System;
using System.Collections.Generic;

namespace CarolusNexus.Services;

/// <summary>
/// Erweiterungspunkt für Operator-Adapter (Explorer, Outlook, Teams, AX-Fat-Client, …).
/// Aktuell: Katalog + Auflösung nach App-Familie — konkrete UI-Automation folgt pro Adapter.
/// </summary>
public static class OperatorAdapterRegistry
{
    private static IReadOnlyList<IOperatorAdapter>? _merged;

    /// <summary>Pilot-Adapter plus optionale DLLs aus <c>windows/plugins/*.dll</c>.</summary>
    public static IReadOnlyList<IOperatorAdapter> Adapters =>
        _merged ??= BuildAdapters();

    public static void ReloadAdapters() => _merged = null;

    private static IReadOnlyList<IOperatorAdapter> BuildAdapters()
    {
        var list = new List<IOperatorAdapter> { new ExplorerPilotAdapter(), new BrowserPilotAdapter() };
        list.AddRange(PluginAdapterLoader.LoadAdapters());
        return list;
    }

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

    public static string? TryEnrichForegroundContext()
    {
        if (!OperatingSystem.IsWindows())
            return null;
        var d = ForegroundWindowInfo.TryReadDetail();
        if (d == null)
            return null;
        var fam = ResolveFamily(d.Value.ProcessName, d.Value.Title);
        foreach (var a in Adapters)
        {
            if (a.CanHandle(fam))
                return a.EnrichContext(d.Value.Title ?? "", d.Value.ProcessName ?? "");
        }

        return null;
    }

    public static string FormatPilotSnippetsForFamily(string family)
    {
        foreach (var a in Adapters)
        {
            if (a.CanHandle(family))
                return a.SuggestStepSnippets();
        }

        return "";
    }
}
