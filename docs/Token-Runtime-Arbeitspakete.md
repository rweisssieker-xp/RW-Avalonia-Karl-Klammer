# Token-Runtime — Mini-Arbeitspakete und Akzeptanz

Pro Token-Familie: **Ziel** und **Akzeptanz** „Happy Path unter Windows + Safety-Profil `power-user`“ ohne blindes `[SKIP]` aus „nicht implementiert“.

## 1. UIA / `uia.*`

- **Ziel:** Repräsentativer Schritt (z. B. Fokus, Invoke bekanntes Control) mit reproduzierbarem Setup.  
- **Akzeptanz:** Log zeigt kein `[SKIP]` nur wegen fehlendem Token-Mapping; Ergebnis ist Erfolg oder **erwarteter** Soft-Fail (z. B. Fenster nicht gefunden) mit klarer Meldung.  
- **Code:** [`UiAutomationActions`](CarolusNexus/Services/UiAutomationActions.cs), [`Win32AutomationExecutor`](CarolusNexus/Services/Win32AutomationExecutor.cs).

## 2. `app|…` / App-Starter

- **Ziel:** Mindestens ein Mapping über [`AppFamilyLauncher`](CarolusNexus/Services/AppFamilyLauncher.cs) end-to-end (EXE/Alias dokumentiert).  
- **Akzeptanz:** Start oder erwarteter Fehler (Pfad fehlt) ohne generischen Skip.

## 3. Skripte (`script`-Kanal)

- **Ziel:** Ein `powershell:`- oder `cmd:`-Schritt mit gültigem Safety-Profil.  
- **Akzeptanz:** Ausführung oder Block mit **verständlicher** Policy-Meldung — siehe [`ScriptHookRunner`](CarolusNexus/Services/ScriptHookRunner.cs).

## 4. API-Hooks (`api`-Kanal)

- **Ziel:** Konfigurierter Hook, der in der Testumgebung antwortet oder sauber mit „nicht konfiguriert“ abbricht.  
- **Akzeptanz:** Kein stiller Erfolg; siehe [`ApiHookRunner`](CarolusNexus/Services/ApiHookRunner.cs).

## 5. `ax.*` / AX 2012

- **Ziel:** Lesender Pfad mit Testmandant + Flags; Schreiben nur nach expliziter Freigabe.  
- **Akzeptanz:** Kein produktiver Schreib-Call ohne Konfiguration; Meldung statt Teil-Hänger — [`Ax2012ODataClient`](CarolusNexus/Services/Ax2012ODataClient.cs), COM-Runtime.

## Querverweise

- Ausführungskette: [Ausfuehrungskette-Automation.md](Ausfuehrungskette-Automation.md)  
- Prioritäten: [Implementierungs-Prioritaeten.md](Implementierungs-Prioritaeten.md)
