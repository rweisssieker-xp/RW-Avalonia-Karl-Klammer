# AX / Fat-Client: Pilot-Narrativ und messbare Meilensteine

**Zweck:** Externe und interne Kommunikation **ohne** Überversprechen (Handbuch §1.1, §22, §23.0). Alles unterhalb **vollständiger** AX-UIAutomation gilt als **Pilot** bzw. **Build**.

---

## Narrativ (Pitch, 3 Sätze)

1. Carolus Nexus liefert **Vision**, **UIA-Snapshot im Ask** und **Live Context** mit AX-Heuristik — damit sind Fat-Client-Szenarien **promptbar** und **planbar**, auch wenn einzelne Schritte noch simuliert werden.
2. **Governance** (Freigabe, Queue, Audit) bleibt gleich — der Pilot reduziert Risiko, während die Ausführungstiefe wächst.
3. **Kein** Versprechen „AX komplett hands-free“ ohne Mandanten-Demo und definierte Milestones.

---

## Meilensteine (objektiv prüfbar)

| # | Meilenstein | Akzeptanzkriterium | Tag |
|---|-------------|-------------------|-----|
| M0 | Kontext | Live Context / AX-Untertab zeigt konsistent Fenster-Titel/Prozess/Heuristik auf Referenz-AX-Client | Proof |
| M1 | Lesen | UIA-Snapshot oder Form-Summary liefert **mindestens N** sinnvolle Felder/Labels auf **einer** Referenz-Form (N vom Pilot festgelegt, z. B. ≥5) | Build |
| M2 | Plan | Modell erzeugt `ax.*`-Plan; Dry-Run zeigt alle Schritte; keine versteckten „execute“ ohne Gate | Proof / Build |
| M3 | Ausführung schmal | **Eine** schreibende `ax.*`-Operation End-to-End auf **Testmandant** mit power-user + PlanGuard | Build |
| M4 | Audit | Jeder Pilot-Lauf schreibt Step-Audit-Zeilen; Abbruch ist sauber | Proof |
| M5 | Skalierung | Zweite Form / zweiter Dialog — Wiederholbarkeit dokumentiert | Build |

*Tag **Proof** nur, wenn im Branch `CarolusNexus/` für Kunden sichtbar nachweisbar.*

---

## Kommunikations-Do’s / Don’ts

- **Do:** „Wir pilotieren AX mit definierten Meilensteinen M0–M3.“
- **Don’t:** „AX ist fertig integriert“ ohne M3 auf Kundensystem.
- **Do:** Auf §23.0-Tabelle verweisen (Simulation vs. Runtime).

---

*Verknüpfung: [Carolus-Nexus-ICP-Personas.md](Carolus-Nexus-ICP-Personas.md) ICP-A; Technik: [Implementierungs-Prioritaeten.md](Implementierungs-Prioritaeten.md).*
