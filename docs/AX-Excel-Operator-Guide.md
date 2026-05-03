# AX + Excel Operator Guide

This guide describes the main unique workflow: validating Excel lists against Microsoft Dynamics AX 2012 R3 CU13.

## Goal

Many teams work with Excel lists and AX 2012 screens every day. Carolus Nexus should reduce manual checking effort without pretending that risky ERP mutations are safe by default.

The v1 workflow is:

1. Load `.xlsx` or `.csv`.
2. Detect columns and likely key field.
3. Pick an AX preset.
4. Validate rows against AX read paths.
5. Mark row status.
6. Export evidence.

No AX write operation is executed by this workflow.

## Supported Inputs

- `.xlsx`
- `.csv`

The first row is treated as the header. The app profiles columns and suggests a likely key column based on names such as `AccountNum`, `Konto`, `Debitor`, `Kreditor`, `Vendor`, `Customer`, `Item`, or `Id`.

## Presets

- `Debitor/Kunde`: default AX entity `Customers`, key `AccountNum`.
- `Kreditor/Lieferant`: default AX entity `Vendors`, key `AccountNum`.
- `Artikel`: default AX entity `ReleasedProducts`, key `ItemId`.
- `Sachkonto/Kostenstelle`: default AX entity `MainAccounts`, key `MainAccountId`.
- `Custom OData`: manual entity and key field.

The entity names are defaults and can be changed in the page before running validation.

## AX Read Strategy

The service uses a hybrid read strategy:

- If `AxODataBaseUrl` is configured, it performs read-only OData lookup.
- If OData is missing, it captures foreground AX/UIA context and marks rows for manual review.
- If AX integration is disabled, rows are marked `AxUnavailable`.

## Result Statuses

- `OK`: AX read returned a matching record.
- `NotFound`: AX read returned no matching key in the response snippet.
- `DuplicateInExcel`: key appears more than once in the input file.
- `MissingKey`: key cell is empty.
- `NeedsManualReview`: no deterministic AX lookup was possible, but context/evidence may exist.
- `AxUnavailable`: AX integration or read backend is unavailable.

## Evidence Output

Each run writes files to:

```text
windows/data/excel-ax-checks/
```

Files:

- `<run-id>.csv`: row-level result export.
- `<run-id>.json`: structured run metadata.

Do not commit exports containing customer or production data.

## Recommended Pilot

Use one repeated real-world list first:

- One Excel list type.
- One AX company/DataArea.
- One key field.
- One read-only validation target.
- No write actions.

Measure:

- manual time before/after
- number of rows
- duplicate/missing count
- AX-unavailable count
- manual-review count
- exported evidence completeness

## Future Extensions

- AX Form Memory for known masks and fields.
- Watch-to-AX-Flow from observed user work.
- Reconciliation Copilot for explaining mismatch causes.
- Semantic AX tokens such as `ax.customer.exists`, `ax.vendor.validate`, `ax.item.status`.
- Gated write pilot on a test tenant only after read-only validation is stable.
