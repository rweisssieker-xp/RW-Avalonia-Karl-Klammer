# Hinweise für Entwickler und KI-Agenten

- **Benutzerhandbuch (Produkt/Zielbild + Abgleich mit Stub-UI):** [docs/Carolus-Nexus-Benutzerhandbuch.md](docs/Carolus-Nexus-Benutzerhandbuch.md)
- **Demo / Pilot / Release-Abgleich:** [docs/Demo-Script-15min-Reality-Bundle.md](docs/Demo-Script-15min-Reality-Bundle.md) · [docs/AX-Pilot-Narrativ-und-Meilensteine.md](docs/AX-Pilot-Narrativ-und-Meilensteine.md) · [docs/Release-Abgleich-Featurekatalog.md](docs/Release-Abgleich-Featurekatalog.md)
- **Handbuch §5–§14 vs. Avalonia-Tabs (Ist-Abgleich):** [docs/Carolus-Nexus-Handbuch-Tab-Abgleich.md](docs/Carolus-Nexus-Handbuch-Tab-Abgleich.md)
- **Prioritäten technische Lücken (Adapter/AX/Distribution/WinUI-Parität):** [docs/Implementierungs-Prioritaeten.md](docs/Implementierungs-Prioritaeten.md)
- **Persona-Referenz:** [SOUL.md](SOUL.md)
- **Solution:** [KarlKlammer.slnx](KarlKlammer.slnx) · Projekt `CarolusNexus/CarolusNexus.csproj` · Ziel `net10.0-windows`
- **Datenpfade:** [CarolusNexus/AppPaths.cs](CarolusNexus/AppPaths.cs) — erwartet Ordner `windows/` unter der Repository-Wurzel (für `DiscoverRepoRoot` beim Start aus `bin/`)

Build/Start aus dem Repo-Root: `Build-Avalonia.cmd` / `Start-Avalonia.cmd` oder `dotnet build` / `dotnet run` wie im Handbuch §3.
