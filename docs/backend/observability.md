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
- Azure Container Apps: no probes are configured yet (see `deploy/README-azure.md`).

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
- Every log written during a request carries `TraceId` and `SpanId`, which lead to the trace,
  and `RequestId`.
- `RequestId` is the same value an error response returns as `requestId` (`ErrorEnvelope`). A
  user-reported error therefore leads to its log lines, and from there to the whole trace.

Health probes are left out of traces: they would arrive every few seconds and bury real
requests. Tests: `buddy.IntegrationTests/Common/Observability/`.
