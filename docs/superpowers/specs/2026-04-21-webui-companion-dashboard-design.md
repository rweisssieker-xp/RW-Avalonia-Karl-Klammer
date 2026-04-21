# WebUI Companion Dashboard Design

Date: 2026-04-21

## Decision

Build the WebUI Companion Dashboard inside the existing `LocalToolHost`.

The first version is read-only and demo-focused. It shows real local state from Carolus Nexus in a browser while keeping WinUI/Avalonia as the main operator app.

## Goal

Create a visible demo surface that explains the product quickly:

- Carolus Nexus watches local Windows work context.
- It combines AI provider readiness, local knowledge, GUI context, watch history, flow jobs, and safety posture.
- It can later grow into a WebUI companion without becoming a second automation surface too early.

## Non-Goals

- No browser-triggered plan execution.
- No WebUI flow builder in version 1.
- No external network binding.
- No new database or storage format.
- No replacement of the WinUI/Avalonia desktop shell.

## Routes

`LocalToolHost` adds these GET routes:

- `/dashboard` returns the HTML shell.
- `/dashboard/app.css` returns the dashboard styles.
- `/dashboard/app.js` returns client-side polling and rendering.
- `/v1/dashboard/state` returns current status JSON.
- `/v1/dashboard/watch` returns recent watch entries JSON.
- `/watch-thumbnails/{file}` returns existing watch thumbnail JPG files from `AppPaths.WatchThumbnailsDir`.

Existing routes stay compatible:

- `/health`
- `/v1/invoke`

`/v1/invoke` keeps its current behavior and is not used by the dashboard for version 1.

## Security

The host remains bound to `127.0.0.1`.

The dashboard is read-only. It exposes local status and thumbnails only to the local machine.

If a bearer token is configured, write-capable routes such as `/v1/invoke` continue to require it. Dashboard state routes may remain local-only without a token in version 1 because they are GET-only and bound to loopback. If a later deployment needs stricter isolation, the same token check can be applied to dashboard JSON routes.

Thumbnail serving must sanitize file names:

- accept only a simple file name, not a path
- serve only `.jpg`
- resolve under `AppPaths.WatchThumbnailsDir`
- return 404 for missing or invalid files

## UI Structure

The dashboard uses a hybrid layout: showcase first, operator cockpit below.

### Showcase Area

First viewport, large and readable:

- product name: Carolus Nexus
- active foreground app and window title
- AI / GUI / WebUI readiness score
- one USP sentence
- watch mode state
- local tool host state

This area is meant for live demos and screenshots.

### Operator Cockpit

Dense status area:

- Provider and model
- `.env` / provider key readiness
- Knowledge file/index/chunk/embedding status
- Safety profile and automation posture
- Flow job summary
- Recent app log excerpt

This area proves the demo is backed by local runtime state.

### Watch Timeline

Recent watch entries:

- local time
- process name
- window title
- adapter family
- screen hash prefix
- source
- optional thumbnail

The timeline should work with no entries and with missing thumbnails.

## Data Shape

`/v1/dashboard/state` returns:

```json
{
  "ok": true,
  "app": "Carolus Nexus",
  "version": "string",
  "provider": "string",
  "model": "string",
  "mode": "string",
  "providerKeyReady": true,
  "knowledge": {
    "fileCount": 0,
    "indexReady": true,
    "chunksReady": true,
    "embeddingsReady": false
  },
  "foreground": {
    "process": "string",
    "title": "string",
    "adapterFamily": "string"
  },
  "safety": {
    "profile": "string",
    "panicStopEnabled": true,
    "neverAutoSend": true,
    "automationPosture": "simulation | guarded | power-user"
  },
  "flows": {
    "pendingJobs": 0,
    "summary": "string"
  },
  "readiness": {
    "score": 0,
    "max": 6,
    "usp": "string"
  },
  "recentLog": "string"
}
```

`/v1/dashboard/watch` returns:

```json
{
  "ok": true,
  "version": 2,
  "entries": [
    {
      "atLocal": "string",
      "process": "string",
      "windowTitle": "string",
      "adapterFamily": "string",
      "screenHash": "string",
      "source": "string",
      "thumbnailUrl": "string"
    }
  ]
}
```

## Implementation Units

### LocalToolHost route handling

Add explicit GET handling for dashboard static assets and JSON endpoints.

Keep the current POST `/v1/invoke` path unchanged.

### Dashboard state builder

Add a small helper that collects:

- settings from `NexusContext.GetSettings` or `SettingsStore`
- knowledge file/index state from `AppPaths`
- foreground process/title from `ForegroundWindowInfo`
- adapter family from `OperatorAdapterRegistry`
- flow job summary from `RitualJobQueueStore`
- log excerpt from `NexusShell`
- readiness score using the same criteria as the WinUI dashboard radar

### Watch response builder

Read `WatchSessionService.LoadOrEmpty()`, take the most recent entries, and map thumbnail paths to `/watch-thumbnails/{file}`.

### Static assets

Store dashboard assets in a local project folder, for example:

- `CarolusNexus/WebUi/dashboard.html`
- `CarolusNexus/WebUi/dashboard.css`
- `CarolusNexus/WebUi/dashboard.js`

Embed or copy them as content depending on the existing project conventions. The simplest version can read them from the repo/runtime path and fall back to embedded strings if needed.

## Error Handling

- Unknown dashboard route: 404.
- Invalid thumbnail path: 404.
- Foreground read failure: return empty foreground fields with `ok: true`.
- Watch file read failure: return `ok: true`, empty entries, and a short `message`.
- JSON serialization failure: return 500 with a short plain-text error.

## Testing

Build:

```powershell
dotnet build CarolusNexus.WinUI\CarolusNexus.WinUI.csproj -r win-x64
```

Manual checks:

- Start the app with local tool host enabled.
- Open `/health`.
- Open `/dashboard`.
- Confirm cards render with no watch entries.
- Switch to watch mode, wait for entries, confirm timeline updates.
- Confirm thumbnails render when present.
- Confirm `/v1/invoke` behavior is unchanged.

## Rollout

Version 1 is a demo dashboard only.

Later versions can add:

- WebUI flow builder
- explicit token gate for dashboard JSON
- exportable demo report
- WebSocket or server-sent events instead of polling
- promoted watch-session summaries

