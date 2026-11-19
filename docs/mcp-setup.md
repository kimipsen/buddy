# Connecting an AI app to Buddy (MCP)

Buddy has a read-only [Model Context Protocol](https://modelcontextprotocol.io) server at `/mcp`. A
guardian signs in with their own Keycloak account, and the AI app can then read family data in plain
language ("who has night duty on Friday?"). It cannot change anything in v1.
Design: [MCP server](backend/analysis/mcp-server.md).

## Turn it on

- Set `Features__Mcp=true` on the API. It is off by default; with it off `/mcp` is a 404.
- `Keycloak:Audience` must be non-empty. With the flag on and an empty audience the API refuses to start.
- Existing realm: follow the MCP steps in [deploy/README.md](../deploy/README.md) (and
  [deploy/README-azure.md](../deploy/README-azure.md)): the `buddy-mcp:read` scope, the `buddy-mcp`
  client and the dynamic client registration (DCR) policies. The dev realm already has them.
- DCR is a separate Keycloak decision. Enable it to let any client register itself; disable it and
  use only the pre-registered `buddy-mcp` client.

## Connect a client

1. Open Buddy once with the guardian account. `/mcp` rejects an account that has never signed in
   to the app (`user_not_provisioned`).
2. Add a custom connector with the URL `https://<your-api>/mcp` (Claude, VS Code, ChatGPT).
3. Sign in at Keycloak and approve the consent screen for `buddy-mcp:read`.

Only `buddy-guardian` accounts are accepted. A child account gets 403 `not_a_buddy_guardian`.

**Where it works.** v1 supports desktop and server-side clients. The API has one CORS policy
(`Cors:AllowedOrigins`, meant for the Buddy frontend) and no separate one for `/mcp`, so a client that
runs in a browser page on another origin cannot call `/mcp` directly. This is a decision, not an
oversight; an MCP CORS policy can be added if a browser-hosted client turns out to be needed.

**A client with a custom-scheme redirect (for example `cursor://...`).** Dynamic registration refuses a
redirect URI with no host. In Keycloak, open the `buddy-mcp` client, add the client's redirect URI
under **Valid redirect URIs**, save, and give the guardian the `buddy-mcp` client id to enter in the
connector's advanced settings.

## What the tools return

15 read tools, all marked read-only: `whoami`, `get_today`, `list_children`, `list_groups`,
`list_calendars`, `list_calendar_occurrences`, `list_meal_plan`, `list_meals`, `list_night_duty`,
`list_pickups`, `list_task_templates`, `list_house_rules`, `list_sleep_diary`, `list_work_days`,
`get_child_progress`. A tool of a feature that is switched off (`Features__<Name>=false`) is not
listed. Date ranges follow the REST limits (for example 92 days for nights). Titles are cut at 200
characters and notes at 500, with `truncated: true`. Everything is scoped to the caller's own
families, as in the app.

## Safety

- Tool results contain text that family members wrote. A hostile text could try to steer the AI app
  (prompt injection). Keep per-call approval on in the client, especially once write tools arrive.
- Revoke a grant in the Keycloak account console under Applications.
- Every call is logged (tool, user, outcome, duration; never argument values; event 15001). The
  sleep diary tool also writes the usual health read-audit log.
- MCP has its own rate limit (`RateLimiting:Mcp`), separate from the REST per-user limit.

## Troubleshooting

- `invalid_client`: the client was removed (see the cleanup job). Remove the connector and add it again.
- 401 with `resource_metadata`: the token is missing, expired or for another audience.
- 403 `insufficient_scope`: the token lacks `buddy-mcp:read`.

## Cleanup of abandoned clients

A daily job deletes dynamically registered clients that no user has consented to, 7 days after it
first saw them (`buddy.firstSeen`). It never touches `buddy-mcp`, `buddy-frontend`, `buddy-admin-cli`
or built-in clients, and fails closed when it cannot read every user's consents.

- `Mcp__ClientCleanup__DryRun=true` is the default: it stamps `buddy.firstSeen`, deletes nothing and
  reports `wouldDelete` in its summary line (event 15003). Roll out with a dry run first, then set it
  to `false`.
- `Mcp__ClientCleanup__MaxDeletesPerPass` (default 100) caps a pass; reaching it logs event 15005.
- `Mcp__ClientCleanup__Enabled=false` turns it off (event 15007 says so at startup).
- Log events (15000-15099): 15001 tool call, 15002 non-guardian rejected, 15003 cleanup summary,
  15004 pass failed, 15005 cap reached, 15006 consents unreadable, 15007 cleanup off at startup,
  15008 one client failed. Alert on 15004, 15005 and 15006; read 15003 after each pass.
- Recompute the DCR client cap when the number of families grows.

## Not in v1

Write tools, a "Connected AI apps" card, a DCR rate limit and Client ID Metadata Documents are in
[TODO.md](../TODO.md).
