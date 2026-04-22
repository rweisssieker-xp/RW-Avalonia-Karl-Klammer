using System.Text.Json.Serialization;

namespace CarolusNexus.Models;

public sealed class NexusSettings
{
    [JsonPropertyName("provider")]
    public string Provider { get; set; } = "anthropic";

    [JsonPropertyName("model")]
    public string Model { get; set; } = "claude-sonnet-4-20250514";

    [JsonPropertyName("mode")]
    public string Mode { get; set; } = "companion";

    [JsonPropertyName("speakResponses")]
    public bool SpeakResponses { get; set; }

    [JsonPropertyName("useLocalKnowledge")]
    public bool UseLocalKnowledge { get; set; } = true;

    [JsonPropertyName("suggestAutomations")]
    public bool SuggestAutomations { get; set; } = true;

    /// <summary>Abstand zwischen Watch-Snapshots in Sekunden (15–600).</summary>
    [JsonPropertyName("watchSnapshotIntervalSeconds")]
    public int WatchSnapshotIntervalSeconds { get; set; } = 45;

    /// <summary>Im Watch-Modus: periodisch einen kurzen LLM-Hinweis fürs Dashboard holen.</summary>
    [JsonPropertyName("proactiveDashboardLlm")]
    public bool ProactiveDashboardLlm { get; set; }

    /// <summary>Mindestabstand für proaktive LLM-Aufrufe (Sekunden, 60–3600).</summary>
    [JsonPropertyName("proactiveLlmMinIntervalSeconds")]
    public int ProactiveLlmMinIntervalSeconds { get; set; } = 180;

    /// <summary>Lokalen Tool-Host (127.0.0.1) starten — siehe LOCAL_TOOL_TOKEN in .env.</summary>
    [JsonPropertyName("enableLocalToolHost")]
    public bool EnableLocalToolHost { get; set; }

    [JsonPropertyName("localToolHostPort")]
    public int LocalToolHostPort { get; set; } = 17888;

    /// <summary>Oberflächenthema: <c>Dark</c>, <c>Light</c>, <c>Default</c> (System).</summary>
    [JsonPropertyName("uiTheme")]
    public string UiTheme { get; set; } = "Dark";

    /// <summary>Erweiterte UI-Präferenz.
    /// Werte: <c>system</c>, <c>light</c>, <c>dark</c>, <c>highContrast</c>.</summary>
    [JsonPropertyName("uiThemeMode")]
    public string UiThemeMode { get; set; } = "system";

    /// <summary>Shell-Dichte: <c>comfortable</c> oder <c>compact</c>.</summary>
    [JsonPropertyName("uiDensity")]
    public string UiDensity { get; set; } = "comfortable";

    /// <summary>Weniger Animationen für reduzierte Bewegung.</summary>
    [JsonPropertyName("reduceMotion")]
    public bool ReduceMotion { get; set; }

    /// <summary>Pins für Command-Palette-Einträge als persistente IDs (z. B. <c>page:dashboard</c>).</summary>
    [JsonPropertyName("commandPalettePinned")]
    public string[] CommandPalettePinned { get; set; } = [];

    /// <summary>Bei Ask: gekürzten UIA-Baum des Vordergrundfensters in den Prompt einfügen (Windows).</summary>
    [JsonPropertyName("includeUiaContextInAsk")]
    public bool IncludeUiaContextInAsk { get; set; }

    /// <summary>Letzte Turns in <see cref="AppPaths.ConversationMemory"/> mitschicken (Token-/Kostenbewusst kürzen).</summary>
    [JsonPropertyName("conversationMemoryEnabled")]
    public bool ConversationMemoryEnabled { get; set; }

    [JsonPropertyName("conversationMemoryMaxChars")]
    public int ConversationMemoryMaxChars { get; set; } = 8000;

    /// <summary>Bei Plan mit Risiko „high“: zweite Bestätigung vor Ausführung.</summary>
    [JsonPropertyName("highRiskSecondConfirm")]
    public bool HighRiskSecondConfirm { get; set; } = true;

    [JsonPropertyName("safety")]
    public SafetySettings Safety { get; set; } = new();

    /// <summary>Hinweis für UI: gebündelte Edge-Modelle sind optional — siehe OfflineEdgeCapabilities.</summary>
    [JsonPropertyName("showOfflineCapabilityBanner")]
    public bool ShowOfflineCapabilityBanner { get; set; } = true;

    /// <summary>AX-/Dynamics-Token (<c>ax.*</c>) und Kontext — abschaltbar für reine Demo-Umgebungen.</summary>
    [JsonPropertyName("axIntegrationEnabled")]
    public bool AxIntegrationEnabled { get; set; } = true;

    /// <summary>Aktiviert textbasierte „use codex / use claude code / use openclaw“-CLI-Routen.</summary>
    [JsonPropertyName("enableCliHandoffRoutes")]
    public bool EnableCliHandoffRoutes { get; set; } = true;

    /// <summary>Aktiviert textbasierte Mission-/Autonomy-Routen (autonomy, predictive, orbit, explicit Mission-Flow).</summary>
    [JsonPropertyName("enableMissionPromptRoutes")]
    public bool EnableMissionPromptRoutes { get; set; } = true;

    /// <summary>Erlaubt direkte `radical-auto` Ausführung statt nur „Plan generiert“-Ausgabe.</summary>
    [JsonPropertyName("enableRadicalAutoRoutes")]
    public bool EnableRadicalAutoRoutes { get; set; } = true;

