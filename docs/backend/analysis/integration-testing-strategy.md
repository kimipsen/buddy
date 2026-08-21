# Integration testing strategy

Buddy had no automated tests before this. This document covers the integration test suite added
across the whole backend (Users, Groups, Calendars) and, just as importantly, the mechanism for
keeping that coverage from silently rotting as the feature set grows. Status: implemented —
`src/backend/buddy.IntegrationTests` exists with full endpoint coverage, event-shape tests, and
the coverage guard described below all wired up and passing where this environment can run them
(see "Verification status" at the end).

## Goals

- Exercise real infrastructure (Postgres/Marten, Keycloak, mailpit) instead of mocking it away,
  since almost all of the interesting behavior in this codebase lives at those boundaries
  (event-sourced aggregates, JWT claims mapping, group-derived calendar authorization, email
  verification).
- Cover every HTTP endpoint at least once, plus the authorization matrix that
  [CalendarAuthorization](../../../src/backend/buddy/Features/Calendars/CalendarAuthorization.cs)
  and the group/calendar permission design in
  [group-owned-calendars-and-permissions.md](group-owned-calendars-and-permissions.md) depend on.
- Make it mechanically obvious in CI when a new endpoint or event type ships without a test,
  rather than relying on reviewers to notice.

## Project layout

New project: `src/backend/buddy.IntegrationTests/buddy.IntegrationTests.csproj`, referencing
`buddy.csproj` directly and added to the existing `src/backend/backend.slnx` (a solution file
already existed — the repo uses the newer XML `.slnx` format, not `.sln`). Package versions are
declared centrally in `src/backend/Directory.Packages.props`, alongside the main project's.
`buddy.csproj` also needed `<InternalsVisibleTo Include="buddy.IntegrationTests" />` so Alba can
see the implicitly-generated `Program` class from the main project's top-level statements.

Test files mirror the main project's feature-slice layout 1:1:

```
buddy.IntegrationTests/
  Fixtures/
    BuddyApiFixture.cs        # Testcontainers + WebApplicationFactory, see below
    TestRealm.json             # Keycloak realm export used by the fixture
  Features/
    Users/
      GetCurrentUser/GetCurrentUserTests.cs
      UpdateEmail/UpdateEmailTests.cs
      ...
    Groups/
      CreateGroup/CreateGroupTests.cs
      ...
    Calendars/
      CreateCalendar/CreateCalendarTests.cs
      CalendarAuthorizationTests.cs   # cross-cutting role/permission matrix
      ...
  EventShapeTests/
    UserEventShapeTests.cs
    CalendarEventShapeTests.cs
    GroupEventShapeTests.cs
  Meta/
    EndpointCoverageTests.cs   # the drift guard, see "Keeping tests updated"
```

The mirrored path (`Features/<Feature>/<Command>/<Command>Tests.cs` next to
`<Command>.Endpoint.cs`) is deliberate: a missing test file is visible just by looking at the
two trees side by side, before any tooling even runs.

## Libraries

- **xUnit** — already the de-facto default for ASP.NET Core/Marten/Wolverine projects (all
  three are JasperFx-authored and their own test suites use xUnit); no reason to deviate.
