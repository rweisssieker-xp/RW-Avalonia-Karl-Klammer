namespace CarolusNexus.Services;

/// <summary>Pilot-Adapter: Explorer — belegbare Snippets, kein Produktversprechen „alle Apps“.</summary>
public sealed class ExplorerPilotAdapter : IOperatorAdapter
{
    public bool CanHandle(string family) =>
        string.Equals(family, "explorer", StringComparison.OrdinalIgnoreCase);

    public string EnrichContext(string windowTitle, string processName) =>
        $"Adapter=explorer · process={processName} · title={windowTitle}\n" +
        "Heuristics: user is likely in File Explorer. Prefer path tokens explorer.open_path:… " +
        "or keyboard [ACTION:hotkey|Alt+D] for address bar.";

    public string SuggestStepSnippets() =>
        "explorer.open_path:C:\\\\Users\\\\Public\\\\Documents\n" +
        "[ACTION:hotkey|Ctrl+Shift+N]\n" +
        "[ACTION:hotkey|Alt+D]";
}
