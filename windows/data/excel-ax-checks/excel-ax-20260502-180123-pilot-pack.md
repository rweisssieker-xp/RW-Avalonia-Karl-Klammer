# Carolus Nexus AX/Excel Pilot Pack

- Run: excel-ax-20260502-180123
- Created UTC: 2026-05-02T18:01:23.2475039+00:00
- Input: C:\Users\reinerw\AppData\Local\Temp\carolus-excel-ax-tests\customers.csv
- Preset: Debitor/Kunde
- Key mapping: AccountNum -> Customers.AccountNum
- CSV evidence: C:\tmp\RW-Avalonia-Karl-Klammer\windows\data\excel-ax-checks\excel-ax-20260502-180123.csv
- JSON evidence: C:\tmp\RW-Avalonia-Karl-Klammer\windows\data\excel-ax-checks\excel-ax-20260502-180123.json

## Management Summary

AX readiness is 55/100 (C). Pilot usable with manual review gates.
Rows: 4; Mode: AX disabled; AxUnavailable: 2; DuplicateInExcel: 1; MissingKey: 1

## Readiness

AX Readiness Score
Score: 55/100 (C)
Verdict: Pilot usable with manual review gates.

Blockers
- 2x Enable AX integration, configure OData, or open AX foreground context for UIA evidence.
- 1x Fill 'AccountNum' before reconciliation.
- 1x Decide which Excel row is authoritative and remove or merge duplicates.

Strengths
- 0/4 rows are already classified as ready.
- Every row has an auditable local explanation and next action.
- CSV and JSON evidence are generated without AX write operations.

## Exception Inbox

AI Exception Inbox
- [blocked] AxConnectivity: 2 items | Owner: AX/IT owner | Action: Enable AX integration, configure OData, or open AX foreground context for UIA evidence. | Keys: 1000, 2000
- [blocked] ExcelDataQuality: 1 items | Owner: Excel owner | Action: Fill 'AccountNum' before reconciliation. | Keys: row 4
- [warning] Duplicate: 1 items | Owner: Business data owner | Action: Decide which Excel row is authoritative and remove or merge duplicates. | Keys: 1000

## Reconciliation Copilot

AI Reconciliation Copilot (local deterministic v1)
AX Readiness Score
Score: 55/100 (C)
Verdict: Pilot usable with manual review gates.

Blockers
- 2x Enable AX integration, configure OData, or open AX foreground context for UIA evidence.
- 1x Fill 'AccountNum' before reconciliation.
- 1x Decide which Excel row is authoritative and remove or merge duplicates.

Strengths
- 0/4 rows are already classified as ready.
- Every row has an auditable local explanation and next action.
- CSV and JSON evidence are generated without AX write operations.

AI Exception Inbox
- [blocked] AxConnectivity: 2 items | Owner: AX/IT owner | Action: Enable AX integration, configure OData, or open AX foreground context for UIA evidence. | Keys: 1000, 2000
- [blocked] ExcelDataQuality: 1 items | Owner: Excel owner | Action: Fill 'AccountNum' before reconciliation. | Keys: row 4
- [warning] Duplicate: 1 items | Owner: Business data owner | Action: Decide which Excel row is authoritative and remove or merge duplicates. | Keys: 1000

- AxConnectivity/blocked: 2
- ExcelDataQuality/blocked: 1
- Duplicate/warning: 1

Top next actions
- 2x Enable AX integration, configure OData, or open AX foreground context for UIA evidence.
- 1x Decide which Excel row is authoritative and remove or merge duplicates.
- 1x Fill 'AccountNum' before reconciliation.

Examples
- Row 2, key '1000': AX could not be queried with the current setup. -> Enable AX integration, configure OData, or open AX foreground context for UIA evidence.
- Row 3, key '1000': The same key appears multiple times in the Excel list. -> Decide which Excel row is authoritative and remove or merge duplicates.
- Row 4, key '': The Excel row has no usable key, so AX cannot be queried safely. -> Fill 'AccountNum' before reconciliation.
- Row 5, key '2000': AX could not be queried with the current setup. -> Enable AX integration, configure OData, or open AX foreground context for UIA evidence.

## Audit Note

This pack is generated read-only. It does not write, post, book or mutate AX data.
