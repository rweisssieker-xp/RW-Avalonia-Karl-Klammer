# Carolus Nexus WinUI Handbook

This handbook describes the current active WinUI app. For older Avalonia-era details, see [Carolus-Nexus-Benutzerhandbuch.md](Carolus-Nexus-Benutzerhandbuch.md).

## 1. Purpose

Carolus Nexus is a native Windows operator cockpit for people who work across Excel, AX 2012, local documents, browsers, and desktop tools. The app is designed around read-first AI assistance, guarded execution, and evidence.

The primary unique product direction is:

> AX + Excel Operator Copilot for Windows-heavy enterprise work.

## 2. Build And Run

Build:

```powershell
dotnet build CarolusNexus.WinUI/CarolusNexus.WinUI.csproj -c Debug -p:WindowsAppSDKSelfContained=false
```

Run:

```powershell
dotnet run --project CarolusNexus.WinUI/CarolusNexus.WinUI.csproj -c Debug -p:WindowsAppSDKSelfContained=false
```

The active target is `net10.0-windows10.0.26100.0` with default `win-x64`.

## 3. Navigation

Main pages:

- `Ask`: AI question, local knowledge, plan detection, preflight, execution status.
- `Dashboard`: readiness, live context, next best action, watch/proactive state.
- `Setup`: provider, model, safety, local tool host, AX configuration.
- `Knowledge`: local document index and search.
- `Operator flows`: reusable guarded flows.
- `History`: action history, step audit, self-heal hints.
- `Diagnostics`: runtime reports, AI evaluation, proof packs, enterprise controls.
- `USP Studio`: ROI, governance, proof packs, drift, heatmap, mission score.
- `Backend Coverage`: capability matrix across UI, services, adapters, safety status.
- `Excel + AX Check`: read-only Excel list validation against AX.
- `UI Lab`: DevWinUI control validation area.
- `Console`: local CLI agent runner.
- `Live Context`: foreground window, adapter family, UIA/AX context.
- `Experiments (Tier C)`: isolated research/guarded computer-use surface.

## 4. Safety Model

The app separates planning, read-only checks, guarded execution, and unsupported actions.

Common statuses:

- `REAL`: executable with current configuration.
- `GUARDED`: known capability, blocked by safety/configuration.
- `UNSUPPORTED`: no executable adapter exists for this token.
- `SIM` / `DRY-RUN`: intentionally non-mutating.
- `BLOCKED`: denied by policy.

AX + Excel v1 is read-only. It does not write to AX.

## 5. AX Configuration

AX settings live in Setup and `windows/data/settings.json`.

Supported read paths:

- OData/AIF endpoint via `Ax2012ODataClient`.
- Business Connector logon probe via `Ax2012ComBusinessConnectorRuntime`.
- Foreground UIA/context fallback via `AxClientAutomationService`.

Secrets belong in `windows/.env`:

```text
AX_HTTP_USER=...
AX_HTTP_PASSWORD=...
```

Use Windows default credentials where possible.

## 6. Evidence And Runtime Files

Important data paths:

- `windows/data/action-history.json`
- `windows/data/ritual-step-audit.jsonl`
- `windows/data/execution-evidence.jsonl`
- `windows/data/adaptive-operator-memory.json`
- `windows/data/excel-ax-checks/`
- `windows/data/knowledge/`

The app should remain useful even if these files are missing; services create required folders on startup.

## 7. Backend Coverage

The Backend Coverage page is the truth surface for what exists, where it is exposed, and how it is gated.

Use it before making product claims. If a capability is marked `guarded`, `disabled`, or `real/report`, it should not be described as fully autonomous production execution.

## 8. Recommended Demo Flow

1. Open `Dashboard` and show readiness/live context.
2. Open `Excel + AX Check`.
3. Load a sample `.csv` or `.xlsx`.
4. Show detected columns and key suggestion.
5. Run read-only validation.
6. Export evidence.
7. Open `Backend Coverage` and show AX+Excel capability status.
8. Open `USP Studio` and show proof/ROI/evidence reports.

## 9. Known Boundaries

- AX write/post/book flows are not production-ready by default.
- Office/Outlook/Teams deep semantic automation is not the main v1 moat.
- Generic UIA can be fragile across custom desktop apps.
- Power-user execution must stay behind explicit safety controls.
