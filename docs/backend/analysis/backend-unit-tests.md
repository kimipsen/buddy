# Backend unit tests as the primary test source

Status: Phase 0 implemented (2026-10-16): pre-generated Wolverine handler code, shared extra test
hosts and a Keycloak memory cap; a full backend run now peaks under 1 GB in the test process. Phases
1-5 (the unit test project) are proposed, not yet implemented.

## Context

Running the backend tests (`task test:backend`, which runs `dotnet test backend.slnx`) often makes
the devcontainer run out of memory. The suite is a single project,
[buddy.IntegrationTests](../../../src/backend/buddy.IntegrationTests), with about 1,490 test cases
(1,442 `[Fact]` and 50 `[Theory]`). It was built on purpose to test against real infrastructure;
see [integration-testing-strategy.md, Goals](integration-testing-strategy.md#goals). That choice
has served the project well: endpoint coverage, the authorization matrix and the persisted event
shapes are all checked mechanically. But every change to a backend file now pays for a full stack,
and the feedback loop gets slower as features are added.

What a run starts today:

- **Three containers per run.** [BuddyApiFixture.cs](../../../src/backend/buddy.IntegrationTests/Fixtures/BuddyApiFixture.cs)
  starts Postgres 18, Keycloak 26 (a JVM) and Mailpit through Testcontainers. These run alongside
  the devcontainer's own Postgres, Keycloak and Mailpit (`.devcontainer/docker-compose.yml`), so
  two Keycloak JVMs run at once.
- **One shared Alba host,** which 237 test classes share through `[Collection(BuddyApiCollection.Name)]`.
  Wolverine was left on its default runtime code generation, so every host compiled its 228
  handler classes with Roslyn. Marten no longer generates code at runtime.
- **About 55 extra host starts** from `fixture.CreateHostAsync(...)`, each a full second copy of
  the app. xunit creates a test class per test, so the classes that start a host in
  `InitializeAsync` (RateLimiting, Observability, AuditLog, ExceptionHandling, UndoWindow) started
  one for every test and theory row, and disposed it afterwards. FeatureFlags, HealthChecks,
  DataProtection and VisitSummaryReport add a few more, some of them hosts that fail at startup on
  purpose.

About 25 files in the project are already plain unit tests: they never touch the fixture. They
include all of `EventShapeTests/`, [RecurrenceExpansionTests](../../../src/backend/buddy.IntegrationTests/Features/Calendars/RecurrenceExpansionTests.cs),
`CalendarOccurrenceExpansionTests`, `WorkPatternTests`, `MealPlanImportParsingTests`,
`AiMealFilterTests`, `ValidatedOptionsTests`, `FeatureOptionsTests`, `EventReplayTests`,
`SleepEntryDateShiftTests` and `ValueTupleJsonConverterFactoryTests`. They are only slow and
memory-hungry because they live in a project that also references Testcontainers. Its `dotnet test`
run starts the containers as soon as any collection test runs.

The production code is already easy to unit-test, which makes this change affordable:

- Handlers are static `Handle(...)` methods whose dependencies are parameters, as in
  [RescheduleItem.Handler.cs](../../../src/backend/buddy/Features/Calendars/RescheduleItem/RescheduleItem.Handler.cs).
- Handlers depend on small event-store interfaces (27 `I*EventStore` interfaces with about 165
  methods between them, for example [ICalendarEventStore](../../../src/backend/buddy/Features/Calendars/ICalendarEventStore.cs)),
  never on `IDocumentSession` or `IQuerySession` directly. No handler file references Marten.
- Results are the [`Result<T>`](../../../src/backend/buddy/Common/Result.cs) union, so a test can check the
  outcome case (`NotFound`, `Forbidden`, `Validation`, `Success`) without HTTP.

This document answers six questions: what actually uses the memory, where unit tests live, what a
unit test fakes, how handlers get the time, what stays an integration test, and how each suite is
run.

## Question 1: is the number of tests the cause of the out-of-memory errors?

**Decision: no. Each API host compiled its Wolverine handlers at runtime, and that cost several
hundred MB of native memory per host that was never released. Load pre-generated handler code
instead, share the extra test hosts, and cap Keycloak (phase 0, implemented).**

Measured in the devcontainer (15.8 GB, Docker-in-Docker, so the test containers share that memory)
with a 5-second sampler of the test process's RSS and `docker stats`, and a watchdog that killed
the test process below 1.2 GB available:

| Run                               | Test process peak | Keycloak peak | Result                         |
| --------------------------------- | ----------------- | ------------- | ------------------------------ |
| Before                            | 7.7 GB+           | ~0.9 GB       | Killed after 793 tests (3m20s) |
| Extra hosts shared per class      | 7.6 GB            | --            | All passed, killed at teardown |
| + pre-generated handler code      | 895 MB            | 2.7 GB        | 1,679 passed in 6 minutes      |
| + Keycloak heap and container cap | 891 MB            | 811 MB        | 1,679 passed in 6 minutes      |

What the measurements showed:

- **It was native memory, not the managed heap.** A `dotnet-gcdump` at 7.7 GB found only 618 MB
  of managed objects (115 MB live in a later run), and the RSS kept rising after the dump's forced
  GC. `/proc/<pid>/smaps` put 2.7 of 3 GB in anonymous memory, not mapped files.
- **It grew in steps, one per host start.** Lining up timestamped test output with the samples
  showed jumps of 400-600 MB whenever a class started an extra host (ExceptionHandling,
  FeatureFlags, HealthChecks, Observability, AuditLog), even though every host was disposed.
  The heap dump held about 57 DI containers and 54 Wolverine runtimes, plus Roslyn
  (`Microsoft.CodeAnalysis`) state.
- **Sharing hosts alone was not enough.** Cutting about 55 host starts to about 15 let the suite
  finish but left the peak at 7.6 GB. Removing runtime compilation took it to under 1 GB.

Phase 0 changes:

- **Pre-generated handler code.** `Program.cs` sets
  `opts.CodeGeneration.TypeLoadMode = TypeLoadMode.Auto`, and the generated code is committed in
  `src/backend/buddy/Internal/Generated` (228 files). `task codegen:backend` deletes and rewrites
  it; the backend CI job regenerates it and fails when it differs from the committed files. The
  output is deterministic (identical in Debug and Release, Development and Production). Auto still
  compiles a handler that has no pre-generated type yet, so a missing regeneration costs memory, not
  correctness. Production startup no longer compiles handlers either.
- **Shared extra hosts.** `BuddyApiFixture.SharedHostAsync(key, overrides, configureServices)`
  starts a host once per key and disposes it with the fixture. The classes above use it; what they
  capture (logs, spans, a clock, the exception to throw) moved to static fields reset in
  `InitializeAsync`, which is safe because the whole suite is one serial collection.
  `CreateHostAsync` stays for tests that need a brand-new host (startup failures, data protection
  keys, the health checks' broken dependencies).
- **Keycloak capped** at a 512 MB heap (`JAVA_OPTS_KC_HEAP`) inside a 1 GB container. Without a cap
  the JVM sized its heap from the whole machine and reached 2.7 GB.

Considered and rejected:

- **Running the integration suite in several smaller processes.** That splits the memory over time
  but starts the containers several times, so a run gets slower, and the per-host leak remains.
- **Generating the code before each test run without committing it.** It keeps generated files out
  of the repository, but every run builds twice, and production would still compile at startup.

## Question 2: a new project, or traits in the existing one?

**Decision: a new project, `src/backend/buddy.UnitTests`, which references `buddy` and xunit only:
no Alba, no Testcontainers, no Docker. The ~25 pure test files move into it first.**

- With no Testcontainers reference, a unit test cannot pull in `BuddyApiFixture` by accident.
  The compiler enforces the boundary; a naming rule would not.
- `dotnet test buddy.UnitTests` needs no Docker, so it also runs in a worktree while another
  worktree has the dev stack up.
- Stryker.NET can point its `test-projects` at the unit project alone. Mutants then run against
  millisecond tests instead of a full HTTP round trip (see
  [mutation-testing-strategy.md, Performance characteristics](mutation-testing-strategy.md#performance-characteristics):
  18 mutants took 14.5 minutes against the integration suite).
- `buddy.csproj` gains `<InternalsVisibleTo Include="buddy.UnitTests" />` next to the existing
  one for the integration project.

Considered and rejected: `[Trait("Category", "Unit")]` with a `--filter` in the same project. The
filter is easy to forget, the containers still start whenever a filtered-in class happens to use
the collection, and Stryker cannot exclude a trait as cleanly as a project.

The golden files used by `EventShapeTests/` (`GoldenFiles/**/*.json`) move with them, along with
their `CopyToOutputDirectory` item. The `BUDDY_UPDATE_*` regeneration switches keep working,
because they are read by the tests themselves, not by the fixture.

## Question 3: what does a unit test fake, and how?

**Decision: hand-written in-memory fakes, one per event-store interface, in
`buddy.UnitTests/Fakes/`. No mocking library. Each fake keeps a list of events per stream and
exposes `Appended` so tests can check exactly which events a handler wrote. A shared contract test
runs against both the fake and the Marten implementation, so the two stay in step.**

```csharp
public sealed class InMemoryCalendarEventStore : ICalendarEventStore
{
    private readonly Dictionary<CalendarId, List<CalendarEvent>> _streams = [];

    public List<(CalendarId Id, CalendarEvent Event)> Appended { get; } = [];

    // Arrange helper: a stream that already exists before the handler runs.
    public InMemoryCalendarEventStore With(CalendarId id, params CalendarEvent[] events) { ... }

    public Task<IReadOnlyCollection<CalendarEvent>> ReadAsync(CalendarId id, CancellationToken ct) => ...;
    public Task AppendAsync(CalendarId id, IReadOnlyCollection<CalendarEvent> events, CancellationToken ct) => ...;

    // Index lookups (ListForUserAsync, ListOwnedByGroupsAsync) are answered by rehydrating the
    // in-memory streams, not by a second copy of the Marten projection.
    ...
}
```

A handler test then reads like this:

```csharp
[Fact]
public async Task Rescheduling_a_task_with_event_timing_is_a_validation_error()
{
    var (calendars, items, groups) = Stores.WithCalendarOwnedBy(Alice, out var calendarId)
        .AndTask(out var itemId, due: new DateOnly(2026, 10, 20));

    var result = await RescheduleItemHandler.Handle(
        new RescheduleItem(Alice, calendarId, itemId, new ItemTiming.Event(...)),
        calendars, items, groups, CancellationToken.None);

    Assert.IsType<Result<CalendarItem>.Validation>(result);
    Assert.Empty(items.Appended);
}
```

Rationale:

- The interfaces are small (4–13 methods each), and the fakes are worth more than mocks. A mock
  setup asserts on how a handler called the store. A fake lets the test assert on the outcome:
  which events were written and what `Rehydrate` makes of them.
- There is precedent for hand-written test doubles of these interfaces:
  [ConcurrentWriterUserEventStore](../../../src/backend/buddy.IntegrationTests/Common/Concurrency/ConcurrentWriterUserEventStore.cs)
  already wraps `IUserEventStore` by hand.
- The risk with fakes is drift: an index lookup in the fake behaves differently from the inline
  Marten document. Contract tests catch it. An abstract `CalendarEventStoreContract` class holds
  the behaviour, and two subclasses run it: one in the unit project against the fake, and one in
  the integration project against `MartenCalendarEventStore`.

Considered and rejected:

- **NSubstitute or Moq.** Setups for `ReadAsync` returning event lists are verbose, and they test
  calls rather than outcomes.
- **Marten in a lightweight in-memory mode.** Marten has none; it needs Postgres.
- **One generic `InMemoryEventStore<TId, TEvent>`.** Each interface has its own index lookups
  (`FindInviteByTokenAsync`, `ListForUserAsync`), so a generic base class helps only for the
  `ReadAsync`/`CreateAsync`/`AppendAsync` core. Use it for that core and keep the lookups per fake.

Other dependencies get the same treatment: `IKeycloakAdminClient`, the mail sender and the AI
provider registry each get one recording fake.

## Question 4: how do handlers get the current time?

**Decision: inject `TimeProvider` into every handler that reads the clock, following the 25 files
that already do (the Feelings and VisitSummaries features). Do it one domain at a time, when that
domain gets its unit tests, not as one large rewrite.**

128 handler files call `DateTimeOffset.UtcNow` or `DateTime.UtcNow` directly. A unit test can work
around that by asserting that a timestamp falls between two readings of the clock, but every
undo-window, expiry, "today" and share-link-expiry rule needs a fixed clock to be tested at its
boundaries.

Before ([RescheduleItem.Handler.cs](../../../src/backend/buddy/Features/Calendars/RescheduleItem/RescheduleItem.Handler.cs)):

```csharp
public static async Task<Result<CalendarItem>> Handle(
    RescheduleItem command,
    ICalendarEventStore calendars,
    ICalendarItemEventStore items,
    IGroupEventStore groups,
    CancellationToken cancellationToken)
{
    ...
    var now = DateTimeOffset.UtcNow;
```

After:

```csharp
public static async Task<Result<CalendarItem>> Handle(
    RescheduleItem command,
    ICalendarEventStore calendars,
    ICalendarItemEventStore items,
    IGroupEventStore groups,
    TimeProvider time,
    CancellationToken cancellationToken)
{
    ...
    var now = time.GetUtcNow();
```

Blast radius: 128 handler files gain one parameter. Wolverine resolves it from the container, so
no endpoint changes. `TimeProvider.System` is registered once in `Program.cs` instead of in
`FeelingsFeature` (`services.TryAddSingleton(TimeProvider.System)`). Unit tests use
`Microsoft.Extensions.TimeProvider.Testing`'s `FakeTimeProvider`.

This changes production code. It changes no behaviour, but under the CLAUDE.md rule ("ask before
changing production code") it needs the user's go-ahead. See the open questions.

## Question 5: what stays an integration test?

**Decision: integration tests keep what only real infrastructure can prove. Handler logic and
edge cases move to unit tests. Each endpoint keeps at least one integration test.**

| Stays integration                                                                                     | Moves to (or starts in) unit tests                                        |
| ----------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------- |
| One happy path per endpoint (`EndpointCoverageTests` / `[CoversEndpoint]` still enforce this)         | Every `Result<T>` branch of a handler                                     |
| Auth wiring: real Keycloak tokens, `UserIdClaimsTransformation`, 401 vs 404                           | Authorization helpers (`CalendarAuthorization`, guardian `Manage`/`View`) |
| Marten persistence: snapshots, inline index documents, concurrency (`ConcurrentWriterUserEventStore`) | Aggregate `Rehydrate` / `Apply` folds                                     |
| HTTP concerns: idempotency keys, ETags, rate limiting, CORS, problem-details shape, OpenAPI contract  | Validators (93 FluentValidation classes)                                  |
| Email through Mailpit, health checks, data protection keys, feature-flag host startup                 | Pure domain helpers: recurrence, period, time-zone, import parsing        |
| The `Meta/` coverage tests and `KeycloakParityTests`                                                  | Event shape golden files (no container needed)                            |
| Store contract tests against the Marten implementations (Question 3)                                  | Store contract tests against the fakes                                    |

The integration suite gets smaller over time as edge-case tests that duplicate a unit test are
removed. Remove them per domain, once its unit tests are in place, never in bulk. Stryker on the
unit project shows whether the move lost coverage.

## Question 6: how is each suite run?

**Decision: `task test:backend` runs the unit tests only. A new `task test:backend:integration`
runs the integration project. `task test` keeps calling `test:backend`, so it no longer needs
Docker for the backend. CI runs both, because GitHub's runner has the memory and Docker.**

```yaml
test:backend:
  desc: Run backend unit tests (no Docker)
  dir: src/backend
  cmds:
    - dotnet test buddy.UnitTests

test:backend:integration:
  desc: Run backend integration tests (Testcontainers; needs Docker)
  dir: src/backend
  cmds:
    - dotnet test buddy.IntegrationTests
```

- `.github/workflows/backend-tests.yml` keeps `dotnet test backend.slnx`, which runs both projects,
  and merges the coverage of both.
- `task docs:openapi` keeps its filtered integration run (the OpenAPI document test needs the
  host).
- Skills: `backend-feature` writes unit tests for every handler branch and one integration test
  per endpoint. `claude-backend` describes both projects. `mutation-fix-backend` strengthens unit
  tests first. The `feature-from-analysis` step 4 command becomes `task test:backend` plus
  `task test:backend:integration` before calling a slice done. CLAUDE.md's Stack and Commands
  lines change to match.
- Stryker: `stryker-config.json` moves to `buddy.UnitTests` with
  `"test-projects": ["buddy.UnitTests.csproj"]`. Integration-only code (endpoints, Marten stores)
  is excluded from `mutate`, so it isn't reported as a survivor.

Considered and rejected: dropping integration tests from CI. They are the only check of auth,
persistence and HTTP wiring, and CI has the resources to run them.

## Phases

| Phase                            | Work                                                                                                                                                            | Done when                                                                      |
| -------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------ |
| 0. Stop the out-of-memory errors | Done: measured; pre-generated handler code with a CI check; extra hosts shared per class; Keycloak heap and container cap                                       | A full integration run completes in the devcontainer, with peaks recorded here |
| 1. Split the projects            | `buddy.UnitTests`; move the ~25 pure files and golden files; `InternalsVisibleTo`; task, CI and CLAUDE.md changes                                               | `task test:backend` runs without Docker                                        |
| 2. Test infrastructure           | Generic in-memory store core, 27 fakes, store contract tests in both projects, event builders, recording fakes for Keycloak/mail/AI                             | Contract tests pass against fakes and Marten                                   |
| 3. Validators and pure domain    | 93 validator test classes; `Rehydrate` for each aggregate; authorization helpers                                                                                | Every validator rule has a passing and a failing case                          |
| 4. Handlers, domain by domain    | `TimeProvider` in that domain's handlers; one test class per handler covering each `Result<T>` branch and the events written; trim duplicated integration tests | All 19 feature folders covered                                                 |
| 5. Tooling                       | Stryker on the unit project; update `backend-feature`, `claude-backend`, `mutation-fix-backend`, `feature-from-analysis` and their description evals            | A Stryker run on one slice takes minutes, not a quarter of an hour             |

Phase 0 has landed (see Question 1). Phase 4 can run in parallel,
one worktree per domain, once phase 2 has landed.

## Failure and edge-case behavior

| Case                                                         | Behavior                                                                                                     |
| ------------------------------------------------------------ | ------------------------------------------------------------------------------------------------------------ |
| A fake's index lookup differs from the Marten document       | The shared store contract test fails in one of the two projects                                              |
| A new endpoint ships with unit tests only                    | `EndpointCoverageTests` still fails in the integration run (CI)                                              |
| A new event type ships without a golden file                 | `EventGoldenFileCoverageTests` still fails; it moves to the unit project                                     |
| A developer runs only `task test:backend` locally            | Wiring bugs are caught by CI's integration run, not locally; `feature-from-analysis` runs both before "done" |
| A handler is changed and still reads `DateTimeOffset.UtcNow` | A source test in the unit project fails for handlers in domains that already moved to `TimeProvider`         |
| Two worktrees run backend tests at once                      | Unit tests have no shared state; integration tests already get their own Testcontainers per run              |

## Estimate

|                  |                                                                                                                                                                                                                                 |
| ---------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Complexity       | High -- 223 handlers and 93 validators to cover, 27 store fakes with contract tests, a 128-file `TimeProvider` refactor, and changes to tasks, CI, Stryker and four skills                                                      |
| Single developer | Phase 0: done. Phases 1-3: 4-6 days. Phase 4: 3-4 weeks. Phase 5: 1 day. About 5-6 weeks in all                                                                                                                                 |
| AI agent         | Phase 0: done (about 1 hour, mostly measuring). Phases 1-3: about 1 day. Phase 4: 1-2 hours per domain across 19 domains in parallel worktrees, about 3-5 days of wall-clock time. Plus about 1 hour of human review per domain |

## Decisions made

| Question                      | Decision                                                                                                     |
| ----------------------------- | ------------------------------------------------------------------------------------------------------------ |
| Cause of out-of-memory errors | Runtime handler compilation in every API host, not the test count; pre-generated code is committed (phase 0) |
| Where unit tests live         | A new `buddy.UnitTests` project with no Docker dependency, so the compiler enforces the boundary             |
| What a unit test fakes        | Hand-written in-memory event stores kept honest by contract tests; no mocking library                        |
| The clock                     | `TimeProvider`, injected one domain at a time, following Feelings and VisitSummaries                         |
| What stays integration        | One happy path per endpoint, auth, persistence, HTTP concerns, email and the meta coverage tests             |
| How suites run                | `task test:backend` = unit; `task test:backend:integration` on demand; CI runs both                          |

## Remaining open questions

- **May handlers be changed to take `TimeProvider`?** It's a production-code change across 128
  files, with no change in behaviour. Lean: yes, one domain at a time in phase 4.
- **Should the pre-commit hook run the backend unit tests?** They should take seconds once moved.
  Lean: run them only when staged files include `src/backend/**`.
- **Coverage gate.** The integration suite has no coverage threshold today. Lean: none until phase
  4 is done, then set one on the unit project from the measured baseline, as
  [mutation-testing-strategy.md](mutation-testing-strategy.md) does for mutation scores.

## Diagram

```mermaid
flowchart TB
    subgraph Local["Devcontainer (default)"]
        direction TB
        TaskUnit["task test:backend"] --> Unit["buddy.UnitTests\nno Docker"]
        Unit --> Handlers["Handlers\nstatic Handle(...)"]
        Unit --> Validators["Validators"]
        Unit --> Aggregates["Aggregate Rehydrate"]
        Handlers --> Fakes["In-memory event stores\n+ FakeTimeProvider"]
        Fakes -. "same contract tests" .- Contract["Store contract"]
    end
    subgraph OnDemand["On demand / CI"]
        direction TB
        TaskInt["task test:backend:integration"] --> Integration["buddy.IntegrationTests"]
        Integration --> Fixture["BuddyApiFixture\nPostgres + Keycloak + Mailpit"]
        Integration --> Marten["Marten event stores"]
        Marten -. "same contract tests" .- Contract
        Integration --> Meta["Endpoint, golden-file and\nOpenAPI coverage"]
    end
    CI["GitHub backend-tests.yml"] --> Unit
    CI --> Integration
```
