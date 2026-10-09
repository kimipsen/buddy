---
name: backend-feature
description: Step-by-step workflow for adding a vertical slice to the Buddy .NET backend (src/backend/buddy) - Command/Query + Validator + Wolverine Handler + minimal-API Endpoint, feature registration, the <Domain>.http entry, new event cases (union, Marten event types, snapshot projection, golden-file EventShapeTests), a new aggregate or domain (event store, inline snapshot in the `snapshots` schema, SnapshotTests), Alba integration tests for every status, and the docs to update. Ships compile-verified templates. Use for "add an endpoint", "new command", "new query", "new use case", "add a backend feature", "add an aggregate", "add an event to <domain>", "expose X over the API".
---

# Backend Feature (vertical slice)

Goal: ship one use case end to end, matching what the code already does. General conventions (union events, `Result<T>`, UUIDv7, value-type ids, Marten/Wolverine setup) live in the **`claude-backend`** skill: load it first and don't restate them. This skill is the step-by-step recipe. Paths are relative to `src/backend/buddy` unless they say otherwise.

Arguments (free text): domain, use case name, verb + route, who may call it. Ask only if the domain or the caller rules are unclear.

## 0. Orient (read, don't skip)

1. `Features/<Domain>/<Domain>Feature.cs`: store, event types, route group, and the `Map*` list.
2. `Features/<Domain>/<Domain>Authorization.cs`: which access check fits (`CheckMark`/`CheckManage`, `CheckView`/`CheckContribute`, ...).
3. The closest sibling slice in the same domain. Copy its shape over the templates when they differ.
4. `docs/backend/<domain>/flow.md`, and the API contract `docs/backend/openapi/buddy.json` (regenerate with `task docs:openapi`).

## 1. The slice: `Features/<Domain>/<UseCase>/`

| File | Shape (reference) |
| --- | --- |
| `<UseCase>.Command.cs` | `public sealed record <UseCase>(UserId UserId, ...)` + `static FromClaims(ClaimsPrincipal principal, ...) => new(principal.GetRequiredUserId(), ...)` (never null: `ProvisionedUserMiddleware` rejects an unprovisioned caller with `403 user_not_provisioned` first). Queries use the same file name (`ListTodaysDoses.Command.cs`). Ref: `Medicines/SetDoseStatus/SetDoseStatus.Command.cs` |
| `<UseCase>.Validator.cs` | `sealed class <UseCase>Validator : AbstractValidator<<UseCase>>`, structural rules only. Shared date-range rule: `this.ValidDateRange(x => x.From, x => x.To, MaxRangeDays)` (`Common/Validation/DateRangeRules.cs`). Validators are registered automatically (`AddValidatorsFromAssemblyContaining<Program>()`). Ref: `Medicines/CreateMedicineSchedule/CreateMedicineSchedule.Validator.cs` |
| `<UseCase>.Handler.cs` | `public static class <UseCase>Handler { public static async Task<Result<T>> Handle(<UseCase> command, IValidator<<UseCase>> validator, I<X>EventStore store, ..., CancellationToken ct) }`. Wolverine finds it by name. Dependencies are method parameters, not constructor parameters. |
| `<UseCase>.Endpoint.cs` | `public static RouteGroupBuilder Map<UseCase>(this RouteGroupBuilder group)`, a lambda typed `Task<Results<...>>` that builds the command, calls `bus.InvokeAsync<Result<T>>(command, ct)` and switches over the result; `.WithName("<UseCase>")`. Request/response DTO records go at the bottom of this file. Ref: `Medicines/UpdateMedicineDetails/UpdateMedicineDetails.Endpoint.cs` |

Handler order (every handler follows it, see `CreateMedicineSchedule.Handler.cs`, `AssignPickup.Handler.cs`):

1. `if (await validator.ValidateCommandAsync(command, ct) is { } problem) return new Result<T>.Validation(problem);`
2. `var access = await <Domain>Authorization.Check...(...); if (access != <Domain>Access.Allowed) return access.ToDeniedResult<T>();`
3. Load: `<Aggregate>.Rehydrate(await store.ReadAsync(id, ct))`. Treat `null`, a wrong owner (`schedule.ChildId != command.ChildId`) or a stopped/archived aggregate as `NotFound`.
4. Checks that need state stay in the handler, after authorization, and return `new Result<T>.Validation(ValidationProblem.Of("..."))`. Example: SetDoseStatus's "no dose at that time". Exception: an active resend cooldown (`Common/RateLimiting/ResendCooldown.IsActive(...)`) is not a validation failure; return `new ResendCooldownActive("...")` from a feature outcome union (`InviteGuardianOutcome`) -> 409.
5. Compare before and after. Append `[new <Event>(...)]` **only if something changed**. This is what makes PUT/PATCH/DELETE idempotent.
6. Return `Success(...)`. Use `Result<Unit>` when there is no body.

