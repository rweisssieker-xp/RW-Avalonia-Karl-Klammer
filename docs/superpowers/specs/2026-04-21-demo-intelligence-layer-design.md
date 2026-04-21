# Demo Intelligence Layer Design

Date: 2026-04-21

## Decision

Add a read-only innovation layer for WinUI demo impact:

- Demo Mode style summary in Dashboard
- Next Action insight
- Context Replay summary from Watch sessions
- Flow Quality scoring for plans

The shared computation lives in a new core service, `OperatorInsightService`, so WinUI pages do not duplicate heuristics.

## Scope

Version 1 is local and read-only. It does not execute plans, change safety settings, or add trusted automation.

## Components

### OperatorInsightService

Collects:

- current settings
- foreground process/window title
- adapter family
- watch entry count and latest entry
- local knowledge readiness
- tool host readiness
- flow queue summary

Produces:

- demo readiness score
- USP sentence
- likely task
- safe next action
- risky action warning
- recommended flow hint
- context replay lines
- flow quality score for `RecipeStep` lists

### WinUI Dashboard

Uses the service for a stronger hero and a dedicated Next Action card.

### WinUI Live Context

Shows compact context replay from recent Watch entries.

### WinUI Ask

Extends the existing plan-risk InfoBar with a flow quality score.

## Non-Goals

- No WebUI implementation in this step.
- No Flow Builder changes.
- No AX write automation.
- No external network surface.

## Validation

Build WinUI with:

```powershell
dotnet build CarolusNexus.WinUI\CarolusNexus.WinUI.csproj -r win-x64
```

