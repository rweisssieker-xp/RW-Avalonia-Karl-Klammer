# Carolus Nexus – USP-Strategie und KI-Killer-Features

**Stand:** Abgestimmt auf den Implementierungsüberblick in [Carolus-Nexus-Benutzerhandbuch.md §1.1](Carolus-Nexus-Benutzerhandbuch.md) (Branch `CarolusNexus/`).  
**Kurzfassung:** Carolus Nexus ist der **multimodale Operator-Copilot mit governiertem Ausführungs- und Audit-Loop** auf dem **Windows-Desktop** — nicht „noch ein Chatfenster“, sondern die **Kombination** aus Vision, lokalem Wissen, strukturierten Plänen, Freigaben, Rituale/Queue und BYO-LLM in **einer** Shell.  
**EN (optional pitch):** A Windows-native operator shell that closes the loop from multimodal understanding to **governed execution** and **auditable history** — with BYO models and local data paths.

**Legende für Killer-Features**

| Tag | Bedeutung |
|-----|-----------|
| **Proof** | Heute im Produkt nachweisbar (Code/UI/Pfade). |
| **Build** | Roadmap; Akzeptanzkriterium und Komplexität angegeben. |
| **Moonshot** | Experiment; nicht als Lieferversprechen kommunizieren. |

---

## Inhaltsverzeichnis

