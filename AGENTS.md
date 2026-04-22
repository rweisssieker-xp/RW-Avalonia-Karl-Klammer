# Hinweise für Entwickler und KI-Agenten

- **Benutzerhandbuch (Produkt/Zielbild + Abgleich mit Stub-UI):** [docs/Carolus-Nexus-Benutzerhandbuch.md](docs/Carolus-Nexus-Benutzerhandbuch.md)
- **Demo / Pilot / Release-Abgleich:** [docs/Demo-Script-15min-Reality-Bundle.md](docs/Demo-Script-15min-Reality-Bundle.md) · [docs/AX-Pilot-Narrativ-und-Meilensteine.md](docs/AX-Pilot-Narrativ-und-Meilensteine.md) · [docs/Release-Abgleich-Featurekatalog.md](docs/Release-Abgleich-Featurekatalog.md)
- **Handbuch §5–§14 vs. Avalonia-Tabs (Ist-Abgleich):** [docs/Carolus-Nexus-Handbuch-Tab-Abgleich.md](docs/Carolus-Nexus-Handbuch-Tab-Abgleich.md)
- **Prioritäten technische Lücken (Adapter/AX/Distribution/WinUI-Parität):** [docs/Implementierungs-Prioritaeten.md](docs/Implementierungs-Prioritaeten.md)
- **KI / RAG Umgebung (`.env`):** [docs/Ki-und-RAG-Umgebung.md](docs/Ki-und-RAG-Umgebung.md) · **Automation-Ausführungskette:** [docs/Ausfuehrungskette-Automation.md](docs/Ausfuehrungskette-Automation.md) · **Token-Arbeitspakete:** [docs/Token-Runtime-Arbeitspakete.md](docs/Token-Runtime-Arbeitspakete.md)
- **Persona-Referenz:** [SOUL.md](SOUL.md)
- **Solution:** [KarlKlammer.slnx](KarlKlammer.slnx) · Projekt `CarolusNexus/CarolusNexus.csproj` · Ziel `net10.0-windows`
- **Datenpfade:** [CarolusNexus/AppPaths.cs](CarolusNexus/AppPaths.cs) — erwartet Ordner `windows/` unter der Repository-Wurzel (für `DiscoverRepoRoot` beim Start aus `bin/`)

Build/Start aus dem Repo-Root: `Build-Avalonia.cmd` / `Start-Avalonia.cmd` oder `dotnet build` / `dotnet run` wie im Handbuch §3.

## Zusammenarbeit mit dem Agenten (Repo-weite Präferenz)

- **Direktmodus bevorzugen:** Bei klaren Aufträgen bitte möglichst ohne Rückfragen direkt bis zur Ausführung vorgehen (Analyse → Implementierung → Build/Start → Rückmeldung), sofern kein blockierendes Risiko besteht.
- **Kurz-Annahmen dokumentieren:** Wenn keine klare Information vorhanden ist, bitte konservative Annahme treffen und diese in der Ergebnis-Zusammenfassung nennen.
- **Nachfragen nur bei echten Blockern:** Nachfrage nur, wenn Eingaben fehlen, die die Aufgabe nicht eindeutig ausführen lassen (z. B. gewünschte Zielarchitektur oder fehlende Credentials/Feature-Schalter).
- **Komplett-Flow statt Teilaufgaben:** Bei Entwicklungsaufträgen standardmäßig in einem Schritt ausführen (`ändern` → `bauen` → `testen/starten`), statt viele separate Mini-Interaktionen.
- **Antwortstil:** Ergebnis als klarer Abschlussbericht mit offenen Restpunkten am Ende.

### Default-Arbeitsmodus

