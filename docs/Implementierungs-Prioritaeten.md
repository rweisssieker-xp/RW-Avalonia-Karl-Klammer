# Implementierungs-Prioritäten (technische Einordnung)

Dieses Dokument ergänzt [Carolus-Nexus-Benutzerhandbuch.md](Carolus-Nexus-Benutzerhandbuch.md) §1.1, §22 und §23.0 mit einer **empfohlenen Reihenfolge** für Engineering (kein Ersatz für Produkt-/Stakeholder-Priorisierung).

## Kriterien

1. **Nutzen × Erreichbarkeit:** Größter Lift bei nachweisbarem Operator-Mehrwert.
2. **Risiko:** Hochriskante Änderungen (Fat-Client, Installer) nach klaren Guardrails.
3. **Abhängigkeiten:** Token-Runtime vor domänenspezifischer Tiefe (AX).

## Empfohlene Reihenfolge

### 1. Operator-Adapter-Runtime (generische Plan-Tokens)

**Problem:** Viele Token-Kategorien werden geparst und angezeigt, die Ausführung bleibt Simulation/`[SKIP]` ([§23.0](Carolus-Nexus-Benutzerhandbuch.md)).  
**Ziel:** Schrittweise echte Ausführung für ausgewählte `app|…` / `browser.*` / Office-Pfade über [Win32AutomationExecutor](CarolusNexus/Services/Win32AutomationExecutor.cs) und [PlanGuard](CarolusNexus/Services/PlanGuard.cs), mit Tests und Safety-Profilen.  
**Warum zuerst:** Verbessert alle Flows (Ask, Rituale, Live Context), ohne AX-spezifische Umgebung.

### 2. Live Context / „Desktop Inspector“-Tiefe (inkl. AX-Heuristik ausbauen)

**Problem:** §15: Keine vollständige UIA-Grid-/Form-Extraktion; AX nur Heuristik.  
**Ziel:** Kontextpakete (Form-/Dialog-Summary, ausgewählte Grid-Zeilen) dort, wo UIA tragfähig ist; weiterhin Vision + Pläne als Fallback.  
**Warum nach (1):** Liefert bessere Eingaben fürs Modell; harte Klicks profitieren von (1).

### 3. AX 2012 / Fat-Client (schmal starten)

**Problem:** §14 beschreibt `ax.*` und einen Snapshot-Dienst; im Code keine `AxClientAutomationService`-Klasse; Ausführung nicht produktionsreif ([Handbuch-Tab-Abgleich](Carolus-Nexus-Handbuch-Tab-Abgleich.md) §14).  
**Ziel:** Expliziter Dienst oder klar benannte Schicht, die **lesend** Kontext bündelt und **schreibend** wenige `ax.*`-Operationen ausführt — hinter Feature-Flags und nur mit Testmandanten.  
**Warum später:** Hoher Integrations- und Wartungsaufwand; von (1) und (2) profitabel abgekoppelt planbar.

### 4. Distribution und Betrieb

**Problem:** §22: Kein Installer; RAG-light ohne Enterprise-Betrieb.  
**Ziel:** MSI/MSIX oder geprüfte Zip-Installation; optional später Vektor-Backend — getrennt von Kern-App-Features.  
**Warum nach Feature-Reife:** Installer lohnt sich, wenn die ausführbare Tiefe (1–3) den Produktversprechen näherkommt.

### 5. WinUI-Shell-Parität in Avalonia (optional, niedrig)

**Problem:** WinUI hat **Experiments (Tier C)** und **Strg+P Command Palette**; Avalonia nicht ([Handbuch-Tab-Abgleich](Carolus-Nexus-Handbuch-Tab-Abgleich.md) GUI-Abschnitt im Plan).  
**Ziel:** Optional Tab „Experiments“ und/oder Tastenkürzel zur Seitenwahl — rein UX-Komfort, kein Blocker für Produktnutzung.

---

## Nicht in dieser Liste

- **Marketing/USP:** [Carolus-Nexus-USP-Strategie.md](Carolus-Nexus-USP-Strategie.md) (Proof vs. Build vs. Moonshot).
- **Stakeholder-Sign-off:** Finale Priorität kann Produkt/AX-Kunden abweichen; dieses Dokument ist technische Orientierung.