1. [Positionierung](#1-positionierung)
2. [Vier Unique-Value-Säulen](#2-vier-unique-value-säulen)
3. [KI-Leitbild](#3-ki-leitbild)
4. [Killer-Features Tier A / B / C](#4-killer-features-tier-a--b--c)
5. [Competitive-Matrix](#5-competitive-matrix)
6. [ICP × Säule × Top-KI-Feature](#6-icp--säule--top-ki-feature)
7. [15-Minuten-Demo-Skript](#7-15-minuten-demo-skript)
8. [Messaging-Don’ts und Glossar](#8-messaging-donts-und-glossar)
9. [Pflege und Ownership](#9-pflege-und-ownership)

---

## 1. Positionierung

**Ein Satz (DE):** Carolus Nexus bündelt **Sehen** (Multi-Monitor-Vision, optional UIA), **Verstehen** (lokales Wissen, optional Embeddings) und **Ausführen unter Kontrolle** (Pläne, Safety, Freigaben, Ritual-Queue, Audit) für **echte Desktop- und Fat-Client-Arbeit** auf Windows.

**KI-Killer-Zusatzsatz:** Die KI liefert nicht nur Text, sondern **strukturierte Schritte**, **Risiko-Erklärung**, **Automationsvorschläge** und optional **UIA-Kontext** — gekoppelt an **Human-in-the-Loop** (z. B. „freigeben + ausführen“, veröffentlichte Rituale mit manueller Freigabe).

**Zielgruppe (Halbsatz):** Windows-Operatoren, Dev+Ops-Teams und regulierte Umgebungen, die **einen Pane** statt einer Tool-Kette brauchen.

---

## 2. Vier Unique-Value-Säulen

### Säule 1 – Governierter Ausführungs-Loop

| | |
|--|--|
| **Claim** | Pläne und Rituale laufen **nicht blind**: Freigaben, Queue, Publikations-/Approval-Modus und Audit. |
| **Warum selten** | Viele KI-Clients enden im Chat; RPA-Tools enden oft ohne **LLM-Verständnis** in derselben UX. |
| **Proof** | [AskTab](../CarolusNexus/Views/AskTab.axaml.cs) (u. a. „freigeben + ausführen“), [RitualJobQueueStore](../CarolusNexus/Services/RitualJobQueueStore.cs), [RitualsTab](../CarolusNexus/Views/RitualsTab.axaml.cs) (published + manual → kein Direktlauf), `windows/data/ritual-job-queue.json`, [RitualStepAudit](../CarolusNexus/Services/RitualStepAudit.cs) / `ritual-step-audit.jsonl`. |
| **Risiko / Anti-Hype** | Echte Win32-Ausführung nur mit Safety **power-user** und erlaubten Tokens; viele `ax.*`/Adapter-Tokens bleiben Simulation — siehe Handbuch **§23.0**. |

### Säule 2 – Native Windows-Operator-Shell

| | |
|--|--|
| **Claim** | **Eine** Avalonia-Shell: Vision, Sprache, Wissen, Pläne, CLI, Tray, PTT — ohne Browser als Pflicht. |
| **Warum selten** | Browser-Chat und M365 sind stark, aber **Fat-Client + Multi-Monitor + lokaler Ordner** als Erstklass-Workflow sind selten integriert. |
| **Proof** | [MainWindow](../CarolusNexus/MainWindow.axaml.cs), [LlmChatService](../CarolusNexus/Services/LlmChatService.cs), [KnowledgeSnippetService](../CarolusNexus/Services/KnowledgeSnippetService.cs), [CliAgentRunner](../CarolusNexus/Services/CliAgentRunner.cs), [App.axaml](../CarolusNexus/App.axaml) Tray. |
| **Risiko / Anti-Hype** | Plattform **Windows-targeted** für Automation; kein Installer im aktuellen Stand (Handbuch §22). |

### Säule 3 – Trust-UX (Karl Klammer)

| | |
|--|--|
| **Claim** | Sichtbare **Zustände** (ready / listening / thinking / …) reduzieren „Black Box“-Gefühl. |
| **Warum selten** | Die meisten Assistenten haben keine **persistent sichtbare** Begleitung am Cursor mit Zustandsmaschine. |
| **Proof** | [KarlCompanionWindow](../CarolusNexus/KarlCompanionWindow.axaml), [CompanionHub](../CarolusNexus/Services/CompanionHub.cs), Header-Toggle. |
| **Risiko / Anti-Hype** | „Springen zu Punkt-Tags“ ist **Zielbild** (Handbuch §17), nicht vollständig als Produktversprechen ausgeben. |

### Säule 4 – Fat-Client-/ERP-Zielbild mit ehrlicher Tiefe

| | |
|--|--|
| **Claim** | AX-/Adapter-Narrativ, `ax.*`-Tokens, Live Context und optional **UIA-Snapshot im Ask** für bessere Fat-Client-Prompts. |
| **Warum selten** | ERP-Fat-Client + moderne Multimodal-KI in **einer** Operator-Oberfläche ist eine klare Nische. |
| **Proof** | [OperatorAdapterRegistry](../CarolusNexus/Services/OperatorAdapterRegistry.cs), [LiveContextTab](../CarolusNexus/Views/LiveContextTab.axaml.cs), [UiAutomationSnapshot](../CarolusNexus/Services/UiAutomationSnapshot.cs), Setup „UIA im Ask“. |
| **Risiko / Anti-Hype** | **Keine** behauptete produktionsreife **vollständige** AX-UIA-Steuerung ohne Demo-Nachweis (Handbuch §22, §23.0). |

---

## 3. KI-Leitbild

**Verstehen:** Die KI arbeitet **multimodal** (Screenshots mehrerer Monitore, optional gekürzter UIA-Baum des Vordergrundfensters) und **kontextuell** mit lokalem Wissen (Chunk-Ranking, optional Embeddings über `knowledge-embeddings.json`).

**Entscheiden:** Antworten werden in **Pläne** übersetzt: Regex-/Token-Extraktion, bei Bedarf **JSON** (`steps`), **KI: strukturierte Schritte**, **Plan erklären / Risiko** und **SuggestAutomations** — bei Ausführung greifen **Safety-Profil**, **PlanGuard** und **explizite Freigabe** (wo vorgesehen).

**Ausführen und lernen:** Pläne lassen sich **speichern**, **als Ritual** persistieren, aus **History/Watch promoten**, in die **Job-Queue** legen und nach **Approve** mit **History/Audit** nachvollziehen — das ist **Wiederholbarkeit und Governance**, nicht Einmal-Chat.

**Human-in-the-Loop (Bullets)**

- „freigeben + ausführen“ im Ask (Dialog unter Windows) vs. direkter „run plan“.
- Ritual **published** + **manual** → Direktlauf gesperrt, stattdessen Queue + „approve next job“.
- Safety **power-user** bewusst wählen für echte Win32-Schritte; sonst Simulation.

---

## 4. Killer-Features Tier A / B / C

### 4a. Tier A (**Proof**) – bereits nutzbar

| Feature | Nutzen | Proof (Repo) |
|--------|--------|----------------|
| Multimonitor-Vision-Ask | Echter Desktop-Kontext | [LlmChatService.cs](../CarolusNexus/Services/LlmChatService.cs), Tab Ask |
| Lokales Wissen + optional Embeddings | SOPs ohne Enterprise-Search-Projekt | [KnowledgeSnippetService.cs](../CarolusNexus/Services/KnowledgeSnippetService.cs), [EmbeddingRagService.cs](../CarolusNexus/Services/EmbeddingRagService.cs), Handbuch §10 / §20 |
| Plan → strukturierte Schritte | Weniger Parser-Bruch | [ActionPlanExtractor.cs](../CarolusNexus/Services/ActionPlanExtractor.cs), [PlanJsonParser.cs](../CarolusNexus/Services/PlanJsonParser.cs), [LlmStructuredPlanService.cs](../CarolusNexus/Services/LlmStructuredPlanService.cs) |
| Plan erklären / Risiko | Dry-Run-Begleittext | Ask „Plan erklären / Risiko“, [LlmChatService.cs](../CarolusNexus/Services/LlmChatService.cs) |
| UIA-Snapshot im Ask | Fat-Client-Prompts | [UiAutomationSnapshot.cs](../CarolusNexus/Services/UiAutomationSnapshot.cs), [NexusSettings.cs](../CarolusNexus/Models/NexusSettings.cs), Setup |
| Freigabe-Gate vor Run | Vertrauen / Compliance-Narrativ | Ask „freigeben + ausführen“ |
| SuggestAutomations nach Ask | Zweiter Nutzen pro Session | [AskTab.axaml.cs](../CarolusNexus/Views/AskTab.axaml.cs), Setup-Schalter |
| Proactive Watch-Hinweis | Ambient Intelligence | [MainWindow.axaml.cs](../CarolusNexus/MainWindow.axaml.cs) `TryProactiveHintAsync`, Modus watch + Setup |
| CLI-Handoff + Screen | Dev+Ops in derselben Shell | [CliAgentRunner.cs](../CarolusNexus/Services/CliAgentRunner.cs), Ask-Router, Handbuch §9 |

### 4b. Tier B (**Build**) – Roadmap

Jede Zeile ist explizit als **Build** (noch nicht als Produktversprechen zu verkaufen) gekennzeichnet.

| # | Status | Feature | Akzeptanz (1 Satz) | Komplexität | Abhängigkeit |
|---|--------|--------|---------------------|-------------|----------------|
| 1 | **Build** | Native Tool-/Function-Calling für `steps[]` | Golden-Tests: ≥90 % stabile Schritt-Extraktion auf feste Beispiel-Prompts. | L | Provider-API, Schema-Pflege |
| 2 | **Build** | Ritual-QA vor Speichern (LLM + Denylist/Waits/Risk) | Rituals-Tab zeigt Blocker + diffbaren Vorschlag vor Save. | M | Prompt-Design, Settings |
| 3 | **Build** | Risk-Score + härteres Gate | Bei `high` zweistufige Freigabe; Score in SafetyOut. | M | Heuristik + optional LLM |
| 4 | **Build** | Konversationsgedächtnis (opt-in) | Follow-up ohne vollständigen Re-Prompt; Kostenlimit im Setup. | M | Speicher, Kontextfenster |
| 5 | **Build** | UIA + Vision Fusion | Referenz-App: strukturierte Felderliste aus UIA + Ausschnitt im Ask. | L | Fensterwahl, Token-Budget |
| 6 | **Build** | Companion „Springen zu Ziel“ | Demo: Fokus/Highlight auf Referenz-Dialog aus Modell-Output. | L | Koordinaten/Control-IDs |
| 7 | **Build** | Self-Heal nach Fehllauf | Ein Klick „Vorschlag übernehmen“ aus History/Audit + LLM. | L | UI, Parsing |
| 8 | **Build** | Voice Barge-in | TTS bricht bei neuer PTT zuverlässig ab. | S | Audio-Pipeline |

### 4c. Tier C (**Moonshot**)

Nur **Experiment** — nicht als Roadmap-Datum oder Lieferversprechen kommunizieren.

| Idee | Stolperstein (Policy/Ethik) |
|------|-------------------------------|
| On-Device Guard-Summary (kleines lokales Modell) | Verteilung/Updates, Modellqualität, was darf lokal klassifiziert werden? |
| Org-weites Ritual-Marketplace lite (signiert) | Vertrauenskette, Signing, Import-Governance |
| Laufzeit-Lernen aus Fehlermustern | Einwilligung, Anonymisierung, Aufbewahrung |
| **Closed-loop Eval-Harness** (synthetische Desktop-Szenarien für Regression) | Aufwand, Repräsentativität, kein „Spiegel der Produktion“ ohne Abstimmung |
| **Geteilte Wissens-Kontexte** (team-intern, ohne globales Scraping) | Zugriffsrechte, Löschfristen, Trennung personenbezogen / betrieblich |

---

## 5. Competitive-Matrix

| Dimension | Carolus Nexus | Typ. M365-Copilot | Typ. Browser-Chat | Reine RAG | Reine RPA |
|-----------|---------------|-------------------|-------------------|-----------|-----------|
| Fat-Client + Multi-Monitor | Kern | Teilweise | Schwach | oft irrelevant | custom/teuer |
| Lokale Ordner ohne Tenant-Lock-in | stark | Tenant-zentriert | Upload-Reibung | stark | schwach |
| Governierte Ausführung + Audit | stark (Queue, Gates, Logs) | variiert | meist nein | nein | stark, schwer |
| BYO-LLM / Endpoint | stark | nein | teilweise | teilweise | variiert |
| Human-in-the-Loop sichtbar | stark (Karl + Gates) | variiert | schwach | schwach | oft nur Dev |

**Wann wir nicht konkurrieren:** Wir positionieren uns **nicht** als Ersatz für **Voll-M365-Copilot** in Word/Excel-Inline, **nicht** als Enterprise-Search mit globalem Index über alle Mandanten, und **nicht** als „vollautomatische AX-Produktion“ ohne Nachweis. Stattdessen: **Kombination X unter Bedingung Y** (z. B. regulierter Windows-Operator, lokales `knowledge\`, Freigabe-Pflicht, BYO-Endpoint).

---

## 6. ICP × Säule × Top-KI-Feature

Siehe ausführlich [Carolus-Nexus-ICP-Personas.md](Carolus-Nexus-ICP-Personas.md).

| ICP | Stärkste Säule | Top-KI-Feature (Stichwort) |
|-----|----------------|----------------------------|
| **ICP-A** (AX-heavy) | Fat-Client-Zielbild | Vision + UIA-Snapshot + `ax.*`-Pläne |
| **ICP-B** (reguliert) | Governierter Loop | Queue, Audit, lokale Pfade, BYO |
| **ICP-C** (Dev+Ops) | Native Shell | CLI-Handoff + Screen-Zusammenfassung |
| **ICP-D** (Knowledge) | Verstehen | RAG-light, Embeddings optional, Ritual aus Plan |
| **ICP-E** (Sponsor) | Governance / Trust | Safety-Staffelung, Freigaben, Karl-Zustände |

---

## 7. 15-Minuten-Demo-Skript

| Szene | Dauer | Erwarteter Bildschirm / Ablauf |
|-------|-------|--------------------------------|
| **1 Governance-Queue** | 4 min | Tab Rituals: Ritual speichern, „queue for run“, Dashboard oder Queue-Detail; „approve next job“; History sichtbar. |
| **2 Ask: Vision + Wissen + Plan + Freigabe** | 5 min | Ask: lokales Wissen an, optional Screenshot; „ask now“; Plan-Vorschau; „Plan erklären“ in SafetyOut; „freigeben + ausführen“ vs „run plan“ erklären. |
| **3 CLI-Handoff** | 3 min | Prompt mit CLI-Route (Handbuch §9); Ausgabe + Log-Pfad zeigen. |
| **4 Optional Watch + Proactive** | 3 min | Modus watch, Proactive in Setup; Dashboard-Hinweis erwähnen (API-Key nötig). |
| **5 Optional KI-Tiefgang** | +3 min | Setup: UIA im Ask aktiv; nach Antwort: „JSON-Plan aus Antwort“ oder „KI: strukturierte Schritte“, dann „Plan erklären / Risiko“, abschließend „freigeben + ausführen“ (Windows-Dialog). |

---

## 8. Messaging-Don’ts und Glossar

### Don’ts (mindestens diese acht)

1. Nicht behaupten: „Unser LLM ist das beste Modell.“
2. Nicht: „Vollautomatische AX-Produktion garantiert.“
3. Nicht: „Ersetzt Enterprise Search / Vektordatenbank ohne Einschränkung.“
4. Nicht: „Sicher nur durch KI“ — Sicherheit ist **Profil + Guard + Operator**.
5. Nicht: „Copilot ist überall schlechter“ — differenzieren nach **Szenario**.
6. Nicht: Marketing ohne **Proof**-Tag bei Tier A vs **Build** bei Tier B.
7. Nicht: Tier-C als Liefertermin kommunizieren.
8. Nicht: DSGVO/Privacy „gelöst“ behaupten ohne konkrete Kunden-Policy zu Watch/Logs.

### Erlaubte Superlative (nur mit Bedingung)

- „Unschlagbar in der **Kombination** [Fat-Client + lokales Wissen + governierte Ausführung] **für Teams, die …**“
- „Stärker als reiner Browser-Chat **für Multi-Monitor-Desktop-Operate**, sofern …“

### Glossar (Auszug)

| Begriff | Kurzdefinition |
|---------|------------------|
| **Operator-Shell** | Eine Desktop-Oberfläche zum Steuern echter Apps, nicht nur Web. |
| **BYO-LLM** | Bring-your-own API-Key / OpenAI-kompatibler Endpoint. |
| **Governance-Loop** | Plan → Freigabe/Queue → Ausführung → Audit/History. |
| **RAG-light** | Chunk-basiertes Retrieval ohne Pflicht-Vector-DB; Embeddings optional. |
| **Human-in-the-Loop** | Operator bestätigt oder staffelt riskante Schritte. |

---

## 9. Pflege und Ownership

- Bei **Tier-B-Features** im Produkt: dieses Dokument **und** [Carolus-Nexus-GTM-Messaging.md](Carolus-Nexus-GTM-Messaging.md) **und** [Carolus-Nexus-Benutzerhandbuch.md §1.1](Carolus-Nexus-Benutzerhandbuch.md) synchron halten.
- **Owner-Vorschlag:** Produkt/PM; technische Korrektheit mit Handbuch §1.1 abgleichen.
- Grenzfälle und Ist-Stand: Handbuch **§22**; Marketing vs. Produkt immer trennen.

---

*Ende USP-Strategie.*
