# Health checks and observability

How to tell whether the API is up, and how to see what it's doing.

## Health checks

| Endpoint | Kind | Checks | Answer |
|---|---|---|---|
| `GET /health` | Liveness | None | `200 Healthy` while the process serves requests |
| `GET /health/ready` | Readiness | Postgres, Keycloak | See below |

`/health/ready` answers with each check's status and nothing else, because it is anonymous:

```json
{ "status": "Degraded", "checks": { "postgres": "Healthy", "keycloak": "Degraded" } }
```

- **postgres**: `SELECT 1` through the shared `NpgsqlDataSource`, so it tests the same pool every
  Marten store uses. A failure is `Unhealthy`, and the endpoint answers `503`.
- **keycloak**: fetches `<Authority>/.well-known/openid-configuration`, which is where JwtBearer
  gets its signing keys. A failure is only `Degraded`, and the endpoint still answers `200`:
  JwtBearer caches the keys, so tokens keep validating for a while, and a Keycloak outage
  shouldn't take every API replica out of rotation at once.
- Each check gives up after 3 seconds, below the probes' 5-second timeout.

Why two endpoints: restarting the API can't fix an unreachable database, so liveness never looks
at dependencies. Readiness tells a load balancer or an operator whether this replica can serve.

Both endpoints are exempt from rate limiting and aren't traced. Where they're used:

- `deploy/docker-compose.prod.yml`: the `api` healthcheck calls `/health/ready`, and the
  `frontend` healthcheck fetches `/`.
- Azure Container Apps: `deploy.sh` configures startup and liveness probes on `/health` and a readiness probe on `/health/ready` for the API, and probes Keycloak and the frontend too (see `deploy/README-azure.md`).

Code: `src/backend/buddy/Common/Health/`. Tests: `buddy.IntegrationTests/Common/Health/`.

## OpenTelemetry

The API collects traces, metrics and logs with OpenTelemetry (`Common/Observability/ObservabilityFeature.cs`):

| Signal | Sources |
|---|---|
| Traces | Incoming requests (ASP.NET Core), outgoing HTTP (Keycloak admin API, AI providers), every Postgres command (Npgsql, so all Marten reads and appends), Wolverine handlers |
| Metrics | ASP.NET Core, HttpClient, .NET runtime (GC, thread pool), Npgsql (pool usage), Wolverine |
| Logs | Every `ILogger` log, with scopes and the formatted message |

Every signal carries the resource `service.name=buddy-api`, `service.version` (the build version
from `/version`) and `service.instance.id` (the container's host name).

### Exporting

Nothing is exported unless `OTEL_EXPORTER_OTLP_ENDPOINT` is set. Point it at any OTLP receiver,
such as an OpenTelemetry Collector, the Aspire dashboard, Grafana Alloy, Honeycomb or Azure
Monitor's OTLP ingestion. The SDK reads the other standard variables itself, for example
`OTEL_EXPORTER_OTLP_PROTOCOL` (default `grpc`, port 4317; use `http/protobuf` for port 4318) and
`OTEL_EXPORTER_OTLP_HEADERS` for an API key.

- **Oracle VM**: set `OTEL_EXPORTER_OTLP_ENDPOINT` in `deploy/.env`. The compose file passes it to
  the `api` service and leaves it empty when it's unset.
- **Locally**: run any OTLP receiver and set the variable before starting the API. For example,
  the standalone Aspire dashboard shows traces, metrics and structured logs in one UI:

  ```bash
  docker run --rm -p 18888:18888 -p 4317:18889 mcr.microsoft.com/dotnet/aspire-dashboard:latest
  OTEL_EXPORTER_OTLP_ENDPOINT=http://localhost:4317 dotnet run --project src/backend/buddy
  ```

  The dashboard prints a login link with a token on startup. In the devcontainer, Docker runs
  nested, so `localhost` may not reach the dashboard. Use the address the API can actually reach.

### Logs and correlation

- In Development, console logs stay human-readable. Everywhere else, they are JSON lines with
  their scopes as fields, so a log shipper can index them.
- Every log written during a request carries `TraceId` and `SpanId`, which lead to the trace.
- An error response's `requestId` (`ErrorEnvelope`) *is* the trace id: `UseObservability` sets
  `HttpContext.TraceIdentifier` to it at the start of every request. A user-reported error
  therefore leads straight to its log lines and its trace.
- Logs exported over OTLP carry no scopes. ASP.NET Core's `RequestPath` scope holds the raw path,
  and some paths contain secret tokens (below). `TraceId` and `SpanId` are fields of every
  exported log record anyway. The local JSON console keeps its scopes.

### Secret tokens in URLs

iCal feeds (`/calendars/{id}/ical/{token}`, `/mealplans/{id}/ical/{token}`), shared sleep diaries
(`/sleep-diary/shared/{token}`) and invite links (`.../{token}/accept`, `.../{token}/preview`) carry
a secret in the path. A span's `url.path` shows `{token}` in its place (`RedactSecretPath`, keyed on
the route parameter name `token`). Name a new secret route parameter `token` too, and it's covered.

