# Carolus Nexus

Carolus Nexus is a Windows operator desktop for AI-assisted enterprise work. It combines local knowledge, live desktop context, guarded automation, audit evidence, and AX 2012 / Excel workflows in one native WinUI app.

The current primary app is `CarolusNexus.WinUI` on .NET 10 / WinUI / win-x64. The older Avalonia project is kept as historical reference and is not the active release target.

## Highlights

- Native WinUI shell with dashboard, Ask, Knowledge, Operator Flows, Diagnostics, USP Studio, Backend Coverage, and Excel + AX Check.
- Local RAG over files in `windows/data/knowledge`.
- Guarded action plans with preflight status: `REAL`, `GUARDED`, `UNSUPPORTED`.
- Evidence, recovery suggestions, adaptive operator memory, mission timeline, drift report, confidence heatmap, and proof packs.
- AX 2012 R3 CU13 support path: OData/AIF/BusinessConnector probes plus foreground UIA context.
- Read-only Excel + AX validation workbench for checking `.xlsx` / `.csv` lists against AX without writing to AX.

## Quick Start

Prerequisites:

- Windows 10/11
- .NET 10 SDK
- Optional provider keys in `windows/.env`
- Optional AX 2012 test endpoint/client configuration

Build:

```powershell
dotnet build CarolusNexus.WinUI/CarolusNexus.WinUI.csproj -c Debug -p:WindowsAppSDKSelfContained=false
```

Run:

```powershell
dotnet run --project CarolusNexus.WinUI/CarolusNexus.WinUI.csproj -c Debug -p:WindowsAppSDKSelfContained=false
```

Convenience scripts:

```cmd
Build-WinUI.cmd
Start-WinUI.cmd
Smoke-WinUI.cmd
```

## Documentation

- [WinUI Handbook](docs/WinUI-Handbook.md)
- [AX + Excel Operator Guide](docs/AX-Excel-Operator-Guide.md)
- [Architecture](docs/Architecture.md)
- [Release Checklist](docs/Release-Checklist.md)
- [Existing detailed product handbook](docs/Carolus-Nexus-Benutzerhandbuch.md)
- [USP strategy](docs/Carolus-Nexus-USP-Strategie.md)

## Important Product Truth

Carolus Nexus is strongest as an AX + Excel operator copilot for Windows-heavy enterprise teams.

The current AX + Excel workflow is intentionally read-only: it validates Excel lists against AX data/context and exports evidence. AX write/post/book actions remain gated and must not be marketed as production-ready until validated on a real test tenant with explicit approvals.

## Repository Layout

```text
CarolusNexus.WinUI/       Active WinUI app
CarolusNexus.Core/        Shared .NET 10 core/services project
CarolusNexus/             Legacy app + shared source files linked into Core
CarolusNexus.Core.Tests/  xUnit tests
docs/                     Product, technical, and release documentation
windows/                  Local app data, .env, settings, knowledge, runtime artifacts
```

## Data And Secrets

- Runtime data lives under `windows/data`.
- Knowledge files live under `windows/data/knowledge`.
- Secrets belong in `windows/.env`.
- Do not commit real credentials, AX passwords, customer data, or exported production evidence.

## Validation Commands

```powershell
dotnet test CarolusNexus.Core.Tests/CarolusNexus.Core.Tests.csproj -c Debug
dotnet build CarolusNexus.WinUI/CarolusNexus.WinUI.csproj -c Debug -p:WindowsAppSDKSelfContained=false
```

## Status

Current known-good state:

- .NET 10
- WinUI `net10.0-windows10.0.26100.0`
- `win-x64`
- Build: `0 warnings`, `0 errors`
- Active product direction: AX + Excel Operator Copilot
