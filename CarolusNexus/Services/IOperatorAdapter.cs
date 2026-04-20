namespace CarolusNexus.Services;

/// <summary>Erweiterungspunkt: App-spezifischer Kontext und Vorschläge (Phase C).</summary>
public interface IOperatorAdapter
{
    bool CanHandle(string family);

    /// <summary>Kurzer Freitext für LLM-Kontext (1–2k Zeichen max).</summary>
    string EnrichContext(string windowTitle, string processName);

    /// <summary>Optionale Snippets für Ritual-Builder (Demo).</summary>
    string SuggestStepSnippets();
}
