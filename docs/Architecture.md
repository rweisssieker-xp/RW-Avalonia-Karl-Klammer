# Architecture

## Overview

Carolus Nexus is split into a native WinUI shell and shared service layer.

```text
CarolusNexus.WinUI
  Native UI, navigation, pages, DevWinUI controls

CarolusNexus.Core
  Shared .NET 10 project linking models, services, AppPaths, NexusShell

CarolusNexus
  Legacy app plus shared source files used by Core

CarolusNexus.Core.Tests
  xUnit tests for service behavior
```

## Main Runtime Concepts

- `NexusSettings`: provider, safety, AX, watch, local tool host, and UI behavior.
- `AppPaths`: repository and runtime data paths under `windows/data`.
- `ActionPlanExtractor`: extracts action tokens from model output.
- `AutomationTokenReadiness`: classifies tokens as real, guarded, or unsupported.
- `SimplePlanSimulator`: guarded plan execution, dry-run, evidence, recovery.
- `AxClientAutomationService`: AX token gateway for read context and UIA delegation.
- `ExcelAxValidationService`: read-only Excel-to-AX validation workflow.
- `BackendCoverageService`: product truth report across services, surfaces, and gating.

## UI Surfaces

The WinUI app is code-first in `CarolusNexus.WinUI`.

Key surfaces:

- Shell/navigation: `MainWindow.xaml.cs`
- General pages: `WinUiShellPages.cs`
- Ask page: `AskShellPage.WinUi.cs`
- Dashboard: `DashboardShellPage.WinUi.cs`
- Operator flows: `RitualsShellPage.WinUi.cs`
- Knowledge, History, Live Context: dedicated WinUI page files

## Safety

Execution is intentionally layered:

1. Model/plan generation.
2. Token extraction.
3. Readiness classification.
4. Safety profile and denylist checks.
5. PlanGuard.
6. Execution/evidence/audit.

Default behavior should prefer read-only and guarded modes.

## AX + Excel Data Flow

```text
Excel/CSV file
  -> ExcelAxValidationService.LoadPreview
  -> column/key profile
  -> user selects preset/entity/key
  -> ExcelAxValidationService.Validate
  -> Ax2012ODataClient or AX/UIA fallback
  -> row statuses
  -> CSV + JSON evidence export
```

## Testing

Use targeted service tests for business logic and WinUI build/smoke for UI wiring.

```powershell
dotnet test CarolusNexus.Core.Tests/CarolusNexus.Core.Tests.csproj -c Debug
dotnet build CarolusNexus.WinUI/CarolusNexus.WinUI.csproj -c Debug -p:WindowsAppSDKSelfContained=false
```
