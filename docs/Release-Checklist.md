# Release Checklist

## Build

- [ ] `dotnet test CarolusNexus.Core.Tests/CarolusNexus.Core.Tests.csproj -c Debug`
- [ ] `dotnet build CarolusNexus.WinUI/CarolusNexus.WinUI.csproj -c Debug -p:WindowsAppSDKSelfContained=false`
- [ ] Start WinUI app.
- [ ] Full navigation smoke: no `Could not open page`.

## Product Truth

- [ ] Backend Coverage reviewed.
- [ ] Claims only use `real` or demonstrated `read-only` capabilities.
- [ ] AX write/post/book is not marketed as production-ready.
- [ ] `Excel + AX Check` described as read-only unless a test-tenant write pilot exists.

## Docs

- [ ] README matches current active app.
- [ ] WinUI Handbook updated.
- [ ] AX + Excel Operator Guide updated.
- [ ] Known limitations are explicit.
- [ ] No credentials or customer data in docs.

## Runtime Data

- [ ] `windows/.env` is local only.
- [ ] `windows/data/excel-ax-checks/` exports reviewed before sharing.
- [ ] `windows/data/knowledge/` contains only approved sample or customer-safe docs.
- [ ] Logs do not include secrets.

## Demo

- [ ] Sample `.csv` or `.xlsx` available.
- [ ] AX settings checked.
- [ ] Fallback behavior understood if AX endpoint is unavailable.
- [ ] Evidence export path verified.
