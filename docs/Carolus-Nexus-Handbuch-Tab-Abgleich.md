# Abgleich Handbuch §5–§14 mit Avalonia-UI (Ist-Stand)

Stand: Branch `RW-Avalonia-Karl-Klammer`. Referenz: [Carolus-Nexus-Benutzerhandbuch.md](Carolus-Nexus-Benutzerhandbuch.md). Ziel dieses Dokuments ist der tabweise Abgleich der **beschriebenen** Oberfläche und Abläufe mit **MainWindow** und den `Views/*Tab`-Implementierungen.

## §5 Oberfläche – Tour durch die Tabs

| Handbuch | Ist (Code) | Anmerkung |
|----------|------------|-----------|
| Tab-Reihenfolge Ask · Dashboard · … · Live Context | [MainWindow.axaml](../CarolusNexus/MainWindow.axaml) `TabControl` | Stimmt überein. |
| Hero-Header: Titel, Untertitel, Statuszeile, Companion, Close, Handbook, Badges, drei Kacheln, globale Buttons | [MainWindow.axaml](../CarolusNexus/MainWindow.axaml) | Stimmt überein. |
| **5.2 Ask** – Buttons inkl. „freigeben + ausführen“ | [AskTab.axaml](../CarolusNexus/Views/AskTab.axaml): Button **`approve + run`** | Gleiche Funktion (Bestätigungsdialog); **Beschriftung** im UI Englisch, Handbuch Deutsch. |
| Zusätzliche Ask-Buttons nicht in §5.2 explizit | `copy answer`, `Insert knowledge`, `Polish prompt/answer`, Chip-Buttons `+ Summary` / `+ Next steps` / … | Über Handbuch hinaus; kein Widerspruch. |
| **5.3 Dashboard** – 3D + sieben Karten | [DashboardTab.axaml](../CarolusNexus/Views/DashboardTab.axaml): `OfficeScene3D`, Karten 1–7 | Karte 4 im UI: „Proactive Karl **+ shell log**“ (Handbuch: „Proactive Karl (Watch-Modus …)“) – leichte Textabweichung, Inhalt aus Code befüllt. |
| **5.4 Setup** – Safety, .env-Keys | [SetupTab.axaml](../CarolusNexus/Views/SetupTab.axaml) + [SetupTab.axaml.cs](../CarolusNexus/Views/SetupTab.axaml.cs) | Zusätzliche Schalter (z. B. Conversation memory, second confirmation, local tool host) über §5.4 hinaus dokumentiert in §4/`NexusSettings`. |
| **5.5 Knowledge** | [KnowledgeTab.axaml](../CarolusNexus/Views/KnowledgeTab.axaml) | `search` · `import files` · `remove doc` · `reindex` · `suggest ritual from doc` – übereinstimmend. |
| **5.6 Rituals** – Spalten, Teach, Queue, Governance | [RitualsTab.axaml](../CarolusNexus/Views/RitualsTab.axaml) | Handbuch: keine Category/Source/Risk-**Filter** in dieser UI → **Zielbild**; Ist: Suche/Builder/JSON wie beschrieben. |
| **5.7 History** | [HistoryTab.axaml](../CarolusNexus/Views/HistoryTab.axaml) | Übereinstimmend mit §5.7. |
| **5.8 Diagnostics** | [DiagnosticsTab.axaml](../CarolusNexus/Views/DiagnosticsTab.axaml) | `export` / `clear logs` – übereinstimmend. |
| **5.9 Console** | [ConsoleTab.axaml](../CarolusNexus/Views/ConsoleTab.axaml) | `run selected agent` – übereinstimmend. |
| **5.10 Live Context** – Untertabs Active / AX / Cross-App | [LiveContextTab.axaml](../CarolusNexus/Views/LiveContextTab.axaml) | Drei `TabItem` wie beschrieben. |

## §6 Hauptarbeitsabläufe

Die beschriebenen Abläufe (Ask mit Screenshot, PTT, Audio-Import, Wissen, speak, Ritual, History/Watch/Teach) sind an **AskTab**, **MainWindow** (Hotkey), **KnowledgeTab**, **RitualsTab**, **HistoryTab** gebunden. Abweichungen folgen primär **API-Keys**, **Safety-Profil** und **PlanGuard**, nicht fehlenden Tabs.

