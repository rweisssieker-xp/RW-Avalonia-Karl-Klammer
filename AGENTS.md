# Hinweise für Entwickler und KI-Agenten

- **Benutzerhandbuch (Produkt/Zielbild + Abgleich mit Stub-UI):** [docs/Carolus-Nexus-Benutzerhandbuch.md](docs/Carolus-Nexus-Benutzerhandbuch.md)
- **Persona-Referenz:** [SOUL.md](SOUL.md)
- **Solution:** [KarlKlammer.slnx](KarlKlammer.slnx) · Projekt `CarolusNexus/CarolusNexus.csproj` · Ziel `net10.0-windows`
- **Datenpfade:** [CarolusNexus/AppPaths.cs](CarolusNexus/AppPaths.cs) — erwartet Ordner `windows/` unter der Repository-Wurzel (für `DiscoverRepoRoot` beim Start aus `bin/`)

Build/Start aus dem Repo-Root: `Build-Avalonia.cmd` / `Start-Avalonia.cmd` oder `dotnet build` / `dotnet run` wie im Handbuch §3.
