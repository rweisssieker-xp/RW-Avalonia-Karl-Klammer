namespace CarolusNexus.Services;

public sealed class BrowserPilotAdapter : IOperatorAdapter
{
    public bool CanHandle(string family) =>
        string.Equals(family, "browser", StringComparison.OrdinalIgnoreCase);

    public string EnrichContext(string windowTitle, string processName) =>
        $"Adapter=browser · process={processName} · title={windowTitle}\n" +
        "Heuristics: treat as generic browser shell — open URLs with browser.open:https://… or raw https links in power-user profile.";

    public string SuggestStepSnippets() =>
        "browser.open:https://example.com\n" +
        "[ACTION:hotkey|Ctrl+L]";
}