- **Feature-Mode (Standard):** Wenn ein Auftrag reale Funktionalität betrifft (z. B. Runtime, Plan-Ausführung, Adapter, Services, Distribution), aktiv zuerst direkt umsetzen mit Build-/Smoke-Run und nur bei echten externen Blockern nachfragen.
- **UI-Mode (nur Oberfläche):** Bei reinen Oberflächenanpassungen vorgehen bis zum visuellen Resultat + kurzer Start/Compile-Prüfung; bei nicht-kritischen Interaktions-Fragen kann stillschweigend Standardlösung gewählt werden.
- **Entscheidungskriterium:** Ich entscheide intern primär nach Dateibereich:
  - `Services`, `Models`, `Core`, `App/Setup`, `Ritual/Plan` → Feature-Mode
  - `Views`, `MainWindow`, `XAML`, reine Layout-/Theme-Dateien → UI-Mode
- **Wenn unsicher:** Feature-Mode bevorzugen, damit die Änderung nicht „nur visuell“ stehen bleibt.

### Maximaler Autonomie-Modus (wenige Anweisungen nötig)

- **Default-Mindestinput:** Aus einem Auftrag werden Ziel, betroffene Komponente und gewünschter Endzustand abgeleitet; bei Bedarf nutze ich vorhandene Projekt- und Doku-Hierarchie als Leitplanke, ohne Zusatzabstimmung.
- **Standard-Verhalten:**  
  1. Analyse der betroffenen Dateien  
  2. Implementierung nach kleinstmöglicher Änderung  
  3. `dotnet build` (entsprechendes Projekt)  
  4. `dotnet run` (entsprechendes Projekt)  
  5. Kurzer Ergebnisbericht mit Risiken
- **Automatische Standardbefehle:**
  - `dotnet build CarolusNexus/CarolusNexus.csproj -c Debug`
  - `dotnet run --project CarolusNexus/CarolusNexus.csproj -c Debug`
  - bei WinUI-Anpassungen zusätzlich: `dotnet run --project CarolusNexus.WinUI/CarolusNexus.WinUI.csproj -c Debug -p:WindowsAppSDKSelfContained=false`
- **Scope- und Schutzregeln:** Nicht verändern ohne Grund `obj/`, `bin/`, `artifacts/`, Logs, temporäre Dateien.
- **Build-Gates:** Wenn Build fehl schlägt, Fehlerursache beheben und weiterführen; Abbruch nur bei externen Blockern (fehlende Credentials, nicht verfügbare Umgebung).
- **Nachfragen nur bei echten Blockaden:**  
  - Sicherheits-/Produktionsrelevante Richtlinienthemen  
  - nicht im Repo verfügbare Zieleingaben (z. B. fehlende Service-Endpoints)
- **Antwortformat bei Abschluss:**  
  - `Änderungen` (kurz)  
  - `Validierung` (Build/Run + Ergebnis)  
  - `Nächster sinnvoller Schritt` (1 Punkt)

## Working Mode for Codex

- The user prefers short steering prompts and fewer repeated instructions.
- Treat prompts like `mach weiter`, `go on`, `weiter`, `mehr gui`, `mehr usps`, `mehr ai`, `teste`, or `start die app` as permission to continue directly.
- Do not stop at proposals when the request is actionable. Implement the next useful increment, then verify it.
- Make conservative implementation decisions that fit the existing WinUI/shared architecture.
- Ask questions only when the task is ambiguous in a way that could cause destructive work, external side effects, credential exposure, or wasted large effort.
- Keep progress updates concise and in German.
- Keep final summaries short: what changed, what was tested, and any blocker.

## Standard-Validierung (selbständig)

- Für jede umsetzbare Änderung führe wenn möglich direkt einen sinnvollen Test-Durchlauf durch.
- Bei UI/WinUI/Avalonia-relevanten Änderungen:  
  1. Build ausführen  
  2. App starten  
  3. Sichtprüfung per Screenshot  
  4. Auffälligkeiten direkt im selben Durchlauf korrigieren
- Bei reinen Backend-/Non-UI-Änderungen: direkte Build-/Unit-/Integration-Validierung und Ergebnislog.
- Standard: Änderungen erst als abgeschlossen markieren, wenn der Lauf erfolgreich war oder klar dokumentierte Blocker vorliegen.