Result to HTTP mapping (`Common/Result.cs`, `Common/ErrorEnvelope.cs`, `docs/backend/http-status-codes.md`):

| Result case | Endpoint arm | Status |
| --- | --- | --- |
| `Success(var v)` | `TypedResults.Ok(Response.From(v))` / `TypedResults.NoContent()` for `Result<Unit>` | 200 / 204 |
| `Validation(var problem)` | `TypedResults.BadRequest(problem.ToEnvelope(httpContext))`, which needs an `HttpContext httpContext` parameter | 400 `validation_error` envelope |
| `Forbidden` | `TypedResults.Forbid()` | 403 (caller is related but under-privileged) |
| `NotFound` | `TypedResults.NotFound()` | 404 (no relationship counts as not found, so a stranger can't tell whether the resource exists) |

- The switch is exhaustive over the union, so don't add a `_ =>` arm. If the route never produces a case, map it to `NotFound` with a comment instead of widening `Results<...>` (`ListMedicineSchedules.Endpoint.cs`, `DeleteItem.Endpoint.cs`).
- Declare in `Results<...>` exactly the statuses the route returns. OpenAPI is generated from that list, plus the middleware responses (`401`, `403 user_not_provisioned`, `409 concurrency_conflict`, `429`, `500`, `503`, ...) that `Common/OpenApi/ErrorResponsesOperationTransformer` adds by rule. An `ErrorEnvelope` code the endpoint returns itself gets `.ProducesErrorCode(status, Code)`; a result type without metadata (`ContentHttpResult`, `JsonHttpResult<T>`) gets `.Produces<T>(status, contentType)`. Enums go over the wire by name (`JsonStringEnumConverter`); a `kind` discriminator (`KindDiscriminatedJsonConverter`) stays numeric.
- **Creates return `200 Ok`**, not 201: no endpoint uses `TypedResults.Created`. Match this convention.
- Use a feature-specific outcome union only when a case doesn't fit `Result<T>` (`CreateChildOutcome` with `UsernameUnavailable` → 409).

Wiring:

- Add `group.Map<UseCase>();` to `Map<Domain>Feature` in `<Domain>Feature.cs`. The group already applies `.WithTags("<Domain>")`, `.RequireAuthorization()` and `.WithGroupName(OpenApiDocumentName)`, and the feature has its own OpenAPI document (`services.AddBuddyOpenApiDocument(OpenApiDocumentName)`). `Program.cs` changes only for a new domain (step 3).
- Anonymous routes are the exception and need `.AllowAnonymous()` (the iCal feeds only).
- **Idempotency:** nothing to do per endpoint. `Common/Idempotency/IdempotencyKeyMiddleware.cs` covers every POST that carries an `Idempotency-Key` header, and the frontend sends one through `postIdempotent`. PUT/PATCH/DELETE get idempotency from step 6.
- **Rate limiting:** a global ASP.NET Core rate limiter (`Common/RateLimiting/RateLimitingFeature`) already covers every endpoint (per Keycloak subject, else per client IP) and answers `429 rate_limited` before the handler runs; a new endpoint needs nothing. Add `.RequireRateLimiting(RateLimitingFeature.<Policy>)` only when it calls an LLM (`AiAssistantPolicy`), sends email (`OutboundEmailPolicy`) or is an anonymous token feed (`IcalFeedPolicy`); a new anonymous endpoint must get a policy or be listed in `Meta/RateLimitingCoverageTests`. Separately, resend throttling is the shared handler check `Common/RateLimiting/ResendCooldown`; the handler returns `ResendCooldownActive` (a case of a feature-specific outcome union, e.g. `InviteToGroupOutcome`) and the endpoint maps it with `cooldown.ToConflict(httpContext)` → `409` with the `resend_cooldown` envelope. Declare `Conflict<ErrorEnvelope>` in `Results<...>`.
- **Optimistic concurrency:** nothing to do per handler. Rehydrate with `store.ReadAsync` and append with `store.AppendAsync` in the same handler; the store's `StreamVersionTracker` calls make the append expected-version, and a lost race becomes `409 concurrency_conflict` through `ConcurrencyConflictMiddleware`. Don't add a 409 arm for it. A new event store must use `session.ObserveStream` / `StartTrackedStream` / `AppendTracked` (see `claude-backend` section 5).
- **Group variant** (`<UseCase>ForGroup.*` in the same folder, route `/groups/{groupId:guid}/children/{childId:guid}/...`): authorize with `<Domain>GroupAccess.ResolveAsync(...)` and `resolved.Reraise<Unit, T>()`, then call the base handler's `internal static ...ForChildAsync(...)` helper (`SetDoseStatusForGroup.Handler.cs`). Extract that helper as soon as a second caller appears.
- **`.http` file:** add a request to `Features/<Domain>/<Domain>.http` using `{{buddy_HostAddress}}`, `Authorization: Bearer {{guardianToken}}`, and a `###` separator (`Medicines/Medicines.http`). Only Calendars, Mealplans, Medicines, Pickups, TaskLibrary and Users have one. Don't create one for other domains unless asked.

## 2. A new event on an existing aggregate

Use `Features/Medicines/Types/MedicineEvents.cs` as the model. Event names are past tense. The record holds the aggregate id, `Before`/`After` (or the new value), `UserId ModifiedBy` and `DateTimeOffset OccurredAt`.

1. `Types/<X>Events.cs`: add the record, then add the case in **three** places: the `union <X>Event(...)` list, `FromPayload` (`<NewEvent> e => e,`) and `EventType` (`<NewEvent> => nameof(<NewEvent>),`).
2. `Types/<Aggregate>.cs`: add an `Advance` arm (or a `Start` arm for a new creation event, plus it in `Advance`'s `AlreadyStarted` arm). `Advance` has no `_ =>` fallthrough, so the build fails until you do; an event that doesn't change the aggregate goes in an explicit `=> state` arm.
3. `Types/<Aggregate>SnapshotProjection.cs`: add `public <X>Snapshot Apply(<X>Snapshot current, <NewEvent> e) => current with { <X> = <X>.Advance(current.<X>, <X>Event.FromPayload(e)) };`. If you skip it, the snapshot goes stale silently.
4. `<Domain>Feature.cs`: add `typeof(<NewEvent>)` to `EventTypes`.
5. Golden file: add a `[Fact]` in `buddy.IntegrationTests/EventShapeTests/<Domain>EventShapeTests.cs` using the fixed ids and `FixedInstant`. Run the filter below. The failure prints the exact JSON. Review it, then save it as `EventShapeTests/GoldenFiles/<Domain>/<NewEvent>.json` (extra shapes of one type: `<NewEvent>_<Variant>.json`). `Meta/EventGoldenFileCoverageTests` fails for any type in a feature's `EventTypes` without one. Once a golden file exists, never edit it to make a test pass: a diff there means stored history can no longer be replayed.
6. Extend `SnapshotTests/<Aggregate>SnapshotTests.cs` so its command sequence produces the new event.

## 3. A new aggregate (and maybe a new domain)

Mirror Medicines/MedicineSchedule file for file:

- [ ] `Types/<X>Id.cs`: `public sealed record <X>Id(Guid Value) { public static <X>Id New() => new(Guid.CreateVersion7()); }`
- [ ] `Types/<X>Events.cs`: union + `FromPayload` + `EventType` (step 2).
- [ ] `Types/<X>.cs`: immutable record with non-null `static <X> Start(<X>Event)` / `static <X> Advance(<X>, <X>Event)`, plus `Rehydrate` / `Replay` via `EventReplay` (copy `Features/Pickups/Types/PickupSchedule.cs`). **Don't name the step functions `Apply`/`Create`/`Evolve`**: JasperFx's generator picks those up and the build breaks.
- [ ] `Types/<X>SnapshotProjection.cs`: `public sealed record <X>Snapshot(Guid Id, <X> <X>);` + `sealed class <X>SnapshotProjection : SingleStreamProjection<<X>Snapshot, Guid>` with `Create(<CreatedEvent>)` and one `Apply` per state-changing event. The wrapper exists because Marten can't use a class-based `<X>Id` as a document id.
- [ ] `I<X>EventStore.cs` + `Marten<X>EventStore.cs` (`MartenMedicineEventStore.cs`): `ReadAsync` (`FetchStreamAsync` → `<X>Event.FromPayload(e.Data)`), `FindSnapshotAsync` (`LoadAsync<<X>Snapshot>(id.Value)` → `?.<X>`), `CreateAsync` (`session.StartTrackedStream`, guard that the first event is the created event, store any index document such as `MedicineIndexDocument` in the same session), `AppendAsync` (no-op on an empty list, then `session.AppendTracked`). Call `session.ObserveStream(id.Value, events)` in `ReadAsync` after the fetch. Persist the unwrapped `e.Value`, never the union.
- [ ] `<Domain>Feature.cs`, inside the store's `StoreOptions`:
  - `options.Projections.Register(new <X>SnapshotProjection(), ProjectionLifecycle.Inline);`. Use `Register`, **not** `Projections.Snapshot<T>()`, which throws for these ids.
  - `options.Schema.For<<X>Snapshot>().DatabaseSchemaName("snapshots");`
  - `services.AddSingleton<I<X>EventStore, Marten<X>EventStore>();`
- [ ] JSON: the store's `UseSystemTextJsonForSerialization(enumStorage: EnumStorage.AsString, configure: json => ...)` must add `StronglyTypedIdJsonConverterFactory`. Also add `ValueTupleJsonConverterFactory` if the aggregate holds a tuple-keyed dictionary or tuple set. Without it, tuples serialize to `{}` and the data is lost without any error. A union whose cases serialize identically needs a dedicated converter (`PrintTemplates/Types/PrintTemplateOwnerJsonConverter.cs`). `Program.cs`'s `ConfigureHttpJsonOptions` already registers both factories for HTTP.
- [ ] Reads (`Get*`) may use `FindSnapshotAsync`. Command handlers keep `ReadAsync` + `Rehydrate`.
- [ ] `buddy.IntegrationTests/SnapshotTests/<X>SnapshotTests.cs`: run commands covering every event, then `Assert.Equivalent(<X>.Rehydrate(await store.ReadAsync(id, ct)), await store.FindSnapshotAsync(id, ct), strict: true)`. Use `Equivalent`, not `Equal`, because `ImmutableDictionary` has no structural equality.
- [ ] Production already has streams for this aggregate type? Rebuild the projection before any read depends on it (`docs/backend/analysis/event-stream-snapshots.md` → "Backfilling").

New domain only:

- [ ] `I<Domain>Store : IDocumentStore`.
- [ ] `<Domain>Feature.cs` with `OpenApiDocumentName`, `EventTypes`, `AddMartenStore<I<Domain>Store>` (`DatabaseSchemaName = "<domain>"`, `StreamIdentity.AsGuid`, `AddEventTypes`), plus `Add<Domain>Feature`/`Map<Domain>Feature`.
- [ ] `Program.cs`: add `Add<Domain>Feature(builder.Configuration)` **after** every feature it depends on (Guardians before anything that uses `IGuardianLinkEventStore`), and add `app.Map<Domain>Feature()`.
- [ ] `taskfile.dist.yml`: add the schema to `MARTEN_SCHEMAS`.
- [ ] Create `docs/backend/<domain>/flow.md` and link it in `docs/backend/README.md`.

## 4. Tests: `src/backend/buddy.IntegrationTests/Features/<Domain>/<UseCase>/<UseCase>Tests.cs`

- `[Collection(BuddyApiCollection.Name)] public sealed class <UseCase>Tests(BuddyApiFixture fixture)`. Name methods as sentences (`A_third_party_with_no_guardian_link_gets_not_found`).
- **At least one test per endpoint must carry `[CoversEndpoint("<WithName value>")]`**. Without it, `Meta/EndpointCoverageTests` fails. It also fails on stale names after a rename.
- Arrange through the API: `fixture.CreateAuthenticatedUserAsync()` → `(User, Token, UserId)`; `GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex")`; `GuardianTestHelpers.CompleteChildLoginAsync(fixture, child)` for a child token; `<Domain>TestHelpers.Create...Async(fixture, token, ..., expectedStatus: 200)`. Add new helpers and response DTOs (`internal sealed record ...Dto`, ids as `Guid`) to `Features/<Domain>/<Domain>TestHelpers.cs` / `<Domain>TestDtos.cs`.
- Alba: `fixture.Host.Scenario(_ => { _.WithRequestHeader("Authorization", $"Bearer {token}"); _.Put.Json(body).ToUrl(path).QueryString("date", ...); _.StatusCodeShouldBeOk(); })`, then `response.ReadAsJson<Dto>()`. `ToUrl` takes the path literally, so chain query parameters with `.QueryString(...)`. `_.Get.Url("...?from=..")` accepts an inline query.
- Cover success (assert the body, then read back through a GET where one exists), **and every declared failure status**: 400 (validator rule plus each handler-level check), 403 (e.g. the child on a `Manage` route), 404 (a stranger with no relationship, and an unknown id).
- Store-level asserts: `fixture.Host.Services.GetRequiredService<I<X>EventStore>()`.

Commands, run from the repo root. Integration tests need Docker (Testcontainers starts Postgres, Keycloak and Mailpit, which takes about 30s):

```bash
dotnet build src/backend/backend.slnx
dotnet test src/backend/backend.slnx --filter "FullyQualifiedName~buddy.IntegrationTests.EventShapeTests"          # no containers, ~1s
dotnet test src/backend/backend.slnx --filter "FullyQualifiedName~buddy.IntegrationTests.Features.<Domain>.<UseCase>|FullyQualifiedName~EndpointCoverageTests|FullyQualifiedName~<X>SnapshotTests"
task test:backend                                                                                                 # full suite before finishing
```

## 5. Docs (when behavior or the contract changes)

- `docs/backend/<domain>/flow.md`: add the row to the endpoint table, and extend the sequence diagram if the flow changed.
- Run `task docs:openapi` and commit the regenerated `docs/backend/openapi/buddy.json` and `src/frontend/buddy/src/app/core/api/buddy-api.ts`. `Meta/OpenApiDocumentTests` fails until the contract is regenerated, and frontend CI fails if the generated types are stale. `docs/backend/http-status-codes.md` holds the rules, not per-endpoint tables; change it only when a rule changes.
- `docs/backend/glossary.md` for new domain terms or ids. Update `docs/backend/analysis/<topic>.md` if the change implements or alters a recorded design decision.
- The post-commit doc-sync hook (`.devcontainer/git-hooks`, opt-in via `task hooks:install AGENT=claude`) may also update docs in a follow-up `docs: sync documentation (auto)` commit. It is per-clone and optional, so don't rely on it.

## 6. Finish

- [ ] Slice files exist (Command, Validator if there are input rules, Handler, Endpoint) and the slice is mapped in `<Domain>Feature.cs`
- [ ] Handler follows the order validate → user → authorize → load → state checks → append-if-changed
- [ ] `Results<...>` matches the reachable statuses, and the switch has no `_ =>` arm
- [ ] Event added to the union, `FromPayload`, `EventType`, `Advance` (or `Start`), the snapshot `Apply` and `EventTypes`, with a golden file
- [ ] `[CoversEndpoint]` present, and tests cover success plus every failure status
- [ ] `.http` entry added (domains that have a `.http` file); flow doc, status-code table and glossary updated
- [ ] `dotnet build` is clean, and the EventShapeTests, the slice tests, `EndpointCoverageTests` and the SnapshotTests pass
- [ ] Run the **`backend-aware-review`** skill on the diff and fix its findings. Don't commit unless the user asks.

## Templates (`templates/`)

Skeletons for a child-scoped PATCH on an existing aggregate, derived from `Medicines/UpdateMedicineDetails` and `SetDoseStatus`. They were verified by instantiating them as `Medicines/RenameMedicine` in a scratch copy: the build was clean and the slice tests, EventShapeTests, SnapshotTests and `EndpointCoverageTests` all passed. Copy each file to the path in its first `// ->` line, delete that line, and replace:

| Placeholder | Example |
| --- | --- |
| `{{Domain}}` / `{{UseCase}}` | `Medicines` / `RenameMedicine` |
| `{{Aggregate}}` / `{{AggregateId}}` / `{{EventStore}}` | `MedicineSchedule` / `MedicineId` / `IMedicineEventStore` |
| `{{Authorization}}` / `{{Access}}` / `{{AccessCheck}}` | `MedicineAuthorization` / `MedicineAccess` / `CheckManage` |
| `{{Event}}` | `MedicineRenamed` (add it per step 2) |
| `{{routeGroup}}` / `{{route}}` | `medicines` / `/children/{childId:guid}/schedules/{id:guid}/name`. Rename `id` to match siblings (`medicineId`). |
| `{{Response}}` / `{{ResponseFactory}}` | `MedicineScheduleResponse` / `FromSchedule` |
| tests: `{{CreateAggregateAsync}}`, `{{ResponseDto}}`, `{{testUrl}}`, `{{snake_use_case}}` | `MedicineTestHelpers.CreateMedicineScheduleAsync`, `MedicineScheduleDto`, `/medicines/children/{child.Id}/schedules/{created.Id}/name`, `rename_a_medicine` |

For Calendars/Groups, authorization takes the loaded aggregate (`CalendarAuthorization.CheckView(calendar, userId, groups, guardians, ct)`, `GroupAuthorization.CheckManage(group, userId)`), so load the aggregate before authorizing.