- **[Alba](https://jasperfx.github.io/alba/)** for HTTP scenario testing, instead of raw
  `WebApplicationFactory` + `HttpClient`. Alba is built by the same team as Marten/Wolverine
  specifically for this kind of ASP.NET Core scenario testing, and its `Scenario` API
  (`.Post.Json(...).ToUrl(...)`, `.StatusCodeShouldBe(200)`, `.ReadAsJson<T>()`) removes a lot
  of the serialization/response-parsing boilerplate `HttpClient` needs. It wraps
  `WebApplicationFactory` under the hood, so nothing is lost.
- **Testcontainers.PostgreSql**, **Testcontainers** (generic container) for Keycloak and
  mailpit. No official `Testcontainers.Keycloak` module is needed — a generic
  `IContainer` built from `quay.io/keycloak/keycloak:21.1.1` (matching the devcontainer's
  version) with a mounted realm-import file is enough.
- No new assertion library. Stick to `Assert.*` — the assertions here are mostly "status code
  X, body shape Y", which doesn't benefit much from fluent-assertion sugar, and it avoids
  pulling in a dependency (FluentAssertions' license changed; Shouldly/AwesomeAssertions are
  fine alternatives if the team wants nicer diffs later, but that's a separate call).

## The shared fixture

One `BuddyApiFixture : IAsyncLifetime`, shared across the whole run via
`[CollectionDefinition]` / `ICollectionFixture<BuddyApiFixture>` — starting three containers
per test class would dominate wall-clock time, so every test class shares one instance:

1. Start a `PostgreSqlContainer` (Testcontainers.PostgreSql), one database, one connection
   string handed to every feature's `PostgresOptions` (Users/Groups/Calendars already each get
   their own Marten schema within one database — that isolation is already correct for this).
2. Start the Keycloak container with `TestRealm.json` imported
   (`--import-realm`, file mounted at `/opt/keycloak/data/import/`). The realm defines a single
   `buddy-api` public client with the direct-grant (resource owner password) flow enabled and an
   audience mapper (so its tokens carry `aud: buddy-api`, matching `Authentication:Keycloak:Audience`),
   plus three named seed users (`alice`, `bob`, `carol`) for simple read-only smoke tests.
3. Start the mailpit container, and expose a small HTTP client against its REST API
   (`/api/v1/search`, `/api/v1/message/{id}`) so verification-email tests can assert on what was
   actually sent instead of stubbing `IEmailSender`.
4. Build an `IAlbaHost` via `AlbaHost.For<global::Program>(...)`, overriding
   `ConnectionStrings:Postgres`, `Authentication:Keycloak:Authority` (pointed at the Keycloak
   container's mapped port) and `Mail:Host`/`Mail:Port` (pointed at the mailpit container) via
   Alba's `ConfigurationOverride.Create(...)` extension.
5. Expose `GetAccessTokenAsync(username, password)` for the three named users, and — the
   primary mechanism most tests actually use — `CreateUserAsync(...)` / `CreateAuthenticatedUserAsync()`,
   which mint a brand new Keycloak user via the Admin REST API (master realm's built-in
   `admin-cli` client) and materialize its buddy `User` aggregate by calling `GET /users/me`
   (which lazily creates it from the token's claims — see `GetOrCreateUserHandler`). This turned
   out to matter more than the plan anticipated: almost every test that mutates a user's own
   profile, or needs several distinct identities at once (an authorization matrix across
   owner/contributor/viewer), needs its own throwaway identity rather than competing over three
   shared named accounts.

Because every aggregate in this codebase is keyed by a fresh `Guid` (`CalendarId`, `GroupId`,
`UserId` derived from the Keycloak subject) and almost every test mints its own fresh Keycloak
user via `CreateAuthenticatedUserAsync()`, tests don't need per-test database resets and can run
against the one shared Postgres/Keycloak/mailpit trio for the whole run without stepping on each
other.

## What to cover

For each feature (Users, Groups, Calendars), per endpoint:

- Happy path (201/200 + response shape).
- The 401 (no/invalid token) and — where relevant — 403/404 (`CalendarAccess.Forbidden` /
  `NotFound`) outcomes, since those are security-relevant branches, not incidental error
  handling.
- Validation failures the handler is responsible for (e.g. malformed time zone id).

Plus cross-cutting suites that don't map to one endpoint:

- **`CalendarAuthorizationTests`** — a table-driven test over
  `(CalendarRole via direct membership) x (GroupRole via CalendarPermissionPolicy) x operation`,
  since this is exactly the resolution logic the
  [group-owned-calendars-and-permissions.md](group-owned-calendars-and-permissions.md) design
  spent the most time on, and it's the kind of logic that's easy to regress silently.
- **Email verification flow end-to-end** — request verification, read the token out of the real
  mailpit message via a regex on its plaintext body, call the verify endpoint, assert
  `Email.IsVerified`.
- **iCal feed** — create an item, issue a token, fetch `/calendars/{id}/ical/{token}`
  anonymously, and assert the item's title appears in the returned `text/calendar` body.

## Event-shape regression tests

This is specific to an event-sourced system and distinct from the behavioral tests above: once
an event (e.g. `CalendarCreated`) has been persisted in a real environment, its JSON shape is
effectively a durable contract — Marten replays it from the stream forever. A refactor that
renames a property or changes an enum's serialization can compile fine and pass every behavioral
test while quietly breaking replay of existing history.

Add one golden-file test per event type, per feature (`UserEventShapeTests`,
`CalendarEventShapeTests`, `GroupEventShapeTests`): serialize a fixed instance of each event
through the feature's actual `System.Text.Json` configuration (same converters as
`CalendarsFeature.AddCalendarsFeature`, etc.) and compare against a checked-in JSON file. A
change to the file only happens when someone deliberately updates it, which makes an
accidental breaking change to a persisted event visible in the PR diff instead of invisible
until a production replay fails.

## Keeping tests updated

The mirrored file layout above makes gaps visible to a human. To make them impossible to merge
without noticing, add one mechanical guard rather than relying on convention alone:

**`Meta/EndpointCoverageTests.cs`** — a single test that:

1. Reflects over the running `WebApplication`'s `EndpointDataSource` to list every mapped route
   (`{HttpMethod} {RoutePattern}`), for endpoints that carry a `[WithName]`/route metadata under
   `/users`, `/groups`, `/calendars`.
2. Reflects over the `buddy.IntegrationTests` assembly for test classes/methods carrying a
   matching marker (simplest option: a `[CoversEndpoint("CreateCalendar")]` attribute on the
   `[Fact]`, matched against the `.WithName("CreateCalendar")` already set on every endpoint in
   this codebase).
3. Fails, listing the missing route(s), if any mapped endpoint has no matching
   `[CoversEndpoint]`.

This starts the moment coverage reaches 100% (end of the phased rollout below) and from then on
turns "someone shipped a new endpoint without a test" into a CI failure with the endpoint's name
in the message, instead of something a reviewer has to remember to check.

Wired into CI: `.github/workflows/backend-tests.yml` runs `dotnet restore` / `build` / `test`
against `src/backend/backend.slnx` on PRs and pushes touching `src/backend/**`. GitHub-hosted
`ubuntu-latest` runners have Docker available, so Testcontainers works without extra setup. This
repo had no CI workflows at all before this, so this also establishes the first one.

## Rollout (completed)

Delivered in this order, matching the original plan:

1. **Scaffold** — `buddy.IntegrationTests` added to `backend.slnx`, `BuddyApiFixture` with all
   three containers, `GetCurrentUserTests` as the first smoke test.
2. **Users** — all 7 endpoints covered, including the email verification end-to-end flow through
   real mailpit messages.
3. **Groups** — all 7 endpoints covered.
4. **Calendars** — all 17 endpoints covered, plus `CalendarAuthorizationTests` (explicit grant
   beats group-derived role, group-member fallback to the policy, group-deletion cascade) and
   the iCal feed round-trip.
5. **Event-shape tests** — one golden-file test per event type (6 Users + 5 Groups + 14
   Calendars/CalendarItem = 25 total), golden files under `EventShapeTests/GoldenFiles/`.
6. **`EndpointCoverageTests`** turned on — all 31 mapped endpoints (`.WithName(...)` values) have
   a matching `[CoversEndpoint("...")]`, verified with no gaps and no stale names.

## Verification status

This environment has network access (so NuGet restore works) but no Docker daemon, so the
Testcontainers-backed suite could not actually be executed here. What was verified directly:

- `dotnet build src/backend/backend.slnx` (Debug and Release) succeeds with zero warnings
  (`TreatWarningsAsErrors` is on for this repo) across both projects.
- The 25 event-shape tests need no containers at all and were run for real with
  `dotnet test --filter FullyQualifiedName~EventShapeTests` — all pass; the golden files were
  generated from that run's actual serialized output, not hand-written.
- The container-backed tests were confirmed to reach `Testcontainers`' Docker-availability check
  and fail there (`dotnet test` reports the same "cannot connect to the Docker daemon" error for
  all 76 of them) — i.e. everything up to and including container startup is exercised; the
  Keycloak realm import, Alba host wiring, and the endpoints' actual behavior are not yet
  confirmed to work end-to-end. Run the suite in the devcontainer or in CI (where Docker is
  available) to get a real pass/fail signal, and fix forward from there — the realm JSON in
  particular (audience mapper, default client scopes) is the piece most likely to need a
  small correction on first real run.

## Open follow-ups (not blocking the above)

- Whether to also assert on Marten projections directly (e.g. `CalendarMembershipDocument`)
  in addition to going through the HTTP API — likely yes for the authorization matrix, since
  driving every combination through full HTTP round-trips would be slow; worth deciding per
  test rather than as a blanket rule.
- RabbitMQ/Redis are present in `docker-compose.yml` but not wired into the app yet
  (no `UseRabbitMq`/distributed cache registration in `Program.cs`); out of scope until the
  app actually uses them.
