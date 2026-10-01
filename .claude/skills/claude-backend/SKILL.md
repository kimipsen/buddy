---
name: claude-backend
description: Buddy .NET 11 backend conventions - vertical-slice features, event sourcing on Marten (per-feature event schemas plus inline snapshot projections in a shared `snapshots` schema), WolverineFx handlers, FluentValidation, minimal-API endpoints, the Result<T> union, and Alba + Testcontainers integration tests. Use when creating or changing a feature/use case, command, handler, endpoint, validator, domain event, aggregate, ID type, event store, snapshot projection or backend integration test under src/backend.
---

# Buddy Backend (.NET 11, Marten, Wolverine)

Scope: `src/backend/` - `buddy/` (the API), `buddy.IntegrationTests/` (all backend tests), `backend.slnx`. There is no EF Core and no `DbContext` in Buddy; persistence is Marten on Postgres. EF Core guidance for *other* services lives in `references/efcore.md` - don't apply it here.

Read before designing anything non-trivial:
- `docs/backend/analysis/event-stream-snapshots.md` - snapshot projections and the Marten gotchas behind the patterns below.
- `docs/backend/http-status-codes.md` - status codes per operation, error envelope, `Idempotency-Key`.
- `docs/backend/glossary.md` - domain vocabulary; use these names.
- `docs/testing.md` - how to run tests, Docker requirement, mutation testing.
- The feature's own analysis doc under `docs/backend/analysis/` and `docs/backend/<domain>/` if one exists.

Start in planning mode: for anything beyond a one-file change, give a short plan (assumptions, files, trade-offs) before code.

## 1. Layout - vertical slices, domain first

```
src/backend/buddy/
  Program.cs                      # Add<Domain>Feature(...) / Map<Domain>Feature() per domain
  Common/                         # Result.cs, Unit.cs, ErrorEnvelope.cs, Idempotency/, RateLimiting/, Validation/
  Serialization/                  # StronglyTypedIdJsonConverterFactory, ValueTupleJsonConverterFactory
  Features/<Domain>/
    <Domain>Feature.cs            # DI + Marten store + endpoint group
    I<Domain>Store.cs             # `public interface IPickupsStore : IDocumentStore;`
    I<Aggregate>EventStore.cs     # store contract, domain types in/out
    Marten<Aggregate>EventStore.cs
    <Domain>Authorization.cs      # access tiers -> Allowed/NotFound/Forbidden
    <Domain>.http                 # manual requests for every endpoint
    Types/                        # IDs, events union, aggregate, *Snapshot + *SnapshotProjection, *Document
    <UseCase>/
      <UseCase>.Command.cs        # also used for queries (GetGroup.Command.cs)
      <UseCase>.Handler.cs
      <UseCase>.Endpoint.cs
      <UseCase>.Validator.cs      # when the command has structural rules
```

Reference slice: `src/backend/buddy/Features/Pickups/` (`AssignPickup/`, `PickupsFeature.cs`, `MartenPickupScheduleEventStore.cs`, `Types/`).

- One folder per use case; namespace is `buddy.Features.<Domain>` for every file in the domain (no sub-namespace per use case).
- Business rules stay inside their slice. Only generic technical code goes to `Common/` (e.g. `Common/Validation/DateRangeRules.cs`).
- Cross-domain needs go through the other domain's `I*EventStore` (e.g. Pickups injects `IGuardianLinkEventStore`), never its Marten store. Register the dependency's feature first in `Program.cs` and say so in a comment.
- New domain: add `Add<Domain>Feature` and `Map<Domain>Feature` calls in `Program.cs`, its own OpenAPI document (`OpenApiDocumentName`), and a `<Domain>.http`.

## 2. Types

