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

### Waiting for async work in component tests

The frontend has no `zone.js` dependency and runs zoneless. Component tests
that use `fixture.whenStable()` to wait for a component's `ngOnInit` (or a
click handler) to finish its async work will find that it resolves
immediately and does nothing, because `ApplicationRef`'s stability tracking
only waits on `PendingTasks` entries -- things like in-flight `HttpClient`
requests -- and a plain `Promise` returned by a stubbed/mocked service is
never registered as one. Tests built against `provideHttpClient` +
`HttpTestingController` are unaffected, since `HttpClient` registers a
pending task per request; this only bites tests that stub the service layer
directly (the pattern used throughout this app's component specs).

The fix used across the child feature specs
([home.spec.ts](../src/frontend/buddy/src/app/features/child/home/home.spec.ts),
[child-mealplan.spec.ts](../src/frontend/buddy/src/app/features/child/mealplan/child-mealplan.spec.ts))
is a macrotask flush instead of `whenStable()`:

```ts
async function settle(fixture: ComponentFixture<unknown>) {
  fixture.detectChanges();
  await new Promise((resolve) => setTimeout(resolve, 0));
  fixture.detectChanges();
}
```

A `setTimeout` callback only runs after the microtask queue is fully drained,
so this reliably flushes any depth of chained `await`s in a mocked service
call (including a `Promise.all` of several mocked calls), as long as nothing
in that chain schedules a further macrotask itself.

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
