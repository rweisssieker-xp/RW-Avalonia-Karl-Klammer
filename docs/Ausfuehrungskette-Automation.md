# Ausführungskette Automation (Ist-Architektur)

Kurzdokument für **Plan-Verifikation**: wo Ritual-/Plan-Schritte technisch landen.

## Ablauf

1. **`SimplePlanSimulator.RunAsync`** ([`CarolusNexus/Services/SimplePlanSimulator.cs`](../CarolusNexus/Services/SimplePlanSimulator.cs))  
   - Dry-Run vs. echte Ausführung: Windows, Safety-Profil `power-user`, Guards.  
   - Fortschritt: **`AgentRunStateStore`** (globale Statuszeile / [`ActivityStatusHub`](CarolusNexus/Services/ActivityStatusHub.cs)).

2. **`PlanGuard`** — erlaubte App-Familien im Vordergrund, Argument-Policy.

3. **`RecipeStepGuardEvaluator`** — optionale Schritt-Guards; bei Fehlschlag `[SKIP]` oder Abbruch je nach Einstellung.

4. **`AutomationToolRouter.Execute`** ([`CarolusNexus/Services/AutomationToolRouter.cs`](../CarolusNexus/Services/AutomationToolRouter.cs))  
   - Kanal `script` → [`ScriptHookRunner`](CarolusNexus/Services/ScriptHookRunner.cs)  
   - Kanal `api` → [`ApiHookRunner`](CarolusNexus/Services/ApiHookRunner.cs)  
   - sonst → [`Win32AutomationExecutor.ExecuteWithCvFallback`](CarolusNexus/Services/Win32AutomationExecutor.cs)

5. Optional UI-Thread-Marshal: **`NexusContext.RunWin32StepOnUiThreadAsync`**.

## Globale Aktivitätsanzeige

- **`ActivityStatusHub`** kombiniert: Companion-Zustand, Ask-Busy, CLI-Agent-Label ([`SetCliAgentRun`](CarolusNexus/Services/ActivityStatusHub.cs)), **Flow-Fortschritt** aus `AgentRunStateStore`.  
- **`NexusShell.SetGlobalStatusLine`** / WinUI-Äquivalent aktualisieren die sichtbare Zeile.

## Computer-Use / Tier-C

[`ComputerUseLoopService.RunThroughSimulatorAsync`](CarolusNexus/Services/ComputerUseLoopService.cs) delegiert an dieselbe Simulator-Kette; der Experiments-Tab setzt zusätzlich den **Companion** auf „Thinking“, damit die Oberfläche den Lauf spürbar macht.