    /// <summary>Ermöglicht `radical` als Ideen-Blueprint (statt normaler Ask-Antwort).</summary>
    [JsonPropertyName("enableRadicalIdeaBlueprint")]
    public bool EnableRadicalIdeaBlueprint { get; set; } = true;

    /// <summary>Kann `mission mode` auch dann automatisch auslösen, wenn der Ask-Modus nicht explizit darauf setzt.</summary>
    [JsonPropertyName("fallbackMissionModeWhenAsk")]
    public bool FallbackMissionModeWhenAsk { get; set; } = false;

    /// <summary>Freitext nur für Logs/Kontext (z. B. Testmandant) — kein Secret.</summary>
    [JsonPropertyName("axTestTenantLabel")]
    public string AxTestTenantLabel { get; set; } = "";

    /// <summary>
    /// <c>foreground_uia</c> = nur Vordergrund/UIA (Standard). <c>odata</c> = AX-2012-OData-HTTP (Testmandant/Datenbereich).
    /// <c>com_bc</c> = Business Connector .NET (lokal installierte DLL, kein Secret im JSON).
    /// </summary>
    [JsonPropertyName("axIntegrationBackend")]
    public string AxIntegrationBackend { get; set; } = "foreground_uia";

    /// <summary>Basis-URL für OData (z. B. <c>https://AOS/AX/Data/</c> oder /AX/Services/). Keine Secrets hier.</summary>
    [JsonPropertyName("axODataBaseUrl")]
    public string AxODataBaseUrl { get; set; } = "";

    /// <summary>Wenn true: <see cref="System.Net.CredentialCache.DefaultNetworkCredentials"/> / DefaultCredentials; sonst Basic mit <c>AX_HTTP_USER</c> / <c>AX_HTTP_PASSWORD</c> in .env.</summary>
    [JsonPropertyName("axODataUseDefaultCredentials")]
    public bool AxODataUseDefaultCredentials { get; set; } = true;

    /// <summary>Optional: Basis-URL für AIF-SOAP-Endpunkte (Health/Ping). Erweiterungspunkt für SOAP-Client.</summary>
    [JsonPropertyName("axAifServiceBaseUrl")]
    public string AxAifServiceBaseUrl { get; set; } = "";

    /// <summary>AX-Datenbereich / Firmenkennung (z. B. USMF, DAT) — Testmandant.</summary>
    [JsonPropertyName("axDataAreaId")]
    public string AxDataAreaId { get; set; } = "";

    /// <summary>Vollständiger Pfad zu <c>Microsoft.Dynamics.BusinessConnectorNet.dll</c> (Client-Bin).</summary>
    [JsonPropertyName("axBusinessConnectorNetAssemblyPath")]
    public string AxBusinessConnectorNetAssemblyPath { get; set; } = "";

    /// <summary>AOS für COM-Logon, z. B. <c>localhost:2712</c> oder NetBIOS.</summary>
    [JsonPropertyName("axBcObjectServer")]
    public string AxBcObjectServer { get; set; } = "";

    /// <summary>SQL-/Anwendungsdatenbankname für COM-Logon.</summary>
    [JsonPropertyName("axBcDatabase")]
    public string AxBcDatabase { get; set; } = "";

    /// <summary>Sprache für COM-Logon (z. B. en-us, de).</summary>
    [JsonPropertyName("axBcLanguage")]
    public string AxBcLanguage { get; set; } = "en-us";

    /// <summary>Main header: badges, tiles, global buttons (Expander).</summary>
    [JsonPropertyName("shellHeaderDetailsExpanded")]
    public bool ShellHeaderDetailsExpanded { get; set; } = true;

    /// <summary>Tab strip on the left (vs top).</summary>
    [JsonPropertyName("useVerticalTabs")]
    public bool UseVerticalTabs { get; set; }

    /// <summary>Ask pane left column star weight (with splitter; wide layout only).</summary>
    [JsonPropertyName("askLeftPaneStarWeight")]
    public double AskLeftPaneStarWeight { get; set; } = 1;

    /// <summary>Ask pane right column star weight.</summary>
    [JsonPropertyName("askRightPaneStarWeight")]
    public double AskRightPaneStarWeight { get; set; } = 1;
}

public sealed class SafetySettings
{
    [JsonPropertyName("profile")]
    public string Profile { get; set; } = "balanced";

    [JsonPropertyName("neverAutoSend")]
    public bool NeverAutoSend { get; set; } = true;

    [JsonPropertyName("neverAutoPostBook")]
    public bool NeverAutoPostBook { get; set; } = true;

    [JsonPropertyName("panicStopEnabled")]
    public bool PanicStopEnabled { get; set; } = true;

    [JsonPropertyName("denylist")]
    public string Denylist { get; set; } = "mail, outlook, teams";

    /// <summary>Kommagetrennte App-Familien (siehe OperatorAdapterRegistry), z. B. <c>explorer,browser</c>. Leer = alle erlaubt.</summary>
    [JsonPropertyName("allowedAppFamilies")]
    public string AllowedAppFamilies { get; set; } = "";

    /// <summary>Erlaubt Ritual-Schritte mit Kanal <c>script</c> (PowerShell/cmd — nur mit power-user).</summary>
    [JsonPropertyName("allowScriptHooks")]
    public bool AllowScriptHooks { get; set; }

    /// <summary>Wenn true: Script-Kanal nur simulieren / loggen, keine echte Prozessausführung.</summary>
    [JsonPropertyName("sandboxScriptHooks")]
    public bool SandboxScriptHooks { get; set; } = true;
}