- **IDs**: one `sealed record` per ID wrapping a `Guid`, with a UUIDv7 factory - see `Features/Pickups/Types/PickupScheduleId.cs`:
  ```csharp
  public sealed record PickupScheduleId(Guid Value)
  {
      public static PickupScheduleId New() => new(Guid.CreateVersion7());
  }
  ```
  Always `Guid.CreateVersion7()`, never `Guid.NewGuid()` for domain IDs. Keep the single `Value` ctor parameter and make the ID a top-level type - `StronglyTypedIdJsonConverterFactory` serializes it as the bare value only under those conditions. Don't switch existing IDs to `readonly record struct`: they are persisted inside event JSON and the codebase is consistent on `sealed record`.
- Other domain concepts get small types too (`Features/Users/Types/Email.cs`, `Name.cs`, `Language.cs`) instead of raw `string`.
- Aggregates: immutable `sealed record`s, collections as `ImmutableDictionary`/`ImmutableList`.

## 3. Events - C# `union`

Pattern: `Features/Pickups/Types/PickupEvents.cs`, `Features/Users/Types/UserEvents.cs`.

```csharp
public union PickupEvent(PickupScheduleCreated, PickupAssigned, PickupCleared)
{
    public static PickupEvent FromPayload(object payload) => payload switch { ...,
        _ => throw new ArgumentException($"Unknown pickup event payload: {payload.GetType().Name}", nameof(payload)) };
    public string EventType => this switch { PickupScheduleCreated => nameof(PickupScheduleCreated), ... };
}
public sealed record PickupScheduleCreated(PickupScheduleId Id, UserId ChildId, DateTimeOffset OccurredAt);
```

- Cases are top-level `sealed record`s next to the union; every event carries `OccurredAt`.
- A union is closed and `switch` *expressions* over it are exhaustiveness-checked (CS8509). `src/backend/Directory.Build.props` sets `TreatWarningsAsErrors`, so a missing case fails the build. Switch *statements* are not checked - prefer expressions over unions. In a new project without global warnings-as-errors, add `<WarningsAsErrors>$(WarningsAsErrors);CS8509</WarningsAsErrors>`.
- A union is a value type: `GetType().Name` on a boxed union is always the union's name. Use `EventType` for discriminators and `e.Value` (the case record) for persistence.
- Requires `net11.0` + `<LangVersion>preview</LangVersion>` (already in `Directory.Build.props`).
- Adding a new event: add the case to the union, `FromPayload`, `EventType`, the aggregate fold, the snapshot projection (`Apply`), the feature's `EventTypes` array in `<Domain>Feature.cs`, and an event-shape golden test (section 8).
- Events are persisted JSON. Renaming/removing a field or a type breaks existing streams - add a new event or field instead, and expect the golden file diff.

## 4. Aggregates and rehydration

Pattern: `Features/Pickups/Types/PickupSchedule.cs`.