## §7 Provider, Modelle und Modi

Umsetzung über [NexusSettings](../CarolusNexus/Models/NexusSettings.cs), [SettingsStore](../CarolusNexus/Services/SettingsStore.cs), [SetupTab](../CarolusNexus/Views/SetupTab.axaml.cs), [LlmChatService](../CarolusNexus/Services/LlmChatService.cs). Kein Tab-Abgleich nötig; Rauchtest-Button im Ask-Tab vorhanden.

## §8 Sprache (PTT, STT, TTS)

Implementierung wie Handbuch: [WindowsMicRecorder](../CarolusNexus/Services/WindowsMicRecorder.cs), [SpeechTranscriptionService](../CarolusNexus/Services/SpeechTranscriptionService.cs), [TextToSpeechService](../CarolusNexus/Services/TextToSpeechService.cs), [PushToTalkHotkeyWindow](../CarolusNexus/Services/PushToTalkHotkeyWindow.cs), Ask-Tab + MainWindow.

## §9 Lokale CLI-Agenten

[CliAgentRunner](../CarolusNexus/Services/CliAgentRunner.cs), [ConsoleTab](../CarolusNexus/Views/ConsoleTab.axaml.cs); Routing-Trigger im Ask-Tab ([AskPromptRouter](../CarolusNexus/Services/AskPromptRouter.cs) / AskTab-Logik).

## §10 Lokales Wissen (RAG)

[KnowledgeIndexService](../CarolusNexus/Services/KnowledgeIndexService.cs), [KnowledgeSnippetService](../CarolusNexus/Services/KnowledgeSnippetService.cs), Knowledge-Tab – übereinstimmend mit §10.

## §11 Rituale

UI gemäß Handbuch als leistungsfähiger, teils vereinfachter Editor (Handbuch §11 „Zielarchitektur“ / §5.6). Persistenz `automation-recipes.json` über [RitualRecipeStore](../CarolusNexus/Services/RitualRecipeStore.cs) o. ä.

## §12 Action-Plans

[ActionPlanExtractor](../CarolusNexus/Services/ActionPlanExtractor.cs), [SimplePlanSimulator](../CarolusNexus/Services/SimplePlanSimulator.cs), [PlanGuard](../CarolusNexus/Services/PlanGuard.cs), Ask-Tab rechte Spalte – übereinstimmend; Ausführung nur **power-user** + erlaubte Tokens (§1.1 / §22).

## §13 Operator-Adapter pro App

**Handbuch:** Zielarchitektur mit semantischen Aktionen; Ist: Familien-Heuristik und Live-Context-Buttons ([OperatorAdapterRegistry](../CarolusNexus/Services/OperatorAdapterRegistry.cs), [LiveContextTab.axaml.cs](../CarolusNexus/Views/LiveContextTab.axaml.cs)). Vollständige pro-App-Automation = Roadmap, nicht UI-Lücke eines einzelnen Tabs.

## §14 AX 2012 Fat-Client

| Handbuch | Ist |
|----------|-----|
| Dienst `AxClientAutomationService` als Snapshot-/Kontext-Quelle | **Keine** Klasse dieses Namens im Repository (Stand Abgleich). Kontext und Heuristik über Live Context / Vordergrund / Registry wie in §15. |
| `ax.*`-Aktionen in Plänen | Parser / Vorschau ja; **echte Ausführung** eingeschränkt wie §23.0 ([Win32AutomationExecutor](../CarolusNexus/Services/Win32AutomationExecutor.cs)). |
| Tab **Live Context** → AX-Untertab Heuristik | [LiveContextTab](../CarolusNexus/Views/LiveContextTab.axaml.cs) – entspricht „Heuristik, keine vollständige Grid-Extraktion“ (§15). |

---

### Kurzfassung

- **§5–§12:** Avalonia-Tabs decken die beschriebene Tour und Workflows überwiegend ab; nennenswerte Abweichungen: **englische** Ask-Labels statt deutscher Handbuch-Bezeichner (`approve + run`), Dashboard-Kartenüberschrift Karte 4, **zusätzliche** Ask-/Setup-Features über die knappe §5-Stichliste hinaus.
- **§13–§14:** Differenz zur **Zielspezifikation** liegt bei Runtime-Tiefe (Adapter/AX), nicht bei fehlenden Tab-Seiten.
