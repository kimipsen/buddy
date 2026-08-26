# Testing

Buddy has frontend unit tests, backend integration and event-shape tests, and
manual mutation-testing workflows. Run commands from the repository root unless
a command changes directory explicitly.

## Frontend

Install dependencies once:

```bash
cd src/frontend/buddy
npm install
```

Run the Angular unit suite once with the Vitest-backed builder:

```bash
npm test -- --watch=false
```

Run mutation testing with StrykerJS:

```bash
npm run test:mutation
```

The HTML mutation report is written to
`src/frontend/buddy/reports/mutation/index.html`. See the
[frontend overview](frontend/README.md#mutation-testing) for configuration and
resource considerations.

## Backend

Build the complete backend solution:

```bash
dotnet build src/backend/backend.slnx --configuration Release
```

Run the full integration suite:

```bash
dotnet test src/backend/backend.slnx --configuration Release
```

The integration suite uses Testcontainers for PostgreSQL, Keycloak, and
Mailpit. A working Docker daemon is required. The development container and the
GitHub-hosted CI runner both provide one.

Run only the container-free event serialization checks:

```bash
dotnet test src/backend/backend.slnx \
  --filter FullyQualifiedName~EventShapeTests
```

Run a narrower test class or namespace with the standard .NET test filter:

```bash
dotnet test src/backend/backend.slnx \
  --filter FullyQualifiedName~Features.Pickups
```

The [integration testing strategy](backend/analysis/integration-testing-strategy.md)
documents the Testcontainers fixture, endpoint coverage guard, and event-shape
golden files.

## Backend mutation testing

Restore the repository-local Stryker.NET tool and run it from the integration
test project:

```bash
cd src/backend/buddy.IntegrationTests
dotnet tool restore
dotnet stryker
```

A full run is intentionally slow because mutants are checked against real
container-backed integration tests. Reports are written under
`src/backend/buddy.IntegrationTests/StrykerOutput/`. See the
[mutation testing strategy](backend/analysis/mutation-testing-strategy.md) for
verified scoping instructions and expected runtime characteristics.

## Continuous integration

- `.github/workflows/backend-tests.yml` restores, builds, and runs the backend
  suite for backend changes.
- `.github/workflows/mutation-testing.yml` exposes manually triggered backend
  and frontend mutation jobs.

Before submitting a documentation-only change, run the relevant Markdown/link
checks and `git diff --check`. Application tests are only necessary when the
change includes executable samples or source code.