- `public static T? Rehydrate(IEnumerable<TEvent> events) => events.Aggregate((T?)null, Fold);`
- `public static T? Fold(T? state, TEvent @event)` - one event step, reused by the snapshot projection. Do **not** name it `Apply`/`Create`: JasperFx's projection source generator scans those names on any projection document type.
- Command handlers that decide on current state rehydrate from `ReadAsync`. Read-only handlers (Get/List) use `FindSnapshotAsync` (`Features/Groups/GetGroup/GetGroup.Handler.cs`).
- Append only when something changed (compare before/after) - this is what makes PUT/DELETE idempotent (see `AssignPickupHandler`'s `unchanged` check).

## 5. Persistence - Marten

Store registration: `Features/Pickups/PickupsFeature.cs`. Event store: `Features/Pickups/MartenPickupScheduleEventStore.cs`.

- One Marten store per domain: `services.AddMartenStore<I<Domain>Store>(...)` with `options.DatabaseSchemaName = "<domain>"`, `Events.StreamIdentity = StreamIdentity.AsGuid`, `Events.AddEventTypes(EventTypes)`.
- Connection: every store shares the process-wide pool. Call `services.AddPostgresDataSource(configuration)` in the feature (idempotent) and `options.Connection(serviceProvider.GetRequiredService<NpgsqlDataSource>())` in the store lambda, never `options.Connection(connectionString)`, which gives the store a private pool. `Common/Postgres/PostgresDataSource.cs` builds it from `ConnectionStrings:Postgres` with a default `Maximum Pool Size=50` and `Application Name=buddy` (explicit values in the connection string win). Marten doesn't dispose a supplied data source; the DI container does. `PostgresDataSourceHostTests` asserts every store uses it - add a new store's interface to its list.
- Serializer: `UseSystemTextJsonForSerialization(enumStorage: EnumStorage.AsString, ...)` and add `StronglyTypedIdJsonConverterFactory` (+ `ValueTupleJsonConverterFactory` if any state is keyed by a `ValueTuple`). Marten's JSON options are separate from `Program.cs`'s HTTP JSON options - register converters in both.
- Event store methods: `QuerySession()` for reads, `LightweightSession()` + `SaveChangesAsync` for writes. Go through the `Common/Concurrency/StreamVersionTracker` extensions, never `session.Events.*` directly: `session.ObserveStream(id.Value, events)` right after `FetchStreamAsync` in `ReadAsync`, `session.StartTrackedStream(id.Value, payloads)` on create, `session.AppendTracked(id.Value, payloads)` after. Payloads are `e.Value ?? throw new InvalidOperationException(...)`. Map back with `<Union>.FromPayload(e.Data)`. Return domain types from the interface, never Marten types.
- `AppendAsync` returns early on an empty event list.
- Lookup documents (`PickupScheduleIndexDocument`, `GroupMembershipDocument`) are written in the same session as the event append so they commit atomically.
- **Optimistic concurrency** (read-modify-append): `StreamVersionScopeMiddleware` (Wolverine middleware on every handler, `Program.cs`) opens a per-invocation scope; `ReadAsync` records the stream version it saw and the matching `AppendAsync` becomes an expected-version append. A concurrent writer in between makes Marten throw `EventStreamUnexpectedMaxEventIdException` (a `JasperFx.ConcurrencyException`), which `ConcurrencyConflictMiddleware` renders as `409 concurrency_conflict` (`ErrorEnvelope`). Handlers and endpoints need nothing extra, as long as the decision is made on a `ReadAsync` in the same handler invocation (nested `IMessageBus.InvokeAsync` shares the scope). Not covered: appends with no prior `ReadAsync` of that stream (e.g. guardian invites decided from `GuardianInviteDocument`), decisions made on `FindSnapshotAsync`, and store calls outside a handler, which all stay plain appends. Test: `buddy.IntegrationTests/Common/Concurrency/OptimisticConcurrencyTests.cs`.
- Concurrency on create: rely on a DB constraint (`session.Insert` + catch `DocumentAlreadyExistsException`), see `MartenUserEventStore.CreateAsync`. No in-memory locks.

### Snapshots

Pattern: `Features/Pickups/Types/PickupScheduleSnapshotProjection.cs`.

- Document is a wrapper `sealed record <Agg>Snapshot(Guid Id, <Agg> <Agg>)` - Marten can't use a `sealed record` ID class as a document Id.
- Projection: `SingleStreamProjection<<Agg>Snapshot, Guid>` with `Create(<FirstEvent>)` and one `Apply(current, <Event>)` per later event, each delegating to `<Agg>.Fold`.
- Register in `<Domain>Feature.cs`:
  ```csharp
  options.Projections.Register(new PickupScheduleSnapshotProjection(), ProjectionLifecycle.Inline);
  options.Schema.For<PickupScheduleSnapshot>().DatabaseSchemaName("snapshots");
  ```
  `Register(...)`, not `Projections.Snapshot<T>()` (throws for these document types). Always `Inline`, always schema `snapshots`. Snapshots are derived, rebuildable state - events remain the source of truth.
- Every snapshot gets a `SnapshotTests/<Agg>SnapshotTests.cs` asserting snapshot == full replay.

### `Reverse()` gotcha

A read-backward query (`OrderByDescending(e => e.Version).Take(n).ToListAsync()`) returns `IReadOnlyList<T>`. `.Reverse()` on it is `Enumerable.Reverse()` - non-mutating. Use the result: `return [.. events.Reverse().Select(...)];`. A bare `events.Reverse();` silently does nothing. See `MartenUserEventStore.ReadBackwardAsync`.

## 6. Commands, handlers, validators - Wolverine + FluentValidation

- Command: `sealed record <UseCase>(UserId? UserId, ...)` plus `static <UseCase> FromClaims(ClaimsPrincipal principal, ...)` using `principal.GetUserId()`. See `AssignPickup.Command.cs`.
- Handler: `public static class <UseCase>Handler` with `public static async Task<Result<T>> Handle(<UseCase> command, <deps...>, CancellationToken cancellationToken)`. Wolverine discovers it by convention and injects parameters - no registration.
- Handler order (from `AssignPickup.Handler.cs`):
  1. `if (await validator.ValidateCommandAsync(command, ct) is { } problem) return new Result<T>.Validation(problem);`
  2. `if (command.UserId is not { } userId) return new Result<T>.NotFound();`
  3. Authorization via `<Domain>Authorization.Check*` -> `access.ToDeniedResult<T>()`.
  4. Async/relationship checks needing the DB -> `ValidationProblem.Of("message")`.
  5. Load, decide, append (only if changed), return `Success`.
- Validator: `sealed class <UseCase>Validator : AbstractValidator<<UseCase>>`, structural rules only. Auto-registered by `AddValidatorsFromAssemblyContaining<Program>()`. DB-backed rules stay in the handler (see `docs/backend/analysis/validation-rules.md`).
- Handlers can call other handlers via an injected `IMessageBus` (`SetTaskCompletion.Handler.cs`).
- Timestamps: `DateTimeOffset.UtcNow` once per handler, reused across the events it emits.

## 7. Result pattern and endpoints

`Common/Result.cs`:
```csharp
public union Result<T>(Result<T>.Success, Result<T>.NotFound, Result<T>.Forbidden, Result<T>.Validation) { ... }
```
- Expected outcomes (validation, not found, forbidden, business-rule rejection) are `Result<T>` values, not exceptions. No success payload -> `Result<Unit>`.
- Throw only for programmer errors, corrupt/unmapped data (`FromPayload` default arm, `UnreachableException` in `ToDeniedResult` for `Allowed`) and infrastructure failures.
- Outcomes that don't fit the four cases get a feature-specific union (`CreateChildOutcome`, `CreateGroupOutcome` with `Unauthenticated`) - don't add cases to `Result<T>`; every switch over it would need the arm.
- `ResultExtensions.Reraise<T, TOther>()` converts a failed result to another payload type.
- No access relationship at all -> `NotFound` (don't reveal existence); relationship but insufficient tier -> `Forbidden`.

Endpoint (`AssignPickup.Endpoint.cs`, `ClearPickup.Endpoint.cs`):
- `public static class <UseCase>Endpoint` with `MapX(this RouteGroupBuilder group)`; the group in `<Domain>Feature.Map<Domain>Feature` adds `.WithTags`, `.RequireAuthorization()`, `.WithGroupName(OpenApiDocumentName)`.
- Lambda returns `Task<Results<Ok<T>, NotFound, ForbidHttpResult, BadRequest<ErrorEnvelope>>>`, builds the command, calls `bus.InvokeAsync<Result<T>>(command, ct)`, and maps with an exhaustive `switch` expression. Validation -> `TypedResults.BadRequest(problem.ToEnvelope(httpContext))`.
- Request body is a separate `sealed record <UseCase>Request` with primitives (`Guid?`), converted to domain IDs in the endpoint.
- Every endpoint has `.WithName("<UseCase>")` - the endpoint coverage test keys on it.
- Status codes per `docs/backend/http-status-codes.md`: creates return **200** with the created resource (no endpoint uses 201/`TypedResults.Created`), update 200/204, delete 204. Create-style POSTs are covered by `IdempotencyKeyMiddleware`.
- Resend throttling: `Common/RateLimiting/ResendCooldown` is the only cooldown (InviteGuardian, InviteToGroup, ResendEmailVerification). The handler returns `ResendCooldownActive` as a case of its feature-specific outcome union; the endpoint renders it with `cooldown.ToConflict(httpContext)` -> `409 resend_cooldown`.
- Add the request to `<Domain>.http`.

## 8. Tests - xunit + Alba + Testcontainers

All backend tests are in `src/backend/buddy.IntegrationTests/` (no separate unit test project).

- Fixture: `Fixtures/BuddyApiFixture.cs` - one shared Postgres/Keycloak/Mailpit set per run via `[Collection(BuddyApiCollection.Name)]`. No DB reset between tests; isolate with fresh users (`fixture.CreateAuthenticatedUserAsync()`) and fresh IDs.
- Feature tests: `Features/<Domain>/<UseCase>/<UseCase>Tests.cs`, `public sealed class X(BuddyApiFixture fixture)`, drive HTTP via `fixture.Host.Scenario(...)` with a real Keycloak token. Test names are sentences: `A_guardian_can_assign_a_sibling_as_escort`. Example: `Features/Pickups/AssignPickup/AssignPickupTests.cs`.
- Mark at least one test per endpoint `[CoversEndpoint("<EndpointName>")]` - `Meta/EndpointCoverageTests.cs` fails on any mapped endpoint without one (and on stale names).
- Cover the authorization matrix (guardian / child / unrelated user) and each `Result` arm the endpoint maps.
- Event shape: new/changed events get a test in `EventShapeTests/<Domain>EventShapeTests.cs` against `EventShapeTests/GoldenFiles/<Domain>/*.json` with fixed IDs and instants. Every registered event type has one; `Meta/EventGoldenFileCoverageTests.cs` fails on a type in any feature's `EventTypes` without a `<Type>.json`/`<Type>_<Variant>.json` (and on stale golden files). `EventShapeTestSupport` registers the union of the stores' converters (StronglyTypedId, ValueTuple, CalendarOwner) - keep it in sync when a store adds one.
- Snapshot: `SnapshotTests/<Agg>SnapshotTests.cs` (section 5).
- Run: `task test:backend` or `dotnet test src/backend/backend.slnx` (needs Docker). Mutation: `task test:mutation:backend`.

## 9. Config and secrets

- Config through `IConfiguration`/options classes (`Features/Users/PostgresOptions.cs` reads `ConnectionStrings:Postgres`, which feeds the shared `NpgsqlDataSource`). Integration tests override via `ConfigurationOverride` in the fixture.
- Never commit secrets. `appsettings.*.json` and `.env` are git-ignored; keep local secrets there or in environment variables, keep a placeholder `.env.example` tracked if you introduce env-based config. Production secrets come from CI/CD or a cloud secret store.
- Central package versions in `src/backend/Directory.Packages.props`; `.csproj` `PackageReference`s carry no version.

## 10. SonarCloud

Known false positives (union syntax, nullable, parameter count) and the one real one (`S2201` on `Reverse()`): `references/sonar-known-issues.md`. Read it before acting on a Sonar finding.

## Output format for generated code

1. Plan - steps and assumptions.
2. Implementation - files with paths under `src/backend/buddy/Features/<Domain>/...`.
3. Persistence - new event types, `EventTypes` registration, snapshot/projection and document changes.
4. Tests - feature tests with `[CoversEndpoint]`, golden files, snapshot test; command to run.
5. Notes - trade-offs, compatibility of persisted events, docs to update.

## Bundled reference (read on demand)

- `references/sonar-known-issues.md` - SonarCloud triage for this repo.
- `references/efcore.md` - EF Core DbContexts/migrations. Not used in Buddy; for other services.
- `samples/` - generic, framework-light templates (Order aggregate, union events, Result, hand-rolled Postgres event store, EF Core DbContext). Not Buddy's patterns: their `readonly record struct` IDs, hand-rolled event store and EF Core usage differ from Buddy. Use only for non-Buddy services; for Buddy, copy from `src/backend/buddy/Features/Pickups/`.
- `examples/example.txt` - example prompts.
