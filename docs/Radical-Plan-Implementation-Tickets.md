# Radical-Plan Umsetzungs-Tickets

Quelle: [docs/radical-plan-20260422-074753.md](/C:/tmp/RW-Avalonia-Karl-Klammer/docs/radical-plan-20260422-074753.md)

## Zielbild
Ausbau von **Agentic Auto-Captain** als radikale UX-Neukonzeption:
- 1-Ziel-Eingabe
- KI-generierter Ausführungsplan
- autonome Abarbeitung
- nur bei Risiko menschliche Kontrolle
- kurzer Endbericht statt Schritt-für-Schritt-Taktik

---

## Ticket 1 — Minimaler Intent-Engine-Flow
- **Titel:** `feat(radical): implement minimal goal intake + plan orchestration state`
- **Beschreibung:** `radical`-Prompt wird als klarer Intent (`goal`) verarbeitet und in einen internen Plan-State überführt.
- **Aufgaben:**
  1. Neues Modell `RadicalGoal` (Goal, Timestamp, GoalId, Status).
  2. Neuer Service `GoalOrchestrator` mit Methode:
     - `Task<RadicalPlan> GeneratePlanAsync(string goal, NexusSettings settings, CancellationToken ct)`
  3. Einfaches JSON-/YAML-Schema für den Plan mit Feldern:
     - `PlanId`, `Goal`, `Hypothesis`, `Steps[]` (`step`, `action`, `target`, `required_inputs`, `expected_output`)
  4. Speichern jedes generierten Plans in `docs/radical-plans/<goal-id>.json` für Audit/Replay.
  5. UI-Hook auf Ask-Flow: bei `radical` nicht direkt ausführen, sondern Plan anzeigen + „Captain Start“.
- **Akzeptanzkriterien:**
  - Bei Eingabe `radical <ziel>` wird mindestens ein Planobjekt erzeugt und angezeigt.
  - Plandatei liegt unter `docs/radical-plans`.
  - Bestehender `ask`-Flow bleibt funktional.
- **Definition of Done:** `Build` grün + manuelles Ausführen in UI (1 Goal -> Plan wird erstellt).

---

## Ticket 2 — Auto-Captain Executor + Guard-Tiers
- **Titel:** `feat(radical): implement autonomous plan executor with simulation gate`
- **Beschreibung:** Auto-Executor führt die meisten Schritte automatisiert aus und nutzt vorhandene Simulation als Sicherheits-Layer.
- **Aufgaben:**
  1. Neuer Service `AutoExecutor` mit Methoden:
     - `Task<ExecutionReport> DryRunAsync(RadicalPlan plan, CancellationToken ct)`
     - `Task<ExecutionReport> ExecuteAsync(RadicalPlan plan, CancellationToken ct)`
  2. Mapping `PlanStep` auf bestehende `SimplePlanSimulator`-Steps (ohne neue Tool-Implementierung).
  3. Guard-Kategorie:
     - `safe` → sofortiger DryRun + optionaler Auto-Start
     - `high-risk` → 1-Klick-Human-Approve
  4. Ergebnisprotokoll in `docs/radical-runs/<run-id>.json` schreiben.
  5. Ausführungsknopf im Ask-Panel auf neuen Auto-Captain-Flow umleiten.
- **Akzeptanzkriterien:**
  - Ausführung kann im DryRun vollständig ohne manuelle Interaktion starten.
  - High-Risk-Plan bleibt ohne Bestätigung blockiert.
  - Erfolgreiche und fehlgeschlagene Schritte werden im Report ausgewiesen.
- **Definition of Done:** End-to-End-Fluss `radical` -> Plan -> Run -> Report sichtbar.

---

## Ticket 3 — Radical Digest UI (Zero-UI-Default)
- **Titel:** `feat(radical): add radical digest panel + reduce UI friction`
- **Beschreibung:** Ersetze klassische Setup-/Review-Schritte durch kompakte Chat-+Digest-Ansicht.
- **Aufgaben:**
  1. Neue View/Komponente `RadicalDigestPanel` (minimal):  
     `Status`, `Nächster Schritt`, `Risiko`, `Ausführen`, `Abbrechen`, `Finale Zusammenfassung`.
  2. Bei non-risk Plans nur 1 CTA anzeigen:
     - `Captain ausführen`
  3. Nach Abschluss: 3-zeiliger, verständlicher Abschlusstext + Verweis auf Artefakte.
  4. Bestehende Plan/Run-Buttons beibehalten, aber in „advanced“ Sektion verstecken.
- **Akzeptanzkriterien:**
  - Bei Standardflow werden weniger als 3 UI-Schritte benötigt.
  - Erfolgsfälle zeigen einen kurzen Digest statt langer Log-Ausgabe.
  - Nutzer kann jederzeit abbrechen.
- **Definition of Done:** UI zeigt neuen Digest in `Ask`-Flow und reduziert sichtbare Klicks messbar.

---

## Ticket 4 — CLI/Script-Integration „radical“
- **Titel:** `feat(radical): wire persistent radical prompt into runbook`
- **Beschreibung:** `radical` und `radical run` sind dauerhaft reproduzierbar über CLI/Script/Pipeline nutzbar.
- **Aufgaben:**
  1. `scripts/Run-RadicalPlan.ps1` erweitern um optionale Parameter:
     - `-Mode plan|execute`
     - `-OutputDir docs/radical-plans`
  2. Bei `radical run` direkt `ExecuteAsync` triggern (falls Safety-Profil es erlaubt).
  3. Rückgabe: Pfade zu Plan/Run-Report + kurzer Laufstatus.
- **Akzeptanzkriterien:**
  - `scripts/Run-RadicalPlan.ps1 -Prompt "radical ..."` generiert deterministisch Plan.
  - `-Mode execute` versucht Ausführung nach Guard-Regeln.
- **Definition of Done:** Dokumentation im Plan-File ergänzt: CLI-Hinweise + Beispielaufrufe.

---

## Nächster Schritt
Ich kann jetzt sofort Ticket 1 direkt im Code aufsetzen (1-2 Dateien + DI + minimaler UI-Hook), oder zuerst Ticket 1-4 als Prioritätenliste im Repo als Milestone-Datei schreiben.  
Entscheidung: `1`=Ticket 1 sofort, `2`=nur Milestone fertig (ohne Code). 
