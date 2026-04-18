# Carolus Nexus – Benutzerhandbuch

> **Karl Klammer** ist die Companion-Persona, **Carolus Nexus** das Produkt:
> ein Windows-Operator-Desktop auf Avalonia (.NET 10), der Sprache, Vision-LLMs,
> lokales Wissen, Ritual-Automation und Fat-Client-Steuerung in einer Oberfläche bündelt.

---

## Inhaltsverzeichnis

1. [Was ist Carolus Nexus?](#1-was-ist-carolus-nexus)
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

Karl Klammer ist die **Identität/Stimme** des Assistenten (siehe `SOUL.md`).

---

## 2. Systemvoraussetzungen

- **Windows** (die Avalonia-App ist hier Windows-targeted, da die Automations-Schicht Windows-spezifisch ist)
- **.NET 10 SDK**
- Mindestens **ein Provider-Schlüssel**: Anthropic *oder* OpenAI *oder* OpenAI-kompatibler Endpoint
- *Optional:* ElevenLabs (Voice-ID + Key) für STT/TTS
- *Optional:* Lokales **Whisper** (Python-Installation) für STT ohne Cloud
- *Optional:* Lokal installierte CLIs **codex**, **claude**, **openclaw**
- Für PDF-/DOCX-Wissen: `UglyToad.PdfPig` und `DocumentFormat.OpenXml` werden via NuGet von der Legacy-Runtime mitgenutzt.

---

## 3. Installation und Start

### Bauen

```cmd
cd avalonia
Build-Avalonia.cmd
```

Skript ruft `dotnet build ClippyRW.Avalonia.csproj -c Release` auf.

### Starten

```cmd
cd avalonia
Start-Avalonia.cmd
```

Skript baut zuerst Release und führt dann
`dotnet bin\Release\net10.0-windows\CarolusNexus.dll` aus.

Beim ersten Start:

1. `windows\.env.example` nach `windows\.env` kopieren.
2. Mindestens einen Provider-Key eintragen (`ANTHROPIC_API_KEY` oder `OPENAI_API_KEY`).
3. Optional: Lokale Wissensdateien in `windows\data\knowledge\` ablegen.
4. App starten, im Header **„refresh all"** klicken.
5. Im Tab **Setup** Provider, Modus und Modell prüfen → **„save settings"**.
6. Im Tab **Ask** prompten oder Push-to-Talk verwenden.

---

## 4. Konfiguration (`.env` und `settings.json`)

### 4.1 `windows\.env` (Secrets, **nicht** in Git)

| Variable | Default | Zweck |
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
| `PUSH_TO_TALK_KEY` | `F8` | Globaler PTT-Hotkey |

Reload zur Laufzeit über den Header-Button **„refresh all"**.

### 4.2 `windows\data\settings.json` (lokale Einstellungen, kein Secret)

Wird über den **Setup**-Tab gepflegt. Enthält u. a.:

- gewählter **Provider** (`anthropic` / `openai` / `openai-compatible`)
- gewähltes **Modell**
- **Mode** (`companion` / `agent` / `automation` / `watch`)
- `SpeakResponses`, `UseLocalKnowledge`, `SuggestAutomations`
- **Safety**-Einstellungen (Profil, Denylist, Auto-Send-Sperre, Panic-Stop)

---

## 5. Oberfläche – Tour durch die Tabs

Die Hauptoberfläche besteht aus einem **Hero-Header** und einem **Tab-Bereich**.

### 5.1 Hero-Header (immer sichtbar)

- **Titel + Subtitle + Status-Meldung**
- Badges für **Layout, Environment, Speech, Automation, Knowledge**
- Live-Kacheln **Operator Memory / Live Context / Environment**
- Globale Buttons:
  `refresh all` · `save settings` · `reindex knowledge` · `refresh active app` · `export diagnostics`

### 5.2 Tab **Ask**

- Prompt-Eingabe + Optionen `include screenshots` und `use local knowledge in ask flow`
- Buttons:
  `ask now` · `smoke test` · `import audio + transcribe` · `start push-to-talk` · `stop + ask` · `cancel recording` · `clear conversation` · `run plan` · `approve + run` · `run next step` · `save plan as ritual` · `clear plan` · `panic stop`
- Hinweis-Zeile zum AX-Fat-Client-Support
- Ausgabebereich: **Assistant Response**, **Retrieval + Context**, **Action Plan Preview**, **Action Plan Execution**
- Anzeigen für **Safety-Policy**, **Failure-Recovery-Hints**, **letztes Transcript**, **letzte generierte Audio-Datei**, **„speak response"** Button

### 5.3 Tab **Dashboard**

Sieben Karten (Wrap-Layout):

1. Environment + Routing (Provider / Mode / Model / `.env`-Summary)
2. Knowledge + Memory (Status, Counts, Operator-Metriken)
3. Live Context (aktive App + AX-Vorschläge)
4. Proactive Karl (Suggestion + Details + Safety-Hinweis)
5. Governance + Audit
6. Recent Rituals (Liste)
7. Recent Watch Sessions (Liste)

### 5.4 Tab **Setup**

- Provider-Auswahl, Modus, Modellname
- Schalter `speak responses` / `use local knowledge` / `suggest automations`
- **Safety Center**: Policy-Profil, „never auto-send", „never auto-post / book", „panic stop enabled", Denylist
- Health-Summary mit `.env`-Pfad und kompletter Variablenübersicht

### 5.5 Tab **Knowledge**

- Suche über Dokumente und indexierte Texte
- `search` / `import files` / `remove doc` / `reindex` / `suggest ritual from doc`
- Dokumentliste links, **Document Preview** rechts

### 5.6 Tab **Rituals**

Drei Spalten: **Ritual Library**, **Ritual Builder**, **Structured Steps**.

- **Library**: Suche, Filter nach Category / Source / Risk, Ritual-Stats
- **Builder**: Name, Description, Mode, Category, Source-Type, Risk, Publication, Approval-Mode, Adapter-Affinity, Confidence-Source, Max-Autonomy-Steps, Guards (App/Form/Dialog/Tab), Tags, Knowledge-Sources, Prompt
  Buttons: `save ritual` · `delete ritual` · `clone ritual` · `archive ritual` · `publish flow` · `queue for run` · `approve next job` · `dry run` · `run ritual` · `run next step` · `resume ritual`
- **Structured Steps**: Schritt-Editor mit Action-Type, Argument, Wait-ms, Retry, Guards (`if app/form/dialog/tab`), `on fail`
  + **Parameter-Editor** für Platzhalter wie `{{customer_account}}`
  + **Teach Mode**: `promote from history`, `promote from watch`, `start teach`, `stop teach`, Vorschau und Run-Log

### 5.7 Tab **History**

- Action-History mit Suchfilter, Detail-Ansicht
- **„create ritual from selection"** mit konfigurierbarem **promote count**

### 5.8 Tab **Diagnostics**

- Filtern, **export** (`windows/data/diagnostics-*.log`), **clear logs**

### 5.9 Tab **Console**

- Lokaler **Agent-Console** für Codex / Claude Code / OpenClaw
- Prompt-Eingabe, **„run selected agent"**, Pfad der Output-Datei, vollständiges Run-Output

### 5.10 Tab **Live Context**

- **Active Window**: kompletter Kontext-Snapshot
- **Cross-App Adapter**-Kontext
- **AX Context**-Snapshot mit Forms/Dialogs/Tabs/Fields
- **Desktop Inspector** mit Buttons für jeden Adapter (Explorer, Browser, Mail, Outlook, Teams, Word, Excel, PowerPoint, OneNote, Editor, AX) und freier Aktion via `InspectorAction`

---

## 6. Hauptarbeitsabläufe

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

- **History**-Tab → Eintrag wählen → **„create ritual from selection"**.
- **Rituals**-Tab → **„promote from history"** / **„promote from watch"**.
- **Teach-Mode** zeichnet neu bestätigte Aktionen auf und schlägt einen Ritual-Draft vor.

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

`smoke test` im Ask-Tab schickt eine Mini-Anfrage `say ready` an den aktuellen Provider und zeigt das Ergebnis – schneller End-to-End-Healthcheck.

---

## 8. Sprache: Push-to-Talk, STT und TTS

### 8.1 Mikrofon-Aufnahme

`MicrophoneRecorderService` nutzt **MCI** (`mciSendString`) und schreibt eine WAV in
`windows\data\` / temporäre Datei. Drei Wege:

- Button **„start push-to-talk"** → **„stop + ask"**
- **Cancel** verwirft die Aufnahme
- **Globaler Hotkey** triggert dieselben Methoden im ViewModel

### 8.2 Speech-to-Text

- `STT_PROVIDER=elevenlabs` → ElevenLabs Speech-to-Text-API
- `STT_PROVIDER=whisper` → Lokales Whisper über `WHISPER_PYTHON`, Modell `WHISPER_MODEL`, Sprache `WHISPER_LANGUAGE`

### 8.3 Text-to-Speech

ElevenLabs erzeugt MP3 unter dem Daten-Ordner. Falls fehlgeschlagen, Fallback auf Windows-SAPI. Pfad zur letzten Audio-Datei wird in der UI angezeigt.

---

## 9. Lokale CLI-Agenten: Codex, Claude Code, OpenClaw

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

- Dateien in `windows\data\knowledge\` werden indexiert und in **Chunks** zerlegt.
- Unterstützte Formate: `.txt`, `.md`, `.log`, `.json`, `.csv`, `.pdf`, `.docx`.
- Index liegt in `windows\data\knowledge-index.json`.
- **Knowledge**-Tab erlaubt Import/Search/Reindex/Remove und liefert Vorschau.
- Bei aktivem `use local knowledge in ask flow` werden relevante Chunks dem Prompt beigelegt; Quellen erscheinen in **Retrieval + Context**.
- **„suggest ritual from doc"** generiert aus Dokumenten einen Ritual-Vorschlag (z. B. aus AX-Arbeitsanweisungen).

---

## 11. Rituale (Automation Recipes)

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

- **promote from history**: aus letzten N History-Einträgen ein Ritual machen
- **promote from watch**: aus Watch-Sessions
- **start/stop teach**: Live-Recorder, der bestätigte Aktionen einfängt

Persistente Speicherung: `windows\data\automation-recipes.json`.

---

## 12. Action-Plans und Plan-Ausführung

Modellantworten können Aktionen vorschlagen. Diese werden als **Plan** geparst und mehrstufig behandelt:

1. **Vorschau** (`Action Plan Preview`) zeigt alle geplanten Schritte.
2. **`approve + run`** akzeptiert den Plan und führt ihn aus, sofern die Safety-Policy es zulässt.
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

Jeder Adapter exportiert **semantische Aktionen**, die sowohl in Action-Plans als auch im Inspector verwendbar sind.

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

Der **Desktop Inspector** ist Read-Only und zeigt:

- Steuerelementliste der aktiven App
- Form-Summary, Dialog-Summary
- Tabellen-/Selected-Row-Auszug
- Browser-/Explorer-/Mail-/Office-spezifische Kontexte über die Adapter-Buttons
- AX-spezifische Snapshots

**„custom action"** akzeptiert beliebige Aktions-Strings (z. B. `app|focus_address`, `ax.read_field:Customer`).

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

- Publication-State (z. B. `draft` / `published`)
- Approval-Mode (z. B. `manual` / `auto`)
- Adapter-Affinity, Confidence-Source, Max-Autonomy-Steps

### 16.6 Audit + Job-Queue

Veröffentlichte Flows können in eine kleine Approval/Queue-Liste eingereiht und einzeln approved werden. Audit-Records landen im Workspace.

---

## 17. Companion-Overlay (Karl Klammer am Cursor)

`CompanionOverlayService` + `CompanionOverlayWindow` erzeugen eine **immer-im-Vordergrund** Mini-Bühne neben dem Mauszeiger.

Zustände:

| Zustand | Bedeutung |
|---|---|
| `ready` | bereit |
| `listening` | nimmt Sprache auf |
| `transcribing` | STT läuft |
| `thinking` | LLM denkt |
| `speaking` | TTS spielt ab |
| `error` | Fehler – siehe Diagnostics |

Karl kann auf Bildschirm-Ziele „springen", die das Modell als Punkt-Tags zurückgibt.

---

## 18. Tray-Icon und Hotkeys

- **Tray-Icon** „Carolus Nexus" (NotifyIcon) mit Kontextmenü:
  - „Open Carolus Nexus"
  - „Ask from current prompt"
  - „Quit"
- **Doppelklick** öffnet das Hauptfenster.
- **Globaler Hotkey** `PUSH_TO_TALK_KEY` (Default `F8`):
  - **Drücken** startet die Sprachaufnahme
  - **Loslassen** stoppt + transkribiert + Ask
- Bei Hotkey-Konflikt funktioniert der „hold to talk"-Button weiterhin.

---

## 19. Diagnostics, History und Audit

- **Diagnostics-Tab**: filtern, Detail-Ansicht, **export** (`windows\data\diagnostics-*.log`), `clear logs`.
- **History-Tab**: Action-History mit Suche, Detail, „create ritual from selection".
- **Audit-Records**: lightweight, sichtbar in Dashboard-Karte „Governance + Audit".

Persistenz:

- `windows\data\action-history.json`
- `windows\data\watch-sessions.json`
- `windows\data\diagnostics-*.log` (Exporte)

---

## 20. Datenablage im Repository

| Pfad | Inhalt |
|---|---|
| `windows\.env` | Secrets (nicht committen) |
| `windows\data\settings.json` | App-Settings |
| `windows\data\knowledge\` | Lokale Wissensdateien |
| `windows\data\knowledge-index.json` | Wissens-Chunkindex |
| `windows\data\automation-recipes.json` | Rituale |
| `windows\data\watch-sessions.json` | Watch-Mode-Sessions |
| `windows\data\action-history.json` | Bestätigte Aktionen |
| `windows\data\diagnostics-*.log` | Diagnostics-Exporte |
| `playground\` | Default-Workdir für Codex |
| `codex output\` | Logs der lokalen CLI-Runs |
| `avalonia\bin\Release\net10.0-windows\` | Build-Output Avalonia |

`SOUL.md` im Root liefert die Persona für Karl Klammer und wird in den Anthropic-System-Prompt injiziert.

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
- Tiefere AX-spezifische UIA/MSAA-Discovery ist nur teilweise portiert (Win32-First-Hybrid).
- AX-Grid-Row-Selection, Lookup-Handling und End-to-End-Posting sind **noch nicht für alle AX-Surfaces** robust.
- Der Teach-Mode-Recorder ist noch history-backed, kein voll-semantischer Recorder.
- CLI-Handoffs (Codex/Claude Code/OpenClaw) sind **One-Shot**, keine persistenten Sessions.
- Trigger-Phrasen sind auf deutsche Sprachvarianten getuned.
- Sprache und Vision hängen an externer API-Verfügbarkeit (außer lokales Whisper).

---

## 23. Anhang: Vollständiger Action-Token-Katalog

Diese Tokens dürfen das Modell oder ein Ritual als Plan-Schritte erzeugen.

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

*Dieses Handbuch beschreibt den Stand des Avalonia-App-Surface (`avalonia/`) inklusive geerbter Daten/Integrationen aus `windows/`. Detail-Updates bei größeren Struktur-Änderungen erfolgen analog zu `AGENTS.md`.*

*Repo-Hinweis (**RW-Avalonia-Karl-Klammer**): Hier liegt die Demo-App **KarlKlammer** (Avalonia, .NET 8) mit u. a. 3D-Szene, Karl-Mauszeiger und optionalem **Begleiter am Cursor** (`KarlCompanionWindow`). Die Tabs, RAG-, Ritual-, AX- und Provider-Funktionen aus diesem Handbuch sind in diesem Arbeitsverzeichnis nicht enthalten — sie beschreiben das Zielprodukt Carolus Nexus.*