Health probes are left out of traces: they would arrive every few seconds and bury real
requests. Tests: `buddy.IntegrationTests/Common/Observability/`.

## What the API logs

Besides the framework's own logs (requests, health check failures), the API writes these.

Unhandled exceptions are logged at Error by
`Microsoft.AspNetCore.Diagnostics.ExceptionHandlerMiddleware`, and the client gets a 500/503
`ErrorEnvelope` whose `requestId` matches the log line (`Common/Errors/ExceptionHandlingFeature.cs`).
An exception from a Wolverine handler also gets Wolverine's own Error entry, under the command's
name. Both entries share the TraceId.

Every audit-style entry below is a source-generated `[LoggerMessage]` method in a `<Domain>Log.cs` file
next to the feature, with a stable EventId, so logs can be filtered by event rather than by
message text.

| EventIds | File | What |
|---|---|---|
| 1001–1007 | `Features/Users/UsersLog.cs` | Account provisioned or deleted, email changed or verified, rejected verification attempts, calls before provisioning, a failed sync of a verified email to Keycloak |
| 2001–2009 | `Features/Guardians/GuardiansLog.cs` | Child accounts created, deleted and their passwords reset; guardian invites sent, accepted, refused, revoked; guardian links given up; Keycloak admin API failures |
| 3001–3007 | `Features/Groups/GroupsLog.cs` | Group invites sent, accepted, refused, revoked; members removed or given a role; groups deleted |
| 4001–4006 | `Features/Calendars/CalendarsLog.cs` | Calendar members removed or given a role, calendars deleted, iCal tokens issued or revoked; a failed star-count update (swallowed on purpose, so it's logged) |
| 5001–5004 | `Features/SleepDiaries/SleepDiariesLog.cs` | Share links created or revoked, every view of a shared diary, and every read of a child's diary entries |
| 6001–6011 | `Features/Mealplans/MealplansLog.cs` | AI provider keys set or removed, active provider changed, provider failures, meal plan iCal tokens, AI conversations erased after 30 days (and a failed sweep), AI data sharing acknowledged |
| 7001–7002 | `Email/EmailLog.cs` | Each email sent (by kind), or the SMTP failure |
| 8001–8005 | `Common/CommonLog.cs` | Concurrency conflicts, unbindable requests, rate-limit rejections, idempotency cleanup |
| 9001–9002 | `Features/Medicines/MedicinesLog.cs` | Every read of a child's medicine schedules or doses, with the reader and the access path (`HealthDataAccessPath`: guardian, the child themself, or a group share and its id) |
| 10001–10006 | `Features/Privacy/PrivacyLog.cs` | Users erased, orphaned children erased, family data passed to a sibling, an erasure that stopped halfway, a failed sweep, a personal data export |

Existing framework-adjacent logs: unbindable requests (`RequestBindingFailureMiddleware`),
concurrency conflicts (`ConcurrencyConflictMiddleware`), rate-limit rejections
(`RateLimitingFeature`) and idempotency cleanup (`IdempotencyCleanupService`).

### Rules for new log lines

- **IDs only.** Never log an email address, a name, a username, a password, a token or a share
  link, an API key, a provider's response body, or what someone wrote: notes, diary entries,
  messages to the AI assistant. Buddy holds children's health data, and logs are kept and copied
  more widely than the database. `AuditLogTests` fails if a known name or address shows up in
  any log line.
- **Log after the change is saved**, and only when something changed. The idempotent no-op paths
  don't log.
- **Levels:**
  - Information: an access or account change worth an audit trail.
  - Warning: something failed or was refused, and it's worth a look (a provider error, an invite
    accepted from the wrong account, a swallowed failure).
  - Error: an infrastructure call failed and the request fails with it.
- **Handlers are static classes**, so they take `ILogger<TheCommand>` as a parameter. Wolverine
  injects it, and the log category becomes the use case's name.
- **EventIds:** use the next free number in the domain's range. A new domain gets the next
  thousand.
