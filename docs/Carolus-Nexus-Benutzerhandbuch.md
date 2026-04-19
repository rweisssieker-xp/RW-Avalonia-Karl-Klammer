# Carolus Nexus – Benutzerhandbuch

> **Karl Klammer** ist die Companion-Persona, **Carolus Nexus** das Produkt:
> ein Windows-Operator-Desktop auf Avalonia (.NET 10), der Sprache, Vision-LLMs,
> lokales Wissen, Ritual-Automation und Fat-Client-Steuerung in einer Oberfläche bündelt.

---

## Inhaltsverzeichnis

1. [Was ist Carolus Nexus?](#1-was-ist-carolus-nexus)
   - [1.1 Dokumentationsstrategie und Implementierungsstand](#11-dokumentationsstrategie-und-implementierungsstand-dieses-repository)
2. [Systemvoraussetzungen](#2-systemvoraussetzungen)
3. [Installation und Start](#3-installation-und-start)
4. [Konfiguration (`.env` und `settings.json`)](#4-konfiguration-env-und-settingsjson)
5. [Oberfläche – Tour durch die Tabs](#5-oberfläche--tour-durch-die-tabs)
6. [Hauptarbeitsabläufe](#6-hauptarbeitsabläufe)
7. [Provider, Modelle und Modi](#7-provider-modelle-und-modi)
8. [Sprache: Push-to-Talk, STT und TTS](#8-sprache-push-to-talk-stt-und-tts)
9. [Lokale CLI-Agenten: Codex, Claude Code, OpenClaw](#9-lokale-cli-agenten-codex-claude-code-openclaw)
10. [Lokales Wissen (RAG)](#10-lokales-wissen-rag)
11. [Rituale (Automation Recipes)](#11-rituale-automation-recipes)
12. [Action-Plans und Plan-Ausführung](#12-action-plans-und-plan-ausführung)
13. [Operator-Adapter pro App](#13-operator-adapter-pro-app)
14. [AX 2012 / Microsoft Dynamics AX Fat-Client](#14-ax-2012--microsoft-dynamics-ax-fat-client)
15. [Live Context und Desktop Inspector](#15-live-context-und-desktop-inspector)
16. [Safety Center und Governance](#16-safety-center-und-governance)
17. [Companion-Overlay (Karl Klammer am Cursor)](#17-companion-overlay-karl-klammer-am-cursor)
18. [Tray-Icon und Hotkeys](#18-tray-icon-und-hotkeys)
19. [Diagnostics, History und Audit](#19-diagnostics-history-und-audit)
20. [Datenablage im Repository](#20-datenablage-im-repository)
21. [Trigger-Phrasen für Sprachbefehle](#21-trigger-phrasen-für-sprachbefehle)
22. [Bekannte Einschränkungen](#22-bekannte-einschränkungen)
23. [Anhang: Vollständiger Action-Token-Katalog](#23-anhang-vollständiger-action-token-katalog)
24. [USP-Strategie und KI-Positionierung (Marketing)](#24-usp-strategie-und-ki-positionierung-marketing)

---

## 1. Was ist Carolus Nexus?

Carolus Nexus ist ein **Windows-Desktop-Assistent** mit folgenden Kernfähigkeiten:

| Bereich | Was die Software tut |
|---|---|
| **Vision-Chat** | Stellt eine Frage zusammen mit aktuellen Screenshots aller Monitore an Anthropic Claude, OpenAI GPT oder einen OpenAI-kompatiblen Endpoint. |
| **Sprache rein** | Aufnahme über Mikrofon (Hold-to-Talk-Button oder globaler Hotkey), Transkription via ElevenLabs STT oder lokalem Whisper, automatisches Abschicken nach dem Loslassen. |
| **Sprache raus** | TTS-Wiedergabe der Antwort als MP3 via ElevenLabs (Fallback auf Windows-SAPI). |
| **CLI-Handoff** | Routet einzelne Prompts auf lokal installierte Codex-, Claude-Code- oder OpenClaw-CLIs und sammelt deren Ausgaben. |
| **Lokales Wissen** | Indexiert lokale Dateien (TXT, MD, PDF, DOCX, JSON, CSV, LOG) und reichert Anfragen mit relevanten Chunks an (RAG). |
| **Rituale** | Wiederverwendbare, parametrisierbare Automations-Rezepte mit strukturierten Schritten, Guards, Risikolevel, Adapter-Affinität und Approval-Modus. |
| **Action-Plans** | Aus Modellantworten extrahierte Mehrschritt-Pläne (`app|...`, `ax.*`, Hotkeys, Klicks) mit Vorschau, Schrittweise-Ausführung und Save-as-Ritual. |
| **Operator-Adapter** | Semantische Aktionen für Browser, Explorer, Mail, Editor, Outlook, Teams, Word, Excel, PowerPoint, OneNote. |
| **Fat-Client AX** | Adapter für Microsoft Dynamics AX 2012 mit Form-, Dialog-, Tab-, Field- und Grid-Erkennung sowie `ax.*`-Aktionen. |
| **Desktop Inspector** | Read-Only-Inspektion der aktiven Windows-Steuerelemente, Formulare, Tabellen und Dialoge. |
| **Companion-Overlay** | Animierte, immer-im-Vordergrund Cursor-Begleitung mit Zustandsanzeige (ready / listening / thinking / speaking / error). |
| **Tray + Hotkey** | Persistentes Tray-Icon und globaler Push-to-Talk-Hotkey. |
| **Safety Center** | Profile (`strict` / `balanced` / `power-user`), Denylist, Panic-Stop, Auto-Send-Sperre, Risk-Gating. |
| **Governance** | Publikations-Status, Approval-Modus, Audit-Records, Job-Queue für veröffentlichte Flows. |
| **Teach-Mode** | Aus Action-History oder Watch-Sessions automatisch Ritual-Drafts erzeugen. |

Karl Klammer ist die **Identität/Stimme** des Assistenten (siehe [SOUL.md](../SOUL.md) im Repository-Root).

### 1.1 Dokumentationsstrategie und Implementierungsstand (dieses Repository)

**Strategie:** Dieses Handbuch ist in erster Linie eine **Produkt- und Zielspezifikation** für Carolus Nexus (Funktionsumfang, Konfiguration, Arbeitsabläufe, Action-Tokens). Kapitel **§5 ff.** sind wo nötig mit dem **aktuellen UI-Stand** im Branch **RW-Avalonia-Karl-Klammer** abgeglichen. **Marketing & USP:** [Carolus-Nexus-USP-Strategie.md](Carolus-Nexus-USP-Strategie.md) (KI-Killer-Features, Wettbewerb, Demo) — immer gegen **§1.1** und **§22** abgleichen, nicht über den technischen Ist-Stand hinaus kommunizieren.

**Implementierungsstand (Kurzüberblick):**

| Bereich | Stand in `CarolusNexus/` |
|--------|---------------------------|
| Tab-Shell, Header, `settings.json`, `.env`-Lesen ([DotEnvStore](../CarolusNexus/Services/DotEnvStore.cs), Setup-Tab) | umgesetzt (Werte im RAM; keine Secret-Ausgabe in Logs) |
| `AppPaths`, `windows/data`-Bäume, Dateipfade laut §20 | umgesetzt |
| Dashboard inkl. 3D-Vorschau (`OfficeScene3D`), Kacheln | UI umgesetzt; **Proactive Karl:** im Modus **watch** optional kurzer LLM-Hinweis fürs Dashboard ([MainWindow](../CarolusNexus/MainWindow.axaml.cs), Schalter in Setup) |
| Begleiter am Cursor (`KarlCompanionWindow`, Toggle im Header) | umgesetzt (Maus folgen, klick-durchlässig auf Windows) |
| **Tray-Icon** ([App.axaml](../CarolusNexus/App.axaml)) | umgesetzt (Menü Öffnen/Beenden; Schließen minimiert ins Tray) |
| **PTT / globaler Hotkey** `PUSH_TO_TALK_KEY` ([PushToTalkHotkeyWindow](../CarolusNexus/Services/PushToTalkHotkeyWindow.cs), Fallback-Polling) | umgesetzt: Hold-to-Talk, Loslassen → STT + optional Ask |
| **STT / TTS** ([SpeechTranscriptionService](../CarolusNexus/Services/SpeechTranscriptionService.cs), [TextToSpeechService](../CarolusNexus/Services/TextToSpeechService.cs), `STT_PROVIDER`, `TTS_PROVIDER`) | umgesetzt (ElevenLabs / Whisper / SAPI-Fallback je nach `.env`) |
| **Vision-LLM Ask** ([LlmChatService](../CarolusNexus/Services/LlmChatService.cs), Multi-Monitor-Screenshots) | umgesetzt (Provider-Keys vorausgesetzt) |
| **RAG-light** ([KnowledgeIndexService](../CarolusNexus/Services/KnowledgeIndexService.cs), Chunks + [KnowledgeSnippetService](../CarolusNexus/Services/KnowledgeSnippetService.cs)) | umgesetzt (`knowledge-index.json`, `knowledge-chunks.json`, PDF/DOCX via NuGet); **optional semantisch** mit [EmbeddingRagService](../CarolusNexus/Services/EmbeddingRagService.cs) (`knowledge-embeddings.json`, `OPENAI_API_KEY`, siehe `.env`) |
| **Action-Plans** ([ActionPlanExtractor](../CarolusNexus/Services/ActionPlanExtractor.cs), Plan-Vorschau / Run / Save-as-Ritual) | umgesetzt |
| **Plan-Ausführung** ([SimplePlanSimulator](../CarolusNexus/Services/SimplePlanSimulator.cs), [Win32AutomationExecutor](../CarolusNexus/Services/Win32AutomationExecutor.cs), [PlanGuard](../CarolusNexus/Services/PlanGuard.cs)) | umgesetzt: echte Win32-Schritte nur bei Profil **power-user** + erlaubten Tokens |
| **CLI-Handoff** ([CliAgentRunner](../CarolusNexus/Services/CliAgentRunner.cs), Console-Tab) | umgesetzt (lokale CLIs, Logs unter `codex output/`) |
| **Watch-Modus** ([WatchSessionService](../CarolusNexus/Services/WatchSessionService.cs), Dashboard-Snapshots, Bildschirm-Hash) | umgesetzt (Rotation max. 500 Einträge) |
| **History** ([ActionHistoryService](../CarolusNexus/Services/ActionHistoryService.cs), `action-history.json`) | umgesetzt (Plan-Läufe werden protokolliert) |
| **Rituals Teach / promote** | umgesetzt: Teach-Session, Promote aus History/Watch, Capture Vordergrund |
| **Ritual Job-Queue** ([RitualJobQueueStore](../CarolusNexus/Services/RitualJobQueueStore.cs), `ritual-job-queue.json`) | umgesetzt: einreihen, nächsten Job freigeben, History begrenzt |
| **Ritual Step-Audit** ([RitualStepAudit](../CarolusNexus/Services/RitualStepAudit.cs), `ritual-step-audit.jsonl`) | umgesetzt: Protokoll je Plan-Schritt |
| **Live Context** ([LiveContextTab](../CarolusNexus/Views/LiveContextTab.axaml.cs), [ForegroundWindowInfo](../CarolusNexus/Services/ForegroundWindowInfo.cs), [OperatorAdapterRegistry](../CarolusNexus/Services/OperatorAdapterRegistry.cs)) | umgesetzt: Vordergrundfenster, Familien-Heuristik, AX-Hinweis; **keine** vollständige UIA-Grid-Tiefe für AX |
| AX Fat-Client UIAutomation (Form/Grid) | **Teilziel** — narrativ und Tokens vorhanden; Live Context liefert Kontext, keine produktionsreife AX-Steuerung |

Details zu Grenzfällen stehen bei **§15**, **§22** und [Carolus-Nexus-ICP-Personas.md](Carolus-Nexus-ICP-Personas.md).

---

## 2. Systemvoraussetzungen

- **Windows** (die Avalonia-App ist hier Windows-targeted, da die Automations-Schicht Windows-spezifisch ist)
- **.NET 10 SDK**
- Mindestens **ein Provider-Schlüssel**: Anthropic *oder* OpenAI *oder* OpenAI-kompatibler Endpoint
- *Optional:* ElevenLabs (Voice-ID + Key) für STT/TTS
- *Optional:* Lokales **Whisper** (Python-Installation) für STT ohne Cloud
- *Optional:* Lokal installierte CLIs **codex**, **claude**, **openclaw**
- PDF-/DOCX-Wissen: [CarolusNexus.csproj](../CarolusNexus/CarolusNexus.csproj) referenziert `UglyToad.PdfPig` und `DocumentFormat.OpenXml`; der Knowledge-Indexer nutzt sie beim Rebuild ([KnowledgeIndexService](../CarolusNexus/Services/KnowledgeIndexService.cs)).

---

## 3. Installation und Start

Repository-Root (Ordner mit `CarolusNexus\`, `windows\`, `docs\`): dort alle Befehle ausführen.

### Bauen

**Variante Skript (Windows):**

```cmd
Build-Avalonia.cmd
```

Ruft `dotnet build CarolusNexus\CarolusNexus.csproj -c Release` auf.

**Variante manuell:**

```cmd
dotnet build CarolusNexus\CarolusNexus.csproj -c Release
```

### Starten

**Variante Skript:**

```cmd
Start-Avalonia.cmd
```

Baut Release und startet anschließend `CarolusNexus\bin\Release\net10.0-windows\CarolusNexus.dll` per `dotnet exec`.

**Variante manuell:**

```cmd
dotnet run --project CarolusNexus\CarolusNexus.csproj -c Release
```

Solution-Datei: [KarlKlammer.slnx](../KarlKlammer.slnx).

Beim ersten Start:

1. Sicherstellen, dass der Ordner `windows\` im Repository existiert (liefert [AppPaths.DiscoverRepoRoot](../CarolusNexus/AppPaths.cs) die richtige Wurzel beim Start aus `bin\`).
2. `windows\.env.example` nach `windows\.env` kopieren (optional; für die Schlüsselübersicht im Setup-Tab).
3. Keys eintragen — Provider/STT/TTS werden zur Laufzeit aus `windows/.env` gelesen ([DotEnvStore](../CarolusNexus/Services/DotEnvStore.cs)); Werte erscheinen nicht in Logs.
4. Optional: Dateien in `windows\data\knowledge\` ablegen.
5. App starten, im Header **„refresh all"** klicken.
6. Im Tab **Setup** Provider, Modus und Modell prüfen → **„save settings"**.
7. Im Tab **Ask** Vision, Wissen, Pläne und Sprache ausprobieren; Details landen bei Bedarf in **Diagnostics**.

---

## 4. Konfiguration (`.env` und `settings.json`)

### 4.1 `windows\.env` (Secrets, **nicht** in Git)

**Aktueller Code:** [DotEnvStore](../CarolusNexus/Services/DotEnvStore.cs) liest Werte aus `windows\.env` (RAM-Cache); [DotEnvSummary](../CarolusNexus/Services/DotEnvSummary.cs) zeigt im Setup-Tab nur **Schlüsselnamen** — keine Secret-Werte. Provider/STT/TTS/Codex nutzen die geladenen Werte. Die folgende Tabelle ergänzt `windows\.env.example`.

| Variable | Default (Ziel) | Zweck |
|---|---|---|
| `ANTHROPIC_API_KEY` | – | Anthropic-Vision-Flow |
| `OPENAI_API_KEY` | – | OpenAI / kompatibel |
| `OPENAI_BASE_URL` | `https://api.openai.com/v1` | Alternativer OpenAI-kompatibler Endpoint |
| `ELEVENLABS_API_KEY` | – | TTS und optional STT |
| `ELEVENLABS_VOICE_ID` | – | TTS-Stimme |
| `STT_PROVIDER` | `whisper` | `elevenlabs` oder `whisper` |
| `WHISPER_PYTHON` | `python` | Python-Aufruf für Whisper |
| `WHISPER_MODEL` | `base` | Whisper-Modellname |
| `WHISPER_LANGUAGE` | `de` | Sprach-Hint |
| `CODEX_COMMAND` | `codex.cmd` | Pfad/Name der Codex-CLI |
| `CODEX_WORKDIR` | `playground/` | Arbeitsverzeichnis Codex |
| `CODEX_TIMEOUT_SECONDS` | `900` | Timeout Codex |
| `CLAUDE_CODE_COMMAND` | `claude` | Claude-Code-CLI |
| `OPENCLAW_COMMAND` | `openclaw` | OpenClaw-CLI |
| `OPENCLAW_SESSION_KEY` | `main` | OpenClaw-Agent/Session |
| `OPENCLAW_TIMEOUT_SECONDS` | `120` | Timeout OpenClaw |
| `OPENCLAW_GATEWAY_URL` | – | optionaler Gateway |
| `GATEWAY_TOKEN` | – | optionaler Gateway-Token |
| `PUSH_TO_TALK_KEY` | `F8` | Globaler PTT-Hotkey — [PushToTalkHotkeyWindow](../CarolusNexus/Services/PushToTalkHotkeyWindow.cs) bzw. Fallback-Polling in [MainWindow](../CarolusNexus/MainWindow.axaml.cs) |
| `TTS_PROVIDER` | `auto` | `auto` (ElevenLabs falls Keys, sonst SAPI), `elevenlabs`, `sapi` / `windows` — [TextToSpeechService](../CarolusNexus/Services/TextToSpeechService.cs) |

Reload der Anzeige zur Laufzeit über den Header-Button **„refresh all"**.

### 4.2 `windows\data\settings.json` (lokale Einstellungen, kein Secret)

Wird über den **Setup**-Tab gepflegt und von [SettingsStore](../CarolusNexus/Services/SettingsStore.cs) als JSON serialisiert. Property-Namen entsprechen [NexusSettings](../CarolusNexus/Models/NexusSettings.cs) (`JsonPropertyName`):

| JSON-Schlüssel | Bedeutung |
|----------------|-----------|
| `provider` | `anthropic` / `openai` / `openai-compatible` |
| `model` | Modellname |
| `mode` | `companion` / `agent` / `automation` / `watch` |
| `speakResponses` | TTS gewünscht |
| `useLocalKnowledge` | RAG im Ask-Flow (Ziel) |
| `suggestAutomations` | Automations-Vorschläge (Ziel) |
| `safety.profile` | `strict` / `balanced` / `power-user` |
| `safety.neverAutoSend` | Kein Auto-Send |
| `safety.neverAutoPostBook` | Kein Auto-Post/Buchen |
| `safety.panicStopEnabled` | Panic-Stop |
| `safety.denylist` | kommagetrennte App-Familien |

---

## 5. Oberfläche – Tour durch die Tabs

Die Hauptoberfläche ([MainWindow.axaml](../CarolusNexus/MainWindow.axaml)) besteht aus einem **Hero-Header** und einem **Tab-Bereich**. Tab-Reihenfolge: Ask · Dashboard · Setup · Knowledge · Rituals · History · Diagnostics · Console · Live Context.

### 5.1 Hero-Header (immer sichtbar)

- **Titel**, Untertitel („Windows-Operator-Desktop · Persona: Karl Klammer“), **Statuszeile**
- Checkbox **„Begleiter am Cursor"** (Windows: `KarlCompanionWindow`) und Button **„Schließen"** (Hauptfenster)
- Button **„Handbuch"** öffnet diese Datei im Standardprogramm (sofern im Build-Layout unter `docs\` auffindbar)
- Badges: **Layout** · **Environment** · **Speech** · **Automation** · **Knowledge** (Kurzstatus aus Settings/.env)
- Kacheln **Operator Memory** · **Live Context** · **Environment**
- Globale Buttons:
  `refresh all` · `save settings` · `reindex knowledge` · `refresh active app` · `export diagnostics`

### 5.2 Tab **Ask**

- Prompt-Eingabe; Checkboxen **„Screenshots einbeziehen"** und **„Lokales Wissen im Ask-Flow"** (Multi-Monitor-Vision bzw. Chunk-Kontext, wenn indexiert). Optional: **UIA-Snapshot** des Vordergrundfensters wird im Setup eingeschaltet und dann an den Prompt angehängt (Windows).
- Buttons:
  `ask now` · `smoke test` · `import audio + transcribe` · `start push-to-talk` · `stop + ask` · `cancel recording` · `clear conversation` · `run plan` · **`freigeben + ausführen`** (Bestätigungsdialog unter Windows) · `run next step` · `save plan as ritual` · `clear plan` · `panic stop` · `speak response`
- KI-Hilfen u. a.: **JSON-Plan aus Antwort**, **KI: strukturierte Schritte**, **Plan erklären / Risiko** (Ausgabe unter **Safety / Recovery** bei „Plan erklären“).
- Hinweiszeile zu **AX Fat-Client** / Ritual-Runtime (Ausführung siehe Safety-Profil **power-user**)
- Spalte links: **Assistant Response**, **Retrieval + Context**, **Safety / Recovery**, **Transcript / Audio**
- Spalte rechts: **Action Plan Preview**, **Action Plan Execution**

### 5.3 Tab **Dashboard**

- Oben: **3D-Vorschau (OpenGL)** mit [OfficeScene3D](../CarolusNexus/OfficeScene3D.cs)
- Darunter sieben Karten im Wrap-Layout (Textfelder, vom Code mit Zusammenfassungen befüllt):
  1. Environment + Routing  
  2. Knowledge + Memory  
  3. Live Context  
  4. Proactive Karl (Watch-Modus + optional LLM-Hinweis aus Setup)  
  5. Governance + Audit (Safety-Profil + **Auszug Ritual-Job-Warteschlange / Verlauf** aus `ritual-job-queue.json`)  
  6. Recent Rituals (Rohinhalt aus `automation-recipes.json`, falls vorhanden)  
  7. Recent Watch Sessions (Rohinhalt aus `watch-sessions.json`, falls vorhanden)

### 5.4 Tab **Setup**

- Provider-, Modus-, Modell-Auswahl; **UI-Thema** (Dark / Light / System-Default); Schalter **speak responses** / **use local knowledge** / **suggest automations** (nach **ask now**: kurzer LLM-Block „Automationsvorschläge“ an die Antwort angehängt, wenn API-Key gesetzt) / **Ask: UIA-Snapshot …**
- **Safety Center:** Profil, **never auto-send**, **never auto-post / book**, **panic stop enabled**, Denylist
- **.env-Übersicht (nur Schlüssel)** + Pfadhinweis — Werte werden intern gelesen, nicht angezeigt

### 5.5 Tab **Knowledge**

- `search` · `import files` · `remove doc` · `reindex` · `suggest ritual from doc`
- Dokumentliste links, **Document Preview** rechts; Reindex über Header oder Knowledge-Tab

### 5.6 Tab **Rituals**

Drei Spalten: **Ritual Library** · **Ritual Builder** · **Structured Steps (JSON)**.

- **Library:** Suchfeld + Liste (keine separaten Category/Source/Risk-Filter in dieser UI; das bleibt **Zielbild** für die volle Library)
- **Builder:** Name, Beschreibung, **Governance (§16.5):** Freigabe, Risiko, Adapter-Affinity, Konfidenz-Quelle, max. Autonomie-Schritte; **Job-Queue-Detail** (ausstehend + Verlauf aus `ritual-job-queue.json`); Buttons  
  `save ritual` · `delete` · `clone` · `archive` · `publish flow` · `queue for run` · `approve next job` · `dry run` · `run ritual` · `run next step` · `resume ritual`  
  sowie **Teach:** `promote from history` · `promote from watch` · `start teach` · `stop teach`  
  Hinweis: **`published` + Freigabe `manual`** blockiert **Direktausführung** (`run ritual` / `run next step`) — stattdessen **queue** + **approve next job**.
- **Structured Steps:** JSON-Editor — **Teach:** `start teach` · `capture foreground step` · optional Live-Context-Schritte · `stop teach` speichert ein Ritual; **promote from history/watch** erzeugt Entwürfe aus `action-history.json` bzw. `watch-sessions.json`

### 5.7 Tab **History**

- Suchfeld, Liste der **Plan-Läufe** (`action-history.json`), JSON-Detail; Button **create ritual from selection** übernimmt die Schritte des gewählten Eintrags

### 5.8 Tab **Diagnostics**

- Filterfeld, Buttons **`export`** (schreibt `windows\data\diagnostics-*.log`) und **`clear logs`**, Log-Textfläche

### 5.9 Tab **Console**

- Agent-Auswahl (`ComboBox`), Prompt, **`run selected agent`**, Output-Pfad unter `codex output/`, Auszug der CLI-Ausgabe ([CliAgentRunner](../CarolusNexus/Services/CliAgentRunner.cs))

### 5.10 Tab **Live Context**

- **Desktop Inspector:** automatische Aktualisierung des **Vordergrundfensters** (Titel, Prozess, Fensterklasse, Bounds) und Zuordnung zu einer **Adapter-Familie** ([OperatorAdapterRegistry](../CarolusNexus/Services/OperatorAdapterRegistry.cs))
- Adapter-Buttons: Vergleich gewählte Familie vs. aktives Fenster; im **Teach-Modus** werden Schritte mitprotokolliert
- **InspectorAction** + **`run`:** protokolliert die Aktion, versucht bei ausführbaren Tokens eine **Win32-Ausführung** (Profil **power-user** + [PlanGuard](../CarolusNexus/Services/PlanGuard.cs)); Teach nimmt die Zeile als Schritt auf
- Untertabs: **Active Window** · **AX Context** (Heuristik AX/Dynamics) · **Cross-App**

---

## 6. Hauptarbeitsabläufe

Die Schritte setzen den **Ist-Stand** aus §1.1 voraus (Vision, RAG-light, STT/TTS, Plan-Lauf). Bei fehlenden API-Keys oder blockierter Automation verweisen Meldungen auf **Diagnostics** und Safety-Profil.

### 6.1 Klassisches „Frag mit Screenshot"

1. **Ask**-Tab öffnen.
2. Prompt eintippen.
3. `include screenshots` aktiv lassen.
4. **„ask now"** klicken.
5. Antwort + ggf. extrahierter **Action-Plan** erscheinen rechts.

### 6.2 Sprache statt Text

- **Hold-to-Talk**: `start push-to-talk` drücken, sprechen, `stop + ask` drücken.
- **Globaler Hotkey** (`PUSH_TO_TALK_KEY`, default `F8`): gedrückt halten → spricht; loslassen → automatisch transkribieren + ask.

### 6.3 Audio-Datei transkribieren

`import audio + transcribe` → Datei wählen → STT-Provider transkribiert → Antwort wie bei Text-Ask.

### 6.4 Wissen einbinden

- Im **Knowledge**-Tab Dateien importieren und reindexieren.
- Im **Ask**-Tab `use local knowledge in ask flow` aktivieren.
- Retrieved-Quellen erscheinen unter **Retrieval + Context**.

### 6.5 Antwort vorlesen lassen

`speak response` im Ask-Tab → ElevenLabs erzeugt MP3 → Pfad wird angezeigt → Datei kann geöffnet werden.

### 6.6 Ein Ritual ausführen

1. **Rituals**-Tab → Ritual auswählen.
2. *(Optional)* Parameter im Builder ergänzen.
3. **„dry run"** zeigt Schritte ohne Ausführung.
4. **„run ritual"** für vollständigen Lauf, **„run next step"** für schrittweise Ausführung.
5. Run-Log und Status erscheinen unten rechts.

### 6.7 Aus History oder Watch lernen

- **History**-Tab → Eintrag mit Schritten wählen → **„create ritual from selection"** (übernimmt die gespeicherten Plan-Schritte).
- **Rituals**-Tab → **„promote from history"** (letzter `plan_run` aus `action-history.json`) oder **„promote from watch"** (ein Schritt pro Watch-Eintrag aus `watch-sessions.json`).
- **Teach-Mode:** **„start teach"** → optional **„capture foreground step"**, Live-Context-**run** oder Adapter-Klicks → **„stop teach"** speichert ein neues Ritual.

---

## 7. Provider, Modelle und Modi

### 7.1 Provider

| Provider | Wann sinnvoll |
|---|---|
| `anthropic` | Direkte Claude-API mit Vision (Screenshots) |
| `openai` | OpenAI-API mit Vision-Modellen |
| `openai-compatible` | Lokale oder alternative LLMs hinter OpenAI-Schema (z. B. Ollama) |

### 7.2 Modus (Persona-System-Prompt)

| Modus | Verhalten |
|---|---|
| `companion` | Locker, hilfsbereit, moderates Antwort-Format |
| `agent` | Aktionsorientiert, schlägt häufiger Action-Plans vor |
| `automation` | Fokus auf Ritual-Vorschläge, knappe deterministische Sprache |
| `watch` | Logging-Modus, schreibt Prompt/Response/Screen in Watch-Sessions für späteres Lernen |

### 7.3 Smoke-Test

`smoke test` im Ask-Tab schickt eine Mini-Anfrage an den aktuellen Provider und zeigt das Ergebnis — End-to-End-Healthcheck inkl. Netzwerk (API-Key nötig).

---

## 8. Sprache: Push-to-Talk, STT und TTS

**Implementierung:** [WindowsMicRecorder](../CarolusNexus/Services/WindowsMicRecorder.cs) (WaveOut/WAV), [SpeechTranscriptionService](../CarolusNexus/Services/SpeechTranscriptionService.cs), [TextToSpeechService](../CarolusNexus/Services/TextToSpeechService.cs), Ask-Tab-Buttons und globaler Hotkey in [MainWindow](../CarolusNexus/MainWindow.axaml.cs).

### 8.1 Mikrofon-Aufnahme

- Button **„start push-to-talk"** → spricht → **„stop + ask"** stoppt, transkribiert und startet bei Erfolg **ask now**
- **Cancel** verwirft die Aufnahme
- **Globaler Hotkey** `PUSH_TO_TALK_KEY` (Default **F8**): gedrückt halten startet die Aufnahme, **Loslassen** stoppt + Transkript + Ask — [PushToTalkHotkeyWindow](../CarolusNexus/Services/PushToTalkHotkeyWindow.cs) mit **Fallback-Polling**, falls `RegisterHotKey` scheitert

### 8.2 Speech-to-Text

- `STT_PROVIDER=elevenlabs` → ElevenLabs Speech-to-Text-API
- `STT_PROVIDER=whisper` → Lokales Whisper über `WHISPER_PYTHON`, Modell `WHISPER_MODEL`, Sprache `WHISPER_LANGUAGE`

### 8.3 Text-to-Speech

- `TTS_PROVIDER=elevenlabs` → ElevenLabs (MP3, Wiedergabe über NAudio)
- `TTS_PROVIDER=sapi` oder `windows` → **Windows-SAPI** ohne Cloud
- `TTS_PROVIDER=auto` (Default) → ElevenLabs wenn Key/Voice-ID gesetzt, sonst SAPI

---

## 9. Lokale CLI-Agenten: Codex, Claude Code, OpenClaw

**Implementierung:** [CliAgentRunner](../CarolusNexus/Services/CliAgentRunner.cs) startet die gewählte CLI mit Timeout und schreibt Logs nach `codex output/`.

### 9.1 Trigger im normalen Ask-Flow

Im Prompt-Text wirkt das Schlüsselwort:

| Schlüsselwort | Routing |
|---|---|
| `nimm codex …` | Lokale Codex-CLI |
| `nimm codex mit screen …` | Codex mit Screenshot-Anhang |
| `nimm claude code …` | Claude-Code-CLI |
| `nimm openclaw …` | OpenClaw-CLI |

### 9.2 Eigener Tab **Console**

- Agent-Auswahl, Prompt, **„run selected agent"**.
- Run-Logs landen in `codex output\karl-klammer-{agent}-{timestamp}.txt`.
- Working-Directory: `playground/` (oder `CODEX_WORKDIR`).

### 9.3 Aufrufe (intern)

- `codex exec --full-auto --skip-git-repo-check -C <workdir> -o <log> -`
- `claude -p --permission-mode bypassPermissions`
- `openclaw agent --agent <id> --message <prompt> --timeout <sec>`

Stderr/Stdout, ExitCode und Prompt werden in jede Log-Datei geschrieben.

---

## 10. Lokales Wissen (RAG)

**Implementierung:** [KnowledgeIndexService](../CarolusNexus/Services/KnowledgeIndexService.cs) (Rebuild → `knowledge-index.json` + **`knowledge-chunks.json`**), [KnowledgeSnippetService](../CarolusNexus/Services/KnowledgeSnippetService.cs) für Kontext im Ask-Tab.

- Dateien in `windows\data\knowledge\` werden indexiert und in **Chunks** zerlegt.
- Unterstützte Formate u. a.: `.txt`, `.md`, `.log`, `.json`, `.csv`, `.pdf`, `.docx` (PdfPig / Open XML).
- **Knowledge**-Tab: Import/Search/Reindex/Remove und Vorschau.
- Bei aktivem `use local knowledge in ask flow` werden relevante Chunks dem Prompt beigelegt; Quellen erscheinen in **Retrieval + Context** (RAG-light, Ranking ohne Vektor-DB).
- **„suggest ritual from doc"** im Knowledge-Tab: Vorschlag aus dem gewählten Dokument ([KnowledgeTab](../CarolusNexus/Views/KnowledgeTab.axaml.cs)).

---

## 11. Rituale (Automation Recipes)

**Zielarchitektur** für Persistenz und Runtime; UI-Teile siehe §5.6 (vereinfachter Stub-Editor).

### 11.1 Felder eines Rituals

| Feld | Bedeutung |
|---|---|
| Name / Description / Tags / Knowledge-Sources | Auffindbarkeit |
| Mode | Welche Persona laufen soll |
| Category / Source-Type / Risk | Filter und Risiko-Bewertung |
| Publication-State / Approval-Mode | Governance |
| Adapter-Affinity | Bevorzugter Operator-Adapter |
| Confidence-Source | Woher die Confidence stammt (history, teach, …) |
| Max-Autonomy-Steps | Hartes Limit für autonome Schritte |
| Guards (App/Form/Dialog/Tab) | Vor-Bedingungen vor Ausführung |
| Enabled | Ein/Aus |

### 11.2 Schritte (`SelectedRecipeSteps`)

Pro Schritt:

- `actionType` (`app|...`, `ax.*`, Hotkeys, `move`, `click`, `type`, `open`, …)
- `actionArgument` (Ziel, Text, Tastenfolge, …)
- `risk`
- `waitMs`, `retryCount`
- `if app/form/dialog/tab` → bedingte Ausführung
- `on fail` (`skip`, `stop`, …)

### 11.3 Parameter

- `name`, `label`, `defaultValue`, `kind`, `required`
- Verwendbar im Prompt als `{{name}}`

### 11.4 Lifecycle-Buttons

`save ritual` · `delete` · `clone` · `archive` · **`publish flow`** · **`queue for run`** · **`approve next job`** · `dry run` · `run ritual` · `run next step` · `resume ritual`

### 11.5 Lernen

- **promote from history**: letzter `plan_run` mit Schritten aus `action-history.json`
- **promote from watch**: ein Token-Schritt pro Eintrag in `watch-sessions.json`
- **start/stop teach**: Puffer mit **capture foreground**, Live-Context-**run** und Adapter-Klicks; **stop** speichert ein Ritual

Persistente Speicherung: `windows\data\automation-recipes.json`.

---

## 12. Action-Plans und Plan-Ausführung

**Ist-Stand:** [ActionPlanExtractor](../CarolusNexus/Services/ActionPlanExtractor.cs), [SimplePlanSimulator](../CarolusNexus/Services/SimplePlanSimulator.cs), [PlanGuard](../CarolusNexus/Services/PlanGuard.cs) — Ausführung wie in §1.1 (nur **power-user** + erlaubte Tokens).

Modellantworten können Aktionen vorschlagen. Diese werden als **Plan** geparst (zuerst Token-/Regex-Extraktion; falls leer, optional **JSON** mit `steps` in der Antwort) und mehrstufig behandelt:

1. **Vorschau** (`Action Plan Preview`) zeigt alle geplanten Schritte.
2. **`freigeben + ausführen`** öffnet einen **Bestätigungsdialog** (Windows); nach Zustimmung wird der Plan wie bei **`run plan`** ausgeführt (Safety-Profil und PlanGuard wie zuvor). **`run plan`** bleibt der direkte Lauf ohne diesen Dialog.
3. **`run next step`** führt schrittweise aus.
4. **`save plan as ritual`** speichert den geplanten Ablauf direkt als Ritual.
5. **`clear plan`** verwirft.
6. **`panic stop`** stoppt jede laufende Plan-/Ritual-Ausführung.

Begleit-Anzeigen:

- **Action Plan State** (Status)
- **Current Plan Step Summary**
- **Safety Policy Summary** (warum eventuell blockiert)
- **Failure Recovery Hint** (was zu tun ist)
- **Action Plan Execution Log** (Live-Protokoll)
- **Action Plan Governance Summary** (Approval/Adapter)

---

## 13. Operator-Adapter pro App

**Zielarchitektur:** Jeder Adapter exportiert **semantische Aktionen** für Pläne und Inspector. **Ist-Stand:** Familien-Heuristik und Live-Context-Buttons (Vergleich/Teach); vollständige pro-App-Automation ist roadmap.

### 13.1 Browser (`browser.*`)

`focus_address`, `open:<url>`, `search:<query>`, `find:<text>`, `refresh`, neue Tab, Back, Refresh-Button.

### 13.2 Explorer (`explorer.*`)

`focus_path`, `open_path:<path>`, `focus_search`, `search:<query>`, `open_selected`, „new folder".

### 13.3 Mail (`mail.*`)

`focus_search`, `new_message`, `find:<text>`, „compose", „reply", „send".

### 13.4 Editor (`editor.*`)

`command_palette`, `quick_open`, `find:<text>`, `save`, `run`, „save all".

### 13.5 Office + Teams (Desktop **und** M365 Web)

`outlook.*`, `teams.*`, `word.*`, `excel.*`, `powerpoint.*`, `onenote.*`
mit jeweiligen Read-Context- und Aktions-Buttons im Live-Context-Tab.

### 13.6 Active-Window-Detection

Erkennt App-Familie + Surface-Kind (Desktop vs. Web) und gibt das an den passenden Adapter weiter, statt alles in „mail" oder „messenger" zu sammeln.

---

## 14. AX 2012 / Microsoft Dynamics AX Fat-Client

**Zielarchitektur:** Dienst z. B. `AxClientAutomationService`. Im Tab **Live Context** liefert der AX-Untertab eine **Heuristik** (Dynamics/AX-Fenster erkannt oder nicht), keine vollständige Form/Grid-Extraktion.

### 14.1 Snapshot

Über `AxClientAutomationService` wird ein Kontext erfasst: Forms, Dialogs, Tabs, Fields, Actions, Grid-Kandidaten.

### 14.2 `ax.*`-Aktionen (in Plans und Ritualen)

| Aktion | Zweck |
|---|---|
| `ax.read_context` | Vollkontext lesen |
| `ax.read_form` | Form-Inhalt lesen |
| `ax.read_field` | Einzelfeld lesen |
| `ax.set_field` | Feld setzen |
| `ax.click_action` | Form-Action klicken |
| `ax.open_tab` | Tab umschalten |
| `ax.open_lookup` | Lookup öffnen |
| `ax.read_grid` | Grid auslesen |
| `ax.select_grid_row` | Grid-Zeile wählen |
| `ax.confirm_dialog` / `ax.cancel_dialog` | Dialog bestätigen/abbrechen |
| `ax.wait_for_form` / `ax.wait_for_dialog` / `ax.wait_for_text` | Warten auf UI-Zustand |

### 14.3 Plan-Guards extra

`if_form`, `if_dialog`, `if_tab`, `on_fail=skip`.

### 14.4 RAG → AX-Plan

Wenn ein lokales SOP-Dokument zu einer AX-Anfrage passt, generiert das System eine vorsichtige `ax.*`-Sequenz. Direkt aus dem Ask-Tab speicherbar via `save plan as ritual`.

### 14.5 Ausführung

AX-Rituale laufen im **Ritual-Runtime** statt als rohe Maus-Replay. Riskante Schritte sind **blockierbar**, Recovery-Hinweise sichtbar.

---

## 15. Live Context und Desktop Inspector

**Aktueller Stand:** [LiveContextTab](../CarolusNexus/Views/LiveContextTab.axaml.cs) zeigt **Vordergrundfenster** (Titel, Prozess, PID, Fensterklasse, Bounds) und leitet eine **Adapter-Familie** ab ([OperatorAdapterRegistry](../CarolusNexus/Services/OperatorAdapterRegistry.cs)). **AX Context** markiert Dynamics/AX-Fat-Client heuristisch; eine vollständige Steuerelement-/Grid-Liste (UIAutomation-Tiefe) ist **nicht** Bestandteil dieses Stands — dafür Vision + Plan im Ask-Tab nutzen.

- Adapter-Buttons: Abgleich gewählte Familie vs. aktives Fenster; Teach-Modus nimmt Schritte auf
- **Custom action** + **run:** Protokoll + Versuch der Ausführung über [Win32AutomationExecutor](../CarolusNexus/Services/Win32AutomationExecutor.cs) bei passenden Tokens (Safety **power-user**)

**Zielbild** (Roadmap): Form-Summary, Dialog-Summary, Tabellen-Row-Auszug, browser-/office-spezifische Kontexte ohne nur Vision zu benötigen.

---

## 16. Safety Center und Governance

### 16.1 Policy-Profile

- **strict** – maximale Vorsicht, viele Auto-Aktionen geblockt
- **balanced** – Default
- **power-user** – minimaler Schutz

### 16.2 Schalter

- **never auto-send** – keine automatischen Sendungen in Mail/Chat
- **never auto-post / book** – keine Buchungen / Posts in sensitiven Apps
- **panic stop enabled** – Panic-Button aktiv

### 16.3 Denylist

`mail, outlook, teams` (kommagetrennt) → für die gelisteten App-Familien werden risikoreiche Aktionen verweigert.

### 16.4 Risk-Gating

Hochrisiko-Rituale werden nur nach Approval ausgeführt; sensitive Send/Post/Book-Schritte sind explizit blockierbar. Recovery-Hints erklären, warum etwas blockiert wurde.

### 16.5 Governance-Felder pro Ritual

Persistiert in `automation-recipes.json`; im Tab **Rituals** unter **Governance** bearbeitbar:

- Publication-State (z. B. `draft` / `published`)
- Approval-Mode (z. B. `manual` / `auto`)
- Risiko-Level (`low` / `medium` / `high`)
- Adapter-Affinity, Confidence-Source, Max-Autonomy-Steps

### 16.6 Audit + Job-Queue

Veröffentlichte Flows können in eine kleine Approval/Queue-Liste eingereiht und einzeln approved werden. **Ausstehende und letzte Jobs** sind im Rituals-Tab (Textfeld) und in der Dashboard-Kachel **Governance + Audit** als Auszug sichtbar. Audit-Records landen im Workspace (`ritual-step-audit.jsonl` etc.).

---

## 17. Companion-Overlay (Karl Klammer am Cursor)

**Aktueller Stand:** [KarlCompanionWindow](../CarolusNexus/KarlCompanionWindow.axaml) — kleines **topmost**-Fenster mit Karl-Grafik, folgt dem Mauszeiger (Timer), auf Windows **klick-durchlässig** ([Win32ClickThrough](../CarolusNexus/Win32ClickThrough.cs)). Ein/Aus über die Checkbox **„Begleiter am Cursor"** im Header.

**Zielbild (vollständige Runtime):** Dienste wie `CompanionOverlayService` können zusätzliche Zustände steuern:

| Zustand | Bedeutung |
|---|---|
| `ready` | bereit |
| `listening` | nimmt Sprache auf |
| `transcribing` | STT läuft |
| `thinking` | LLM denkt |
| `speaking` | TTS spielt ab |
| `error` | Fehler – siehe Diagnostics |

Karl kann auf Bildschirm-Ziele „springen", die das Modell als Punkt-Tags zurückgibt (Zielintegration).

---

## 18. Tray-Icon und Hotkeys

- **Tray-Icon** in [App.axaml](../CarolusNexus/App.axaml) / [App.axaml.cs](../CarolusNexus/App.axaml.cs): Menü **Carolus Nexus öffnen** (Fenster wiederherstellen), **Beenden**. Schließen des Hauptfensters **minimiert** ins Tray (nicht beenden, außer explizit „Beenden").
- **Globaler Hotkey** `PUSH_TO_TALK_KEY` (Default `F8`): **Drücken** startet Aufnahme, **Loslassen** stoppt + STT + Ask — primär [PushToTalkHotkeyWindow](../CarolusNexus/Services/PushToTalkHotkeyWindow.cs); bei Fehler **Fallback-Polling** in [MainWindow](../CarolusNexus/MainWindow.axaml.cs).
- Die Buttons **start push-to-talk** / **stop + ask** im Ask-Tab nutzen dieselbe Aufnahme-/STT-Pipeline.
- Bei Hotkey-Konflikt oder Registrierungsfehler: Fallback-Polling; alternativ nur Button-PTT.

---

## 19. Diagnostics, History und Audit

- **Diagnostics-Tab**: Filter, **export** → `windows\data\diagnostics-<Zeitstempel>.log`, **clear logs** (Inhalt nur im Speicher bis export)
- **History-Tab**: strukturierte Liste aus **`action-history.json`** (Plan-Läufe aus Ritual/Ask-Ausführung), Suche, JSON-Detail, **create ritual from selection**
- **Audit-Records / Job-Queue:** `ritual-job-queue.json` (pending/history), `ritual-step-audit.jsonl`; Dashboard-Karte **Governance + Audit** zeigt Safety-/Settings-Kurztext und Kurzüberblick Queue

Persistenz:

- `windows\data\action-history.json` (Einträge mit `kind`, `steps`, Zeitstempel — siehe [ActionHistoryService](../CarolusNexus/Services/ActionHistoryService.cs))
- `windows\data\ritual-job-queue.json` (siehe [RitualJobQueueStore](../CarolusNexus/Services/RitualJobQueueStore.cs))
- `windows\data\ritual-step-audit.jsonl` (siehe [RitualStepAudit](../CarolusNexus/Services/RitualStepAudit.cs))
- `windows\data\watch-sessions.json`
- `windows\data\diagnostics-*.log` (Exporte)

---

## 20. Datenablage im Repository

Pfade relativ zur **Repository-Wurzel**, die [AppPaths.DiscoverRepoRoot](../CarolusNexus/AppPaths.cs) findet (Ordner `windows\` muss existieren).

| Pfad | Inhalt |
|---|---|
| `windows\.env` | Secrets (nicht committen; `.env` in [.gitignore](../.gitignore)) |
| `windows\.env.example` | Vorlage ohne Werte |
| `windows\data\settings.json` | App-Settings ([NexusSettings](../CarolusNexus/Models/NexusSettings.cs)) |
| `windows\data\knowledge\` | Lokale Wissensdateien |
| `windows\data\knowledge-index.json` | Wissens-Dateiindex + Hashes |
| `windows\data\knowledge-chunks.json` | RAG-light Chunks |
| `windows\data\knowledge-embeddings.json` | Optional: Embedding-Vektoren für semantische RAG-Suche (OpenAI) |
| `windows\data\automation-recipes.json` | Rituale (Bibliothek) |
| `windows\data\ritual-job-queue.json` | Ritual-Jobs: pending + History (Freigabe-Lauf) |
| `windows\data\ritual-step-audit.jsonl` | Audit-Zeilen je Ritual-/Plan-Schritt |
| `windows\data\watch-sessions.json` | Watch-Mode-Sessions (Rotation max. 500) |
| `windows\data\action-history.json` | Protokollierte Plan-Läufe (`plan_run`, Schritte) |
| `windows\data\diagnostics-*.log` | Diagnostics-Exporte |
| `playground\` | Default-Workdir für Codex |
| `codex output\` | Logs der lokalen CLI-Runs |
| `CarolusNexus\bin\Release\net10.0-windows\` | Build-Output (Release) |
| `docs\Carolus-Nexus-Benutzerhandbuch.md` | Dieses Handbuch |

[SOUL.md](../SOUL.md) im Root: Persona-Referenz; **Injektion in den System-Prompt** erfolgt über [SoulPrompt](../CarolusNexus/Services/SoulPrompt.cs) in [LlmChatService](../CarolusNexus/Services/LlmChatService.cs) (alle konfigurierten Provider).

---

## 21. Trigger-Phrasen für Sprachbefehle

Die Routing-Trigger sind auf deutsche Sprache optimiert. In gesprochenen oder getippten Prompts erkannt:

| Phrase | Wirkung |
|---|---|
| `nimm codex …` | Codex-CLI ausführen |
| `nimm codex mit screen …` | Codex mit Screenshot |
| `nimm claude code …` | Claude-Code-CLI |
| `nimm openclaw …` | OpenClaw-CLI |
| `Hey Karl Klammer` | Persona-Antwort: `Hey Meister, stehts zu diensten.` |

---

## 22. Bekannte Einschränkungen

- Avalonia ist hier **Windows-targeted** – die Automations-Schicht ist Win-only.
- **Kein Installer** – Build + manueller Start.
- **RAG-light:** Basis ist Chunk-Ranking über `knowledge-chunks.json`; **optional** semantische Suche über Embeddings (`knowledge-embeddings.json`, OpenAI) — Recall dennoch kein Ersatz für Enterprise-Vektordatenbanken ohne Betrieb/Monitoring.
- **AX / Live Context:** Heuristik und Vision/Pläne ja; **keine** vollständige UIAutomation-Abdeckung aller AX-Grids/Dialogs — keine Behauptung „produktionsreife AX-Steuerung“ ohne Demo-Nachweis.
- Echte UI-Ausführung aus Plänen nur bei Safety-Profil **power-user** und erlaubten Tokens ([PlanGuard](../CarolusNexus/Services/PlanGuard.cs)) — falsch konfiguriert = blockiert oder Dry-Run.
- CLI-Handoffs (Codex/Claude Code/OpenClaw) sind **One-Shot**, keine persistenten Sessions.
- Trigger-Phrasen sind auf deutsche Sprachvarianten getuned.
- Vision und Cloud-STT/TTS hängen an API-Verfügbarkeit; lokales Whisper und SAPI reduzieren Cloud-Abhängigkeit.

**Marketing vs. Ist-Stand:** Externe Claims und „Killer-Feature“-Listen gehören in [Carolus-Nexus-USP-Strategie.md](Carolus-Nexus-USP-Strategie.md) und müssen **Proof** (produktiv) vs. **Build**/**Moonshot** (Roadmap) kennzeichnen — siehe dort Legende. Dieses Kapitel **§22** ist die technische Ehrlichkeitslinie.

---

## 23. Anhang: Vollständiger Action-Token-Katalog

**Zielspezifikation** für Parser und Runtime. Diese Tokens dürfen das Modell oder ein Ritual als Plan-Schritte erzeugen.

### 23.0 Parser vs. ausführbare Runtime (Ist-Stand)

| Kategorie | Parser / Vorschau ([ActionPlanExtractor](../CarolusNexus/Services/ActionPlanExtractor.cs)) | Echte Ausführung ([Win32AutomationExecutor](../CarolusNexus/Services/Win32AutomationExecutor.cs), nur Windows + Safety **power-user** + [PlanGuard](../CarolusNexus/Services/PlanGuard.cs)) |
|-----------|---------------------------------------------------------------------------------------------|----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| `[ACTION:hotkey\|…]`, `type`, `open`, `click`, `move` | ja | **ja** (soweit Token erlaubt) |
| `http(s)://…`, `explorer.open_path`, `browser.open` | ja | **ja** (Shell) |
| `ax.*`, `browser.*`, `word.*`, …, generische `app\|…` | ja (Erkennung) | **nein** — typischerweise Simulation / `[SKIP]` in der Runtime; Vision + manuelle Schritte nutzen |
| `[ACTIONS:…]` / Ketten | teils erkannt | nicht als Chain — Einzelschritte wie oben |

Über die Zeit: Adapter-Schicht (UIA/App-spezifisch) erweitern; Katalog bleibt Zielbild.

### 23.1 Generisch

| Token | Argument | Wirkung |
|---|---|---|
| `[ACTION:move]` | Zielkoordinate / Punkt-Tag | Mauszeiger bewegen (mit Bestätigung) |
| `[ACTION:click]` | – | Linksklick (mit Bestätigung) |
| `[ACTION:open|target]` | App/URL/Pfad | Etwas öffnen (mit Bestätigung) |
| `[ACTION:type|text]` | Text | Text tippen (mit Bestätigung) |
| `[ACTION:hotkey|shortcut]` | z. B. `Ctrl+S` | Tastenkürzel auslösen |
| `[ACTIONS:…]` | mehrere | Action-Chain (z. B. open → wait → type) |

Chain-Direktiven vor jedem Token: `wait=500`, `ifapp=chrome`.

### 23.2 Generische App-Adapter (`app|...`)

- `focus_window`
- `focus_control:<Name>`
- `click_control:<Name>`
- `type_control:<Field>=<Value>`
- `save`, `confirm`, `cancel`, `next_field`, `next_tab`
- `list_controls`, `read_control:<Name>`, `activate_tab:<TabName>`
- `read_form`, `read_table`, `read_dialog`, `read_selected_row`

### 23.3 App-spezifisch

- **Browser**: `browser.focus_address`, `browser.open:<url>`, `browser.search:<q>`, `browser.find:<t>`, `browser.refresh`
- **Explorer**: `explorer.focus_path`, `explorer.open_path:<p>`, `explorer.focus_search`, `explorer.search:<q>`, `explorer.open_selected`
- **Mail**: `mail.focus_search`, `mail.new_message`, `mail.find:<t>`
- **Editor**: `editor.command_palette`, `editor.quick_open`, `editor.find:<t>`, `editor.save`, `editor.run`
- **Outlook / Teams / Word / Excel / PowerPoint / OneNote**: `outlook.*`, `teams.*`, `word.*`, `excel.*`, `powerpoint.*`, `onenote.*`

### 23.4 AX 2012

`ax.read_context`, `ax.read_form`, `ax.read_field`, `ax.set_field`, `ax.click_action`, `ax.open_tab`, `ax.open_lookup`, `ax.read_grid`, `ax.select_grid_row`, `ax.confirm_dialog`, `ax.cancel_dialog`, `ax.wait_for_form`, `ax.wait_for_dialog`, `ax.wait_for_text`.

### 23.5 Ritual-Step-Guards

`if_app`, `if_form`, `if_dialog`, `if_tab`, `on_fail=skip|stop`, `wait_ms`, `retry_count`.

---

## 24. USP-Strategie und KI-Positionierung (Marketing)

Vertiefende **Positionierung**, **KI-Killer-Features** (Tier A/B/C mit Proof/Build/Moonshot), **Wettbewerbs-Matrix**, **Demo-Skript** und **Messaging-Don’ts**: [Carolus-Nexus-USP-Strategie.md](Carolus-Nexus-USP-Strategie.md). Technischer Implementierungsstand bleibt maßgeblich **§1.1** und **§22**.

---

*Pflege:* Bei größeren Struktur- oder Feature-Änderungen **§1.1**, **§3**, **§5** und **§20** mit dem Code abgleichen. Kurz-Checkliste: [AGENTS.md](../AGENTS.md).*

*Repo-Stand (**RW-Avalonia-Karl-Klammer**): **Carolus Nexus** ([CarolusNexus/](../CarolusNexus/), Avalonia, **.NET 10**) — Ist-Stand siehe **§1.1**; Messaging/Zielgruppen siehe [Carolus-Nexus-GTM-Messaging.md](Carolus-Nexus-GTM-Messaging.md), [Carolus-Nexus-ICP-Personas.md](Carolus-Nexus-ICP-Personas.md) und [Carolus-Nexus-USP-Strategie.md](Carolus-Nexus-USP-Strategie.md).*
