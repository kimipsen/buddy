# Buddy API

The Buddy API is an ASP.NET Core + Wolverine backend for guardian and child
workflows. It uses Marten over PostgreSQL for event-sourced aggregates and
read-side projections, Keycloak for authentication, and FluentValidation for
request validation.

For domain concepts, HTTP semantics, and feature flows, see the
[backend documentation](../../../docs/backend/README.md).

## Prerequisites

The supported environment is the repository's VS Code development container;
see the [development container guide](../../../.devcontainer/README.md). It
provides the .NET SDK and starts PostgreSQL, Keycloak, and Mailpit with the
connection strings and authority already wired up in
[`appsettings.Development.json`](appsettings.Development.json).

For development outside the container, a running PostgreSQL instance and a
configured `buddy` Keycloak realm are required before authenticated flows can
work; see the [development container guide](../../../.devcontainer/README.md)
for first-run Keycloak setup steps.

## Run

From this directory:

```bash
dotnet run
```

The API listens on `https://localhost:7076` (`http://localhost:5193`), as
declared in [`Properties/launchSettings.json`](Properties/launchSettings.json).
OpenAPI is available in development at `/openapi/v1.json`.

## Configuration

Configuration follows the standard ASP.NET Core layering
(`appsettings.json` → `appsettings.Development.json` → environment
variables). Notable sections:

- `Authentication:Keycloak` / `Authentication:KeycloakAdmin` — token
  validation authority/audience and the admin API client used for
  Keycloak-backed user management.
- `Cors:AllowedOrigins` — origins allowed to call the API; the frontend dev
  server origins are set in `appsettings.Development.json`.
- `Mail` — SMTP host for verification and invite email (Mailpit in
  development).
- `ConnectionStrings:Postgres` — the Marten/PostgreSQL connection string.
- `AiAssistant` — default model per AI provider for the mealplan AI
  assistant (`OpenAiModel`, `GeminiModel`, `AnthropicModel`); per-family
  provider selection and API keys are stored as events, not configuration.

Secrets (Keycloak admin client secret, AI provider API keys, etc.) belong in
user secrets or environment variables in any environment beyond the checked-in
development defaults, never in `appsettings.json`.

## Tests

Run from the repository root — see the [testing guide](../../../docs/testing.md)
for full details, filters, and CI wiring:

```bash
dotnet test src/backend/backend.slnx --configuration Release
```

The integration suite uses Testcontainers for PostgreSQL, Keycloak, and
Mailpit, so a working Docker daemon is required (the dev container and CI both
provide one). Mutation testing runs separately with Stryker.NET from
`buddy.IntegrationTests`; see the
[mutation testing strategy](../../../docs/backend/analysis/mutation-testing-strategy.md).

## Project layout

- `Features/` — one directory per bounded feature area (`Users`, `Guardians`,
  `Groups`, `Calendars`, `Medicines`, `Mealplans`, `Pickups`, `TaskLibrary`,
  `Progress`), each registered from `Program.cs` via an
  `Add<Feature>Feature(...)` extension method. Within a feature, commands
  follow a `<Command>.Command.cs` / `.Endpoint.cs` / `.Handler.cs` (and
  optional `.Validator.cs`) split, with shared `Types/` for aggregates, IDs,
  and events.
- `Common/` — cross-cutting concerns shared by features: idempotency, rate
  limiting, validation helpers, and the `Result`/`ErrorEnvelope` types used
  for endpoint responses.
- `Email/` — SMTP sending abstraction (`IEmailSender`) used for verification
  and invite email.
- `Serialization/` — JSON converters, including strongly-typed ID support.
- `Program.cs` — host setup: Wolverine configuration, CORS, OpenAPI, and
  feature registration.

Event-sourced aggregates and their Marten schemas are documented per feature
in the [backend flow docs](../../../docs/backend/README.md); see also
[Aggregate roots and their relationships](../../../docs/backend/analysis/aggregate-roots.md).
