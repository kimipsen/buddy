# Eliminating avoidable nulls

Status: Phases 0-6 implemented (see each "As built"); Phase 7 proposed.

## Context

Nullable values have accumulated across the backend and the frontend. Most of them don't mean
"absent". They stand in for a missing union case, a back-compat default for data that doesn't
exist, or a state the type system can't see. This document lists every class of nullable, says
which ones are genuinely necessary, and lays out a phased plan to remove the rest.

**Baseline assumption: nothing has ever been deployed.** No database holds events or documents
with missing fields. So every "back-compat" default (`= null` on an event, `IsAllDay = false`,
`string? Icon` "for rows written before ...") can be deleted outright, and event shapes may change
freely. The cost is regenerating golden files
([EventShapeTests/GoldenFiles](../../../src/backend/buddy.IntegrationTests/EventShapeTests/GoldenFiles))
and snapshot tests, not migrating data.

### Where things stand

| Area | Count | Root cause |
|---|---|---|
| Caller id `UserId?` on commands/queries | 127 of 133 `*.Command.cs`/`*.Query.cs`, 112 handler guards, 10 `command.UserId!` | Authenticated but not provisioned, handled four different ways (404, 401, 200 `[]`, silent no-op) |
| Aggregate `Fold(T?, e)` / `Rehydrate` | 30 nullable signatures, ~120 `!` | Every fold starts from `null`, so every case does `x! with {...}` |
| Kind-correlated nullable fields | Pickups (5), `CalendarItem` (4), `CalendarItemOccurrence` (6), `User` email verification (3), `IdempotencyRecord` (3) | A flat record with nullable columns instead of a union |
| Free text (`Description`, `Comment`, `Notes`, `Label`, `AiChatMessage.Text`) | ~8 types | `null` and `""` both mean "none" |
| `Before?` on events | `MealRated`, `MealAssignedToSlot`, `PickupAssigned`, `WorkLocationOverridden` | No reader uses it |
| Back-compat defaults | ~15 | Written for data that doesn't exist |
| Frontend | 298 `\| null`, 158 `signal<T \| null>(null)`, 19 `$any`, 7 `!` | `strict` is **off** in [tsconfig.json](../../../src/frontend/buddy/tsconfig.json), so none of it is checked. 26 components hand-roll loading/error/data triads |

The backend compiler is already strict:
[Directory.Build.props](../../../src/backend/Directory.Build.props) sets `<Nullable>enable</Nullable>`
and `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`, and the source has 0 `#nullable`,
`null!` or `default!`. The frontend is not, but `tsc --noEmit --strict` reports **0 errors** today,
so turning it on costs nothing.

## What stays nullable

**Decision: a value stays nullable only if it is a lookup miss, an external wire shape, or a
domain "absent" value that a union would only make wordier.**

- **Lookups:** every `Find*Async` / `FindSnapshotAsync`, `Rehydrate` of an empty stream,
  `Calendar.FindMatchingToken`, `MealPlan.FindMatchingToken`, `GoalPostResolver.AtThreshold`,
  `WorkPattern.LocationFor`, `WorkLocationSchedule.FindLocation`, the `Resolve*` helpers. These
  are already mapped to `Result<T>.NotFound`. The fix is to stop re-checking them with `!` (Phase 2),
  not to remove `T?`.
- **External wire shapes:** the provider DTOs in
  [Providers/](../../../src/backend/buddy/Features/Mealplans/AiAssistant/Providers) (already
  `JsonIgnoreCondition.WhenWritingNull`), Keycloak token JSON in `auth.service.ts`,
  `HttpResponse.ContentType`, query parameters (`cursor`, `pageSize`), the `Accept-Language` header,
  configuration-binder results, BCL reflection (`StronglyTypedIdJsonConverterFactory`,
  `ValueTupleJsonConverterFactory`).
- **Domain absent values:**
  - `MedicineSchedule.EndDate`: an ongoing medicine. A `DateOnly.MaxValue` sentinel was rejected
    as a magic value.
  - `PickupAssignment.Time`: the slot already says morning or afternoon.
  - `AiProviderCredential.ActiveProvider`: none configured yet.
  - `MealPlanEntry.Rating`: not rated yet.
  - `RecurrenceRuleRequest.Until` and `WorkDay` location on the wire: mapped to unions at the
    endpoint (Phase 5).
  - Pagination cursors (`PreviousCursor`/`NextCursor`): no page in that direction.
  - `MedicineSharing.SharedWithGroupId`: not shared.
- **`Icon?` meaning "inherit"** on `CalendarItem`, `ItemDetails`, `EventItemCreated`,
  `TaskItemCreated`, `Subtask` and the matching commands and requests. **Decision: keep `Icon?`.**
  Null is the real "inherit from the parent/calendar" value
  ([CalendarOccurrenceExpansion.cs](../../../src/backend/buddy/Features/Calendars/CalendarOccurrenceExpansion.cs)).
  An `ItemIcon = Inherit | Own(Icon)` union was considered and rejected: it adds a converter and a
  golden-file churn for no extra safety.
- **Config:** `KeycloakOptions.ValidIssuer`, `MailOptions.FromName`, SMTP `Username`/`Password`.
  The SMTP pair is grouped into one `SmtpCredentials?` in Phase 0, so it can't be half-set.
- **Frontend:**
  - Auth tokens (logged out), `RuntimeConfigService` before load, storage misses, pipe inputs,
    timers.
  - UI "nothing open" ids (`editingX`, `expandedX`, `confirmingX`).
  - The "no error" state, until Phase 7 folds it into `resource()` / `ActionState`.
- **`IdempotencyKeyMiddleware`** keeps its nullable `GetUserId()`. It runs before endpoint filters
  (Phase 1).

## Phases

The phases run in this order. Each phase is its own PR, and each is green on its own:
`task test`, then `backend-aware-review`. Phase 5 is one PR per domain.

1. Phase 0: guardrails
2. Phase 1: non-null caller id
3. Phase 2: aggregate folds without null
4. Phase 3: delete back-compat code
5. Phase 4: free text as `string`
6. Phase 5: unions instead of correlated fields
7. Phase 6: drop `Before?`
8. Phase 7: frontend state

## Phase 0: guardrails

**Decision: turn on the checks first, so that every later removal is compiler-checked.**

### Frontend

1. `"strict": true` in [tsconfig.json](../../../src/frontend/buddy/tsconfig.json). This reports
   0 errors today.
2. `strictTemplates: true`, and replace the 19 `$any(...)`:
   - **`events-list.html` (8):** make `UserEventItem` a discriminated union on `type`, so
     `@switch` narrows `event.data`.
   - **Agenda, home and child-calendar rows (7):** `agenda.html`, `home.html` and
     `child-calendar.html` mix `TaskRun` and `CalendarOccurrence` rows. Add a `kind` tag, or
     pre-split them in a `computed`.
   - **`$any($event.target).value` (4):** in `home.html`, `child-mealplan.html` and
     `manage-tasks.html`. Replace with an `inputValue(event)` helper.
3. `noUncheckedIndexedAccess: true`, and fix its 52 errors. The largest are in `tasks-today.ts`
   (11) and `mealplan.ts` (5).
   - Replace the 7 `.at(-1)!` with a `MAX_STARS` constant and a `lastDay(days)` helper.
4. ESLint: type-checked linting (`parserOptions.projectService`), plus
   `@typescript-eslint/no-non-null-assertion`, `no-unnecessary-condition` and
   `prefer-nullish-coalescing`.

### Backend

5. **Honest request DTOs.** HTTP JSON options get `RespectNullableAnnotations = true` and
   `RespectRequiredConstructorParameters = true`. Deserialization failures map to an
   `ErrorEnvelope` 400 (`validation_error`), so clients see the same body as for a validator
   failure.
   - Today a request's non-nullable `string Name` silently arrives as null when the field is
     omitted.
     [WorkLocationRequest](../../../src/backend/buddy/Features/WorkLocations/AddWorkLocation/AddWorkLocation.Endpoint.cs)
     declares `string?` fields and coalesces them with `?? ""` to cope.
   - Expect churn in the 400-response tests for omitted fields.
6. **Validated options.** Replace each `services.Configure<T>(section)` with
   `AddOptions<T>().BindConfiguration(...).Validate(...).ValidateOnStart()`. Today `required string`
   options are only checked at compile time; the binder can still leave them null.
   - Group `MailOptions.Username`/`Password` into `SmtpCredentials? Credentials`.

### As built

Phase 0 is implemented. Where the build differs from the plan above:

- **`noUncheckedIndexedAccess` and the three ESLint null rules apply to production code only.**
  The flag is set in `tsconfig.app.json`, and the rules are turned off for `*.spec.ts` and `e2e/`.
  Specs index into `mock.calls` and DOM queries (about 190 index errors and 825
  `querySelector(...)!`), where a miss should just fail the test. Forcing them to comply would add
  hundreds of `!` for no safety.
- **Index access** was fixed with small total helpers instead of `!`:
  - `core/array-utils.ts` (`firstAndLast`, `swapped`, `NonEmptyArray`). `TaskRun.subtasks` and
    `TaskRollup.occurrences` are now `NonEmptyArray`.
  - `const [firstChild] = children; if (!firstChild)` narrowing.
  - `as const` tuples for `GROUP_ROLE_NAMES` / `DEFAULT_COLOR_SWATCHES`.
  - `weekName(week)` instead of `WEEK_NAMES[week]`.
  - One shared `parseIsoDate` instead of three copies.
- **`$any`:**
  - The row templates narrow through type-guard methods (`isRun(entry): entry is TaskRun`).
  - Inputs use template reference variables.
  - The events list gets a `TypedUserEvent` union built once at load (`toTypedUserEvent`).
- **Request binding** (`RequestBindingFailureMiddleware`, with `RouteHandlerOptions.ThrowOnBadRequest`):
  - JSON failures name the field path (`days[0].locationId`).
  - Route, query and missing-body failures get a fixed message, so the framework's text, which
    echoes the input, never reaches clients.
  - Every genuinely optional nullable request parameter now has `= null`, because
    `RespectRequiredConstructorParameters` otherwise makes it required.
  - `RespectNullableAnnotations` also applies when writing responses. A non-nullable response
    member that holds null now fails serialization instead of sending `null`. That is intended
    while no stored data predates a field.
- **Options:**
  - `AddValidatedOptions<T>` (`Common/Configuration/ValidatedOptions.cs`) does data annotations
    plus `ValidateOnStart`.
  - Mail credentials moved to `Mail:Credentials:Username/Password`
    (`Mail__Credentials__*` in `.devcontainer/docker-compose.yml` and the Azure deploy).
  - Both values blank means unauthenticated sending.
  - The checked-in `appsettings.json` ships a placeholder connection string, so a missing
    `ConnectionStrings` still only fails at the first database call.

## Phase 1: non-null caller id

**Decision: an authenticated caller without a Buddy user gets `403` with
`ErrorEnvelope("user_not_provisioned", ...)`, decided once in an endpoint filter. Every command and
query then carries a non-null `UserId`.**

Today [Claims.GetUserId](../../../src/backend/buddy/Features/Users/Claims.cs) returns null when
[UserIdClaimsTransformation](../../../src/backend/buddy/Features/Users/UserIdClaimsTransformation.cs)
found no `KeycloakIdentity` document, that is, before the first `GET /users/me`. Handlers react
inconsistently:

| Behavior | Where |
|---|---|
| `Result<T>.NotFound` (404) | Most of the 112 handlers that guard with `if (command.UserId is not { } userId)` |
| 401, via a custom `Unauthenticated` outcome | `CreateCalendar`, `CreateGroup`, `CreateChild` |
| 200 with `[]` | `ListGroups`, `ListCalendars`, `ListMyChildren`, `ListMyGuardians`, `ListMySiblings`, `ListEvents` |
| Silent no-op | `DeleteCurrentUser` |
| `command.UserId!` after a nullable access helper | 10 `*ForGroup` handlers, via `MealplanGroupAccess` / `MedicineGroupAccess` |

`403` was chosen over keeping `404` because the caller is known (the token is valid) and refused
for a reason about themselves, not about a resource. `404` was rejected because it would
contradict [http-status-codes.md](../http-status-codes.md) and collide with the
existence-hiding `NotFound`. `401` was rejected because the token is valid, so a client that
retries or refreshes on 401 would loop.

### Changes

1. Add a `RequireProvisionedUser()` endpoint-filter extension in `Features/Users/`. Apply it on
   every route group that has `.RequireAuthorization()`:
   `UsersFeature`, `GuardiansFeature`, `GroupsFeature`, `CalendarsFeature`, `MedicinesFeature`,
   `MealplansFeature`, `PickupsFeature`, `WorkLocationsFeature`, `ProgressFeature` and
   `TaskLibraryFeature`.
   - Also apply it to `AcceptGroupInvite` and `AcceptGuardianInvite`, which add
     `.RequireAuthorization()` themselves.
   - **Excluded:** `GET /users/me` (`GetOrCreateUser`, which does the provisioning), the anonymous
     invite previews, and both iCal feeds.
2. Add `Claims.GetRequiredUserId(this ClaimsPrincipal)` next to `GetUserId`. It throws
   `InvalidOperationException` and is unreachable behind the filter. Every `FromClaims` uses it,
   and the first record parameter becomes `UserId UserId`. The other names are kept:
   `GuardianId`, `ChildId`, `CallerId`.
3. Delete:
   - the 112 `is not { }` guards and the 10 `command.UserId!`;
   - `UserId?` in [MealplanGroupAccess](../../../src/backend/buddy/Features/Mealplans/MealplanGroupAccess.cs)
     and `MedicineGroupAccess`;
   - the `Unauthenticated` case of `CreateCalendarOutcome`, `CreateGroupOutcome` and
     `CreateChildOutcome`. If one of these unions then matches `Result<T>`, collapse it into
     `Result<T>`, per the reason given in [Result.cs](../../../src/backend/buddy/Common/Result.cs).
4. Keep `IdempotencyKeyMiddleware`'s nullable read, since it runs before the filter.

### Tests and docs

- Update `UpdateNameTests.Returns_not_found_when_the_caller_has_no_buddy_user_yet` and its
  copies in `UpdateTimeZoneTests` and `UpdateLanguageTests` to expect `403` and
  `code == "user_not_provisioned"`.
- Update the Create* 401 tests for the same case.
- Add one test per route group asserting the 403, for example a theory over one route from each
  group.
- Document the code in [http-status-codes.md](../http-status-codes.md) under 403.
- Frontend: confirm that `AccountService`/`auth.guard.ts` provision via `GET /users/me` before any
  routed page loads. The guards already do this, so 403 should be unreachable in the UI. The e2e
  run in this phase verifies it.

### As built (Phase 1)

- **A middleware, not an endpoint filter.** `Features/Users/ProvisionedUserMiddleware.cs` runs
  right after `UseAuthorization()` and before the idempotency middleware.
  - It applies to every endpoint that carries authorization metadata, so the accept-invite
    endpoints outside the route groups are covered, and a new group can't forget it.
  - Public endpoints (no `IAuthorizeData`, or `IAllowAnonymous`) pass through.
  - `GET /users/me` opts out with `.AllowUnprovisionedUser()`.
- **Commands and handlers.**
  - All 135 commands and queries take a non-null caller id via `Claims.GetRequiredUserId()`.
  - The 120 handler guards became plain assignments.
  - `MealplanGroupAccess` / `MedicineGroupAccess` take a non-null `UserId`.
  - `CreateGroupOutcome` is gone: the handler returns `GroupWithMemberDetails`. `CreateCalendarOutcome`
    and `CreateChildOutcome` lost their `Unauthenticated` case.
- **Behavior change.** For an unprovisioned caller, the list endpoints that used to return 200 `[]`,
  the 404s, the Create* 401s and `DELETE /users/me`'s silent no-op are all `403 user_not_provisioned`
  now. The frontend's `authGuard` provisions via `GET /users/me` before any routed page loads.
- **Tests and docs.** `ProvisionedUserTests` covers one route per group, the provisioning
  round-trip, a public endpoint, and a missing token. The `claude-backend` / `backend-feature`
  skills and templates now show the non-null caller id.

## Phase 2: aggregate folds without null

**Decision: each aggregate has a non-null `static T Create(TCreatedEvent)` and a
`static T Apply(T, TEvent)`, and `Rehydrate` is `T?` only for an empty stream.**

All 15 aggregates fold with `events.Aggregate((T?)null, Fold)`, so every case writes `x! with {...}`
(69 times). Projections call `T.Fold(null, created)!` (15 times). Handlers call `Rehydrate(...)!`
right after a create (37 times).

### Changes

- `Rehydrate(events) => events is [var first, ..var rest] ? rest.Aggregate(Create(first), Apply) : null`,
  where `Create` pattern-matches the creation events.
- Projections call `Create(created)` directly.
  - Pick method names that don't collide with Marten's projection conventions. Marten
    source-generates against `Create`/`Apply` on projection types, so the aggregate methods may need
    different names, for example `Start`/`Evolve` (as built: `Start`/`Advance`; see below).
- Handlers that just created a stream build the aggregate from the event they appended, instead of
  `Rehydrate(...)!`.
- Authorization helpers return the aggregate along with the decision.
  `CalendarAuthorization.Check*(Calendar?)` and `GroupAuthorization` today return `Allowed` only
  when the aggregate is non-null, but the compiler can't see that, so callers write
  `calendar!`/`group!` about 12 times. Use `Result<Calendar>`, or a `[NotNullWhen(true)]`
  `TryCheck*(out Calendar)`.
- Add `ChildProgress.Initial(id, childId)` to replace the `current?.X ?? default` chains in
  `RecordStarChange` and `ConfigureGoalPosts`.
- `MedicineSharedWithGroup` currently doubles as the stream-creation event, so `Fold` has to branch
  on `sharing is null`. Add a `MedicineSharingStarted` creation event to remove that branch.

No event shapes change. Snapshot tests should pass unchanged. This is the proof that the refactor
preserved behavior.

### As built (Phase 2)

- **Names: `Start` / `Advance`.** The open question is settled: JasperFx's source generator
  scans `Evolve` (its `AggregateEvolverGenerator`) as well as `Apply`/`Create`, so `Evolve` broke
  the build.
- **Aggregates and projections.**
  - All 16 aggregates have a non-null `Start(event)`, which throws `EventReplay.NotAStartEvent`
    for a non-creation event, and a non-null `Advance(state, event)`.
  - `Rehydrate` / `Replay` delegate to `Common/Aggregates/EventReplay.cs`.
  - The snapshot projections call `Start` / `Advance` directly.
  - The 37 `Rehydrate(...)!` became `Replay(...)`, and the 69 `state!` in folds are gone.
- **No catch-all in `Advance`.** It names the creation events as `=> throw EventReplay.AlreadyStarted`
  instead of using `_ => state`. That surfaced events the old fallthrough silently ignored:
  `Group` (the three invite events) and `MealplanAiSession` (messages and tool invocations).
  They are now explicit `=> state` arms, and a new event without an arm fails the build (CS8509).
- **`MedicineSharingStarted` was not added.** Splitting into `Start`/`Advance` already separates
  "first `MedicineSharedWithGroup`" from "a later re-share", so the extra event wasn't needed.
- **Authorization helpers.** `CalendarAuthorization.Check*` and `GroupAuthorization.Check*` take a
  non-null aggregate, and callers handle a missing one first with the outcome `Check*(null)` used
  to produce: `NotFound` in handlers, `Forbidden` in `CreateCalendar`, the validation message in
  `PrintTemplateReferenceChecks`. That removes the `calendar!`/`group!` uses.
- **Smaller changes.**
  - `ChildProgress.Initial(id, childId)` replaces the `current?.X ?? default` chains and the
    hand-built empty aggregate in `ConfigureGoalPosts`. `ProgressSummary.From` takes a non-null
    aggregate.
  - The five `FindSnapshotAsync(...)!` after an index lookup now throw a named
    `InvalidOperationException` (index without a snapshot is corrupt data).

## Phase 3: delete back-compat code

**Decision: delete every default and fallback whose only justification is pre-existing data.**

| Item | Change |
|---|---|
| `TaskItemCreated.AssignedTo = null`, `TaskTemplateId = null` | Remove the defaults (types are reshaped in Phase 5) |
| `TaskCompletionChanged`, `StarAwarded`, `StarRevoked`, `RecordStarChange` `SubtaskId = null` | Remove the defaults (Phase 5 replaces the field) |
| `RecurrenceRule.Until = null`, `CalendarItem` / `CalendarItemOccurrence` `= null` defaults, `CalendarItemResponse.TaskTemplateId = null` | Remove the defaults |
| `Period.IsAllDay = false`, `DueDate.IsAllDay = false` | Remove the defaults |
| `MealPlan.SharedWithGroupId = null` | Remove the default; the `MealPlanCreated` fold passes `null` explicitly |
| `CalendarMembershipDocument.Icon`, `GroupOwnedCalendarDocument.Icon` (`string?`) | Make them `string`, and delete the `?? Calendar.DefaultIcon.Value` fallbacks in `ListCalendars.Endpoint.cs` and `MartenCalendarEventStore.cs` (2) |
| `GuardianLinkDocument.CreatedAt = null` | Make it a required `DateTimeOffset`, set from `GuardianLinked.OccurredAt`, not `UtcNow`. Delete the "nulls sort last" comment |
| Legacy `CalendarCreated` / `CalendarOwner.User` path ("only for calendars created before this change") | Delete it |
| `CreateCalendar` command `Icon?`, plus the create-time `CalendarIconChanged` | Put `Icon` on `CalendarCreatedForGroup`. The endpoint resolves blank to `Calendar.DefaultIcon`, so the command takes a required `Icon`. Drop the extra event and the `DefaultIcon` fold seed |
| `ResolvedGoalPost.Label`, `Round` | Never read; delete them |
| Frontend `calendars.service.ts` comment saying the backend doesn't serialize `taskTemplateId` | Stale; make it `taskTemplateId: string \| null` (non-optional) |

Golden files affected: `CalendarCreatedForGroup`, `CalendarIconChanged` (create-time case
removed), plus any whose JSON changes because a default disappeared.

### As built (Phase 3)

Everything in the table above is done, plus three changes that follow from it:

- **`GroupCreated` carries all three policies.** `CreateGroup` used to append
  `GroupMealplanPolicyUpdated` and `GroupMedicinePolicyUpdated` right after `GroupCreated`, because
  that event "already shipped" before those policies existed. That is the same back-compat
  reasoning as the create-time `CalendarIconChanged`. Now one `GroupCreated` holds the calendar,
  meal plan and medicine policies.
- **`CalendarOwner` is gone.** Without `CalendarCreated` only the `Group` case was left, so
  `Calendar.Owner` became `GroupId GroupId`.
  - `CalendarOwnerJsonConverter` is deleted.
  - `CalendarCreatedForGroup.OwnerId` is renamed `GroupId`.
  - `CalendarAuthorization` lost the guardian-of-owner branch and its `IGuardianLinkEventStore`
    parameter. 17 calendar handlers dropped the now-unused dependency.
- **`DueDateRequest`.** With `DueDate.IsAllDay` required, the create and reschedule requests take
  `{ date, time }`, which the endpoint maps to `DueDate` using the request's own `isAllDay`. This
  matches what the frontend already sends.

Kept on purpose: the `= null` defaults on request DTOs. Phase 0 added them for optional wire fields.

Golden files: `CalendarCreated.json` is deleted. `CalendarCreatedForGroup.json` (now with `GroupId` and
`Icon`) and `GroupCreated.json` (all three policies) changed. Every other event's JSON is unchanged.

## Phase 4: free text is `string`, `""` means none

**Decision: optional free text is a non-null `string`. Each endpoint trims it and normalizes
null or whitespace to `""` once.**

This applies to:
- `Meal.Description` (`MealCreated`, `MealDetails`, create and update requests)
- `MealRating.Comment` and `MealPlanEntryRating.Comment` (`MealRated`, `RateMealRequest`)
- `MealPlanAssignment.Notes` (`MealAssignedToSlot`, `AssignMealToSlotRequest`)
- `PickupAssignment.Notes`
- `AiSessionStarted.Notes` (`StartAiSessionRequest`)
- `GoalPost.Label`
- `AiChatMessage.Text` and `AiChatCompletionResult.Text`

The codebase already uses `""` to mean "none": child users get `Email.Unverified("")` in
[CreateChild.Handler.cs](../../../src/backend/buddy/Features/Guardians/CreateChild/CreateChild.Handler.cs).

Normalization is **required**, not cosmetic. The idempotency checks in `RateMeal`
(`before.Comment == command.Comment`) and `AssignMealToSlot` (`before.Notes != after.Notes`) would
otherwise treat `null` and `""` as a change.

- `GoalPost.Label` also gets a `MaximumLength` rule. It has none today.
- The AI provider adapters stop converting `""` to `null` (Anthropic, OpenAI, Gemini). After that,
  `SendAiSessionMessage.Handler` can start `finalText` as `""`.
- Frontend: drop `| null` from the matching fields in `mealplans.service.ts`,
  `pickups.service.ts`, `ai-assistant.service.ts` and `progress.service.ts`. Delete the
  `|| null` / `?? null` payload conversions. 17 sites use `|| null`; only the icon, `until`,
  `endDate` and `assignedTo` sites stay.

Rejected: keeping `string?` and normalizing `""` to `null`. That keeps the nullable for no domain
reason, and the frontend already converts with `?? ''` when displaying.

### As built (Phase 4)

- **One normalizer: `Common/FreeText.Normalize`.** It turns a value into `value?.Trim() ?? ""`.
  Every endpoint that takes one of these fields applies it once:
  - CreateMeal, CreateMealForGroup, UpdateMealDetails, UpdateMealDetailsForGroup
  - RateMeal
  - AssignMealToSlot, AssignMealToSlotForGroup
  - AssignPickup
  - StartAiSession
  - ConfigureGoalPosts
- **Request fields stay optional.** Request DTOs keep `string? X = null`, because the field may be
  omitted. Commands, domain records, events and responses are non-null `string`.
- **AI text is non-null too.** The three provider adapters return `""` instead of `null` for a
  completion with no text. The tool loop starts `finalText` at `""`. The provider wire DTOs stay
  nullable, since that's an external shape.
- **Label length rule.** `GoalPost.Label` gets `MaximumLength(100)`.
- **Frontend.**
  - The matching fields in `mealplans.service.ts`, `pickups.service.ts`, `ai-assistant.service.ts`
    and `progress.service.ts` are `string`, and the requests send `''` rather than `null`.
  - `rateMeal`/`assignMealToSlot` take the text as a required argument. The meal grid and
    drag-and-drop send `''`, which matches the old "no notes" behavior.
  - Six `|| null` conversions and one `?? ''` are gone. The two `rate()` calls now fall back to
    `?? ''` instead of `?? null`, because `rating` itself is still optional.
  - The remaining `|| null` sites (icon, `until`, `endDate`, `assignedTo`, the playdate fields) are
    genuinely optional or belong to Phase 5.
- **Golden files.** `"Notes": null` and `"Label": null` became `""` in four pickup files and in
  `GoalPostsConfigured`.
- **The meal-plan iCal feed** still omits `DESCRIPTION` for a slot without notes (`""` maps to `null`
  for Ical.Net).
- **Existing databases need a reset.** Marten loads a stored `"Notes": null` into the non-null
  property. The HTTP serializer (`RespectNullableAnnotations`) then refuses to write it, so the
  request returns 500. That fits this plan's baseline (no data predates these fields). A database
  holding rows from before Phases 3-4 has to be recreated, because it isn't migrated.

## Phase 5: unions instead of correlated nullable fields

**Decision: when a nullable field is only meaningful for one "kind", model the kinds as a closed
hierarchy with an explicit discriminator.**

- Use `[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]` plus `[JsonDerivedType]`. This
  works for both Marten and HTTP, since both use System.Text.Json. A hand-written converter like
  [PrintTemplateOwnerJsonConverter](../../../src/backend/buddy/Features/PrintTemplates/Types/PrintTemplateOwnerJsonConverter.cs)
  is the fallback.
- Don't rely on shape-based inference of a union. The comment in `PickupAssigneeKind.cs` rejected
  unions because of that ambiguity; an explicit discriminator removes it.
- On the frontend, each one becomes a TypeScript discriminated union on `kind`.

The work is ordered by value, one PR per domain.

### 5.1 Pickups: `PickupAssignee`

```
PickupAssignee = Guardian(UserId GuardianId)
               | SelfEscort
               | Sibling(UserId SiblingChildId)
               | Playdate(string HostName, string Location, string ContactInfo)   // "" = not given
PickupAssignment(PickupAssignee Assignee, TimeOnly? Time, UserId AssignedBy, string Notes)
```

- This replaces the 5 correlated nullables, which today are enforced only by the validator plus
  `!` in `AssignPickup.Handler`. The change carries through the event, snapshot, command, request
  and `PickupOccurrence`.
- `PickupAssigneeKind` survives only as the wire discriminator.
- Blast radius:
  - backend: the 4 `PickupAssigned_*` golden files, `PickupCleared`, `PickupScheduleSnapshotTests`;
  - frontend: `pickups.service.ts` and `pickup-cell.ts`, which loses its 8 `?? ''` and
    3 `|| null`.

**As built (5.1):**
- **Domain.** `PickupAssignee` is a C# `union` of `Guardian(UserId)`, `SelfEscort`,
  `Sibling(UserId)` and `Playdate(HostName, Location, ContactInfo)`. Marten reads and writes it
  through `PickupAssigneeJsonConverter`, with an explicit `Kind`. `PickupAssignment` is
  `(Assignee, Time?, AssignedBy, Notes)`.
- **Wire.** `PickupAssigneeDto` is an object with a numeric `kind`, read by its own converter
  instead of `[JsonPolymorphic]`. With an abstract base, a missing `kind` throws
  `NotSupportedException`, which surfaces as a 500 rather than a 400. A missing or unknown `kind` is
  a 400, and `kind` may come anywhere in the object. Field errors keep the outer path
  (`assignee.guardianId`).
- **Handler and validator.** The relationship check is an exhaustive `switch` over the union.
  The validator only checks the playdate host name and the free-text lengths.
- **Frontend.** `PickupAssignee` is a TypeScript discriminated union. The components' kind
  constants are literal-typed (`0 satisfies PickupAssigneeKind`) so narrowing works, and
  `playdateHostName(assignee)` serves the templates.
- **Known gaps.**
  - The OpenAPI document shows `assignee` as an empty schema, because a custom converter hides the
    cases from the schema generator. A schema transformer could restore it.
  - Pickup events stored in the old flat shape load as an empty assignee and then return 500.
    Like Phases 3-4, a database from before this change needs a reset.

### 5.2 Calendar item schedule

```
ItemSchedule = Event(Period Period)
             | Task(DueDate DueDate, UserId? AssignedTo, TaskSource Source)
TaskSource   = Freeform | FromTemplate(Guid TaskTemplateId)
```

- `CalendarItem` replaces `Kind`, `Period?`, `DueDate?`, `AssignedTo?` and `TaskTemplateId?`
  with `ItemSchedule Schedule`. `Kind` remains a computed property, because the wire sends
  `kind: 0|1`.
  - `AssignedTo` stays nullable inside `Task`, because "unassigned" is a real state.
- `CreateItemRequest` and `RescheduleItemRequest` take a `schedule` object with an explicit `kind`.
  That removes the four `NotNull` rules in `CreateItem.Validator`, the `!` in `CreateItem.Handler`,
  `RescheduleItem.Handler` and `CalendarItem.ScheduleKey`, and the manual "exactly one pair" check
  in `RescheduleItem.Handler`.
- A template-scheduled task gets a separate `TemplateTaskItemCreated(..., Guid TaskTemplateId)`
  event, mirroring the existing Event/Task split. Plain `TaskItemCreated` loses `TaskTemplateId`.

**As built (5.2):**
- **Domain.** `CalendarItem` holds `ItemSchedule Schedule`: `Event(Period)` or
  `Task(DueDate, UserId? AssignedTo, TaskSource)`, where `TaskSource` is `Freeform` or
  `FromTemplate(Guid)`. `Kind` and `ScheduleKey` are computed (`[JsonIgnore]`).
  `ItemScheduleJsonConverter` persists the union in the snapshot only; the events stay
  union-free.
- **Events.** `TemplateTaskItemCreated` is the new creation event for a template-scheduled task.
  `TaskItemCreated` lost `TaskTemplateId` (golden file `TaskItemCreated_FromTemplate.json` became
  `TemplateTaskItemCreated.json`).
- **Commands.** They take small unions: `NewItemSchedule` (create: event span, or task due date
  plus assignee) and `ItemTiming` (reschedule). The four "this kind needs that field" rules are
  gone from `CreateItemValidator`; it keeps the end-after-start rule (reported under `Schedule`),
  the title length and the recurrence interval. `RescheduleItem` switches over `(item.Schedule, command.Timing)`, so a
  mismatched kind is a 400.
- **Wire.**
  - `schedule` objects discriminated by numeric `kind`. Requests use `ItemScheduleRequest` and
    `ItemTimingRequest`; the response uses `ItemScheduleResponse`, with a nested `source`
    `{ kind: 0 } | { kind: 1, taskTemplateId }`.
  - All of them use the shared `Serialization/KindDiscriminatedJsonConverter`, which pickups now
    use too. It binds each case without `kind`, so a case type can opt into
    `[JsonUnmappedMemberHandling(Disallow)]`. Three request cases do: both event cases and the task
    reschedule. So an event with `assignedTo`, or a task reschedule with `assignedTo`, is a 400.
    The task create case doesn't opt in, so a stray `startsAt` there is ignored, as before. Such
    a rejection reports the `schedule` path with a generic message, not the offending field.
- **Frontend.** `ItemTiming` / `ItemSchedule` TypeScript unions in `calendars.service.ts` and the
  agenda's create/reschedule builders. Nothing reads the item response's schedule today.
- **Existing databases need a reset.** A `TaskItemCreated` stored before this change with a
  `TaskTemplateId` would now load as a hand-entered task, silently losing its subtasks. Unlike a
  snapshot, a projection rebuild doesn't fix this. As with Phases 3-5.1, nothing has been
  deployed, so a reset is the remedy rather than an upcaster.

### 5.3 `CompletionTarget`, shared by Calendars and Progress

```
CompletionTarget = WholeTask | Subtask(Guid SubtaskId)
CompletionKey(DateOnly Date, CompletionTarget Target)
OccurrenceKey(CalendarItemId ItemId, DateOnly Date, CompletionTarget Target)
```

- Replaces `Guid? SubtaskId` on `TaskCompletionChanged`, `StarAwarded`, `StarRevoked` and
  `RecordStarChange`.
- `CalendarItem.CompletionLog` becomes `ImmutableHashSet<CompletionKey>`. Today its values are
  always `true`, because false entries are removed, so it is a set in disguise.
- `ChildProgress.AwardedOccurrences` becomes `ImmutableHashSet<OccurrenceKey>`.
- Both `ValueTupleJsonConverterFactory` registrations (`CalendarsFeature`, `ProgressFeature`)
  disappear. Delete the factory if nothing else uses it.
- Split the SetTaskCompletion route into `PATCH .../items/{itemId}/completion` and
  `PATCH .../items/{itemId}/subtasks/{subtaskId}/completion`. That removes the two-way
  "template ⇔ subtask id" check in `SetTaskCompletion.Handler` and its `Result<CalendarItem>?`
  sentinel.

#### As built (5.3)

- **Types.** `CompletionTarget` and `CompletionKey` live in `Calendars/Types/CompletionTarget.cs`;
  `OccurrenceKey` in `Progress/Types/OccurrenceKey.cs`. Field names are `OccurrenceDate`, matching
  the events. `CompletionTargetJsonConverter` persists the union as `{"Kind":"WholeTask"}` or
  `{"Kind":"Subtask","SubtaskId":...}`, in both the events and the snapshots.
- **Converters.** `CalendarsFeature` and `ProgressFeature` register `CompletionTargetJsonConverter`
  instead of `ValueTupleJsonConverterFactory`. The factory stays: Medicines, Mealplans, Pickups
  and the HTTP options still use it.
- **Route split.** `SetTaskCompletion` (whole task) and `SetSubtaskCompletion` share one handler;
  the body is `{ date, isCompleted }`. The handler switches over `(task.Source, command.Target)`:
  a template task as a whole, or a subtask of a plain task, is a 400; an unknown subtask is a 404.
- **Frontend.** `CalendarsService.setTaskCompletion` keeps its signature and picks the route from
  `subtaskId`.
- **Existing databases need a reset.** Stored `TaskCompletionChanged`, `StarAwarded` and
  `StarRevoked` events carry `SubtaskId`, not `Target`, and no longer deserialize.

### 5.4 `CalendarItemOccurrence`

```
OccurrenceTiming = Timed(DateTimeOffset StartsAt, DateTimeOffset EndsAt) | Due(DateTimeOffset DueAt)
CalendarItemOccurrence(..., OccurrenceTiming Timing, DateTimeOffset SortAt, Routine? Routine, ...)
Routine(Guid SubtaskId, string ParentTitle, string ParentIcon)
```

- This occurrence is never persisted, so no golden files change.
- It removes the `!` in `IcalFeedWriter` and the `startsAt ?? dueAt ?? ''` chains in `agenda.ts`,
  `child-calendar.ts`, `tasks-today.ts`, `events-today.ts` and `task-run.ts`.
- `Routine?` stays nullable, because "not a routine subtask" is the normal case.
- Check while doing this: for subtask occurrences, `IconOverride` carries the *parent's* override.

#### As built (5.4)

- **Shape.** `OccurrenceTiming` is an abstract record pair (`Timed`, `Due`) with
  `OccurrenceTimingJsonConverter` (`KindDiscriminatedJsonConverter`): wire `{ kind: 0, startsAt,
  endsAt }` or `{ kind: 1, dueAt }`. It is a DTO-style hierarchy rather than a C# union because the
  occurrence is a response type, never persisted. `SortAt` is a computed property, serialized as
  `sortAt`. `Routine(SubtaskId, ParentTitle, ParentIcon)` replaces the three nullable fields.
- **Behaviour change.** A routine subtask used to carry `StartsAt`, `EndsAt` *and* `DueAt` (= its
  start). It is now `Timed` only. The frontend reads `sortAt` where it read that `dueAt`
  (tasks-today overdue rollup, child home task order), so it orders and flags overdue the same way.
- **Checked.** For a subtask occurrence, `IconOverride` is indeed the parent item's override; the
  record's comment now says so.
- **Consumers.** `IcalFeedWriter` switches on `(Kind, Timing)`: an event must be timed; any task
  becomes a VTODO due at `SortAt`. The meal plan assistant and the expansion sort use `SortAt`.
  Frontend: `startsAt ?? dueAt ?? ''` chains, the `Date | null` `instantFor` helpers and their
  dead null branches are gone; templates switch on `timing.kind`.
- **Specs.** `src/testing/occurrence-fixture.ts` (`nestOccurrence`) lets spec fixtures keep the flat
  `startsAt`/`dueAt`/`subtaskId`/`parentTitle` fields and nests them the way the backend does.
  It throws on a routine subtask without a `startsAt`, so subtask fixtures are timed as in
  production. Tests of impossible states (an occurrence with no instant at all) were deleted, and
  the subtask rows' unreachable due-time template branches removed. `GetIcalFeedTests` now covers
  the VTODO arm too.
- **Noticed, not changed.** The guardian `events-today` widget shows an event's start time even
  when it is all-day (an all-day event's `startsAt` is local midnight); the child home doesn't. That was already the behaviour,
  since real all-day events always had a `startsAt`; the deleted "no startsAt" test only made it
  look covered.

### 5.5 Recurrence

```
Recurrence     = OneOff | Repeating(RecurrenceFrequency Frequency, int IntervalCount, RecurrenceEnd End)
RecurrenceEnd  = Never | On(DateOnly Until)
```

- `CalendarItem.Recurrence`, `EventItemCreated`/`TaskItemCreated.Recurrence` and both sides of
  `RecurrenceUpdated` become non-null. `RecurrenceExpansion` becomes an exhaustive switch.
- The wire format keeps `recurrence: null` and `until: null` and maps at the endpoint, so no
  frontend change is needed.
- Add the missing validator rule: `Until` must be on or after the seed date.

#### As built (5.5)

- **Types.** `RecurrenceRule.cs` became `Types/Recurrence.cs` (`Recurrence`, `RecurrenceEnd`).
  `RecurrenceJsonConverter` persists `{"Kind":"OneOff"}` or `{"Kind":"Repeating","Frequency",
  "IntervalCount","End":{"Kind":"Never"}|{"Kind":"On","Until"}}` in the events and snapshot,
  registered in `CalendarsFeature` and `EventShapeTestSupport`. Six golden files changed, plus a new
  `RecurrenceUpdated_ToAnEndDate.json` with a read-back test.
- **Wire unchanged.** `RecurrenceRuleRequest.ToRecurrence` / `.From` map at the endpoints, and
  `CalendarItemResponse.Recurrence` is still `null` for a one-off item. No frontend change.
- **Rules.** `RecurrenceRules.Problems(recurrence, seed)` holds the interval rule and the new
  `Until >= seed` rule (`Recurrence.Until`), shared by `CreateItemValidator` (seed: the event's
  start date or the due date), `ScheduleTaskFromTemplateValidator` (the start date) and
  `UpdateItemRecurrenceHandler`, which checks `Until` against `CalendarItem.SeedDate` once the item
  is loaded (its validator only checks the interval).
- **Equality.** Unions have no `==`; `UpdateItemRecurrenceHandler`'s no-op check uses `Equals`
  (structural, nested unions included) and runs before the `Until` rule, so re-sending the stored
  recurrence stays a 200. `CalendarItemSnapshotTests` round-trips a repeating recurrence.
- **Not covered.** `RescheduleItem` can still move an item's seed past its `Until`; the item then
  has no occurrences. Rejecting that is a behaviour change left for a decision.
- **Existing databases need a reset**: stored `Recurrence` values are the old flat shape.

### 5.6 User

- `UserCreated` carries `TimeZoneId` and `Language`:
  - adults get UTC, and `Language` detected from `Accept-Language` in `GetOrCreateUser`;
  - children inherit both from the creating guardian in `CreateChild`.
- Then `User.TimeZoneId` and `User.Language` become required, and `ResolvedTimeZoneId` /
  `ResolvedLanguage` go away (about 8 call sites).
- The extra `LanguageUpdated` append in `GetOrCreateUser.Handler` goes away too.
- `EmailVerification = None | Pending(string TokenHash, DateTimeOffset RequestedAt, DateTimeOffset ExpiresAt)`
  replaces the three nullable fields, which are always set and cleared together.
- `UserName` becomes `string`. `FromClaims` falls back to the Keycloak subject.
- Blast radius: `UserCreated.json`, `UserSnapshotTests`, and the VerifyEmail, UpdateEmail and
  ResendEmailVerification handlers and tests. On the frontend, `userName: string` in
  `users.service.ts` and `user-event.model.ts`.

#### As built (5.6)

- **UserCreated** gained `TimeZoneId` and `Language` (before `OccurredAt`); `UserName` is `string`.
  `GetOrCreateUser.FromClaims` falls back to the subject. `TimeZoneId.Utc` is the adult default.
  `CreateChildHandler` loads the guardian's snapshot (guaranteed by `ProvisionedUserMiddleware`) for
  the child's starting values. The extra `LanguageUpdated` on first sign-in is gone.
- **User** is now positional with no defaults: `TimeZoneId`, `Language` and
  `EmailVerification` are required; `ResolvedTimeZoneId` / `ResolvedLanguage` are gone (8 call
  sites now read the fields).
- **EmailVerification** (`Types/EmailVerification.cs`) persists in the snapshot through
  `EmailVerificationJsonConverter`, registered in `UsersFeature` and `EventShapeTestSupport`. The
  events are unchanged: `EmailVerificationRequested` still carries the hash and expiry.
  `ResendCooldown.IsActive` keeps its shared `DateTimeOffset?` parameter (invites use it too);
  the resend handler passes `Pending.RequestedAt` or null.
- **Tests.** `UserCreated.json` gained the two fields; new tests cover a new user's UTC/browser
  language on `UserCreated` with no follow-up event, a child inheriting the guardian's values, and
  the snapshot round-tripping a `Pending` verification. The subject fallback isn't covered: the
  test Keycloak always issues `preferred_username`.
- **Frontend.** `userName: string` in `users.service.ts` and `user-event.model.ts`, which also
  gained `timeZoneId` and `language` on `UserCreatedData`.
- **Existing databases need a reset**: stored `UserCreated` events lack the new fields.
- **Noticed, not changed.** My profile's time zone `<select>` lists `Intl.supportedValuesOf('timeZone')`,
  which has no `"UTC"`, so a user still on the default sees no matching option (unchanged from the
  old implicit default). `e2e/profile-update.spec.ts` restores carol to her original zone and so
  fails on a freshly reset database, where that is `"UTC"`; it passes once she has a listed zone.

### 5.7 WorkLocations

```
WorkDayOverride = AtLocation(WorkLocationId LocationId) | DayOff
WorkDayStatus   = Unplanned | Off | AtLocation(WorkLocationSummary Location, WorkDaySource Source)
```

- Today, `null` on `WorkDayOverride.LocationId` means "day off", while a missing override means
  "follow the pattern". `WorkDay`'s `Location == null` covers three different cases.
- The wire format for `SetWorkLocationOverrides` keeps `locationId: null` as "off", mapped at the
  endpoint. The `WorkDay` response becomes the `WorkDayStatus` union.
- With Phase 0.5 in place, `WorkLocationRequest` fields and `ReplaceWorkPatternRequest.Days` become
  non-null.
- Blast radius: `WorkLocationOverridden_*`, `WorkLocationOverrideCleared`,
  `WorkLocationScheduleSnapshotTests`, `work-locations.service.ts`.

#### As built (5.7)

- **WorkDayOverride** is a C# union persisted through `WorkDayOverrideJsonConverter`
  (`{"Kind":"AtLocation","LocationId"}` / `{"Kind":"DayOff"}`), registered in
  `WorkLocationsFeature` and `EventShapeTestSupport`. The three override golden files changed, and
  `WorkLocationOverridden_Off` has a read-back test. `WorkLocationOverridden.Before` stays
  `WorkDayOverride?` until Phase 6.
- **Unions are value types.** `SetWorkLocationOverridesHandler` reads the existing override with
  `TryGetValue` into a `WorkDayOverride?`, because `GetValueOrDefault` would hand back a default union
  instead of null.
- **WorkDayStatus** is a response-only DTO hierarchy (`KindDiscriminatedJsonConverter`, like
  `OccurrenceTiming` in 5.4): `{kind:0}` unplanned, `{kind:1}` off, `{kind:2, location, source}`.
  `WorkDaySource` lost `None` and is now `Pattern = 0, Override = 1`. A referenced location missing
  from the schedule throws, since locations are archived, never removed.
- **Wire request unchanged**: `SetWorkLocationOverridesRequest.LocationId: null` still means off.
  `WorkLocationRequest` and `ReplaceWorkPatternRequest.Days` were already non-null.
- **Frontend.** `WorkDay.status` union plus `workDayLocation` / `isWorkDayOverride` helpers in
  `work-locations.service.ts`, used by the overrides page and the week-plan print.
- **Existing databases need a reset**: stored overrides are the old `{"LocationId": ...}` shape.

### 5.8 Smaller unions

| Item | Change |
|---|---|
| `ActiveProviderChanged(AiProvider? Provider)` | Split into `ActiveProviderChanged(AiProvider)` + `ActiveProviderCleared`. Removes the `DateTimeOffset? ActivatedAt` handling in `MealFamilyResolution`. Golden file `ActiveProviderChanged_ToNone` becomes `ActiveProviderCleared` |
| `TestProviderConnectionResult(bool, string?)` | `Succeeded \| Failed(string Message)` |
| `TestProviderConnectionRequest.ApiKey` | Stays nullable on the wire (absent = use the stored key) |
| `IdempotencyRecord` `ResponseStatusCode?`/`ResponseContentType?`/`ResponseBody?` | `CompletedResponse? Response(int StatusCode, string? ContentType, byte[] Body)`. Null only while in progress. `ContentType` stays nullable because a 204 has none. Delete the `?? 200` fallback |
| `SharedGroupResponse` / `SharedMedicineGroupResponse(Guid?, string?)`, `Result<Shared…?>` | `200 {groupId, groupName}` or `204`. Not 404, which already means "child not found or forbidden". The handlers return a `NotShared \| Shared(...)` union |
| `UpdateForChildAsync` / `RescheduleForChildAsync` returning `Task<MedicineSchedule?>` | Return `Result<MedicineSchedule>`, like the sibling helpers |
| `ListEvents` `long? AfterVersion` / `BeforeVersion` | One `DecodedCursor Position` |
| `AiSessionToolExecutor.ExecutionOutcome.DraftEvent?` | `IReadOnlyList<MealplanAiSessionEvent> DraftEvents` |
| `ProgressSummary.CurrentIcon?` | Non-null `DisplayIcon` computed on the server. Removes `currentIcon ?? nextGoalIcon` in `progress-badge.ts` and `children-overview.ts` |
| `AssignPickup.Handler.ValidateRelationshipAsync` returning `Task<string?>` | Return `ValidationProblem?`, the house idiom |
| `StreamVersionTracker.BeginScope()` returning `IDisposable?` | Return a no-op disposable singleton |
| `AiSessionHistoryBuilder` `List<>?` lazy init | `[]` |
| Subtask endpoints: `request.Icon is null ? null : new Icon(...)` | Normalize blank to inherit, as Calendars does. `""` currently becomes `Icon("")`. Also add a blank-icon rule to the TaskTemplate validators |

#### As built (5.8)

Committed per group.

- **Common.** `IdempotencyRecord` lost `Status` and its three nullable response fields for one
  `CompletedResponse? Response(StatusCode, ContentType?, Body)`: null means in progress, so the
  status can't disagree with the response. The cleanup query filters on `Response == null` (new
  test). The replay's `?? 200` is gone. `StreamVersionTracker.BeginScope()` returns a no-op
  singleton for a nested scope instead of null.
- **Mealplans AI.** `ActiveProviderChanged(AiProvider)` plus a new `ActiveProviderCleared`
  (golden file `ActiveProviderChanged_ToNone` became `ActiveProviderCleared`). `MealFamilyResolution`
  keeps a `DateTimeOffset?` "activated at" -- no active provider is a real state there -- but derives
  it from the two events. `TestProviderConnectionResult` is `Succeeded | Failed(Message)`, wire
  `{kind:0}` / `{kind:1, message}`; being a top-level response it uses `[JsonPolymorphic]` (minimal
  APIs serialize a top-level value by runtime type, which skips a base-type converter). The frontend
  keeps a local `unreachable` state for a test request that itself failed. `ExecutionOutcome` carries
  `DraftEvents` (a list), and `AiSessionHistoryBuilder` drops its lazy `List<>?`.
- **Sharing reads.** `GetSharedGroup` (meal plan) and `GetSharedMedicineGroup` return
  `MealplanGroupShare` / `MedicineGroupShare` (`NotShared | Shared(Id, Name)`); the endpoints answer
  200 `{groupId, groupName}` (both non-null) or 204. The frontend services return the body or null
  for the 204. `UpdateMedicineDetailsHandler.UpdateForChildAsync` and
  `RescheduleMedicineHandler.RescheduleForChildAsync` return `Result<MedicineSchedule>`.
- **Users.** `EventsPageRequest` carries one `DecodedCursor Position` (made public, with
  `CursorDirection`) instead of `long? AfterVersion` / `BeforeVersion`; no cursor is `After 0`.
- **Progress.** `ProgressSummary.CurrentIcon?` became a non-null `DisplayIcon` (the reached goal
  post's icon, else the next one's). `progress-badge.ts` takes a required `displayIcon`, and
  `children-overview.ts` reads it directly.
- **Pickups.** `AssignPickupHandler.ValidateRelationshipAsync` returns `ValidationProblem?`, the
  same shape as `ValidateCommandAsync`.
- **TaskLibrary.** `AddSubtask` / `UpdateSubtask` map a blank icon to "inherit" (null), as the
  Calendars endpoints do, instead of `Icon("")`. Both TaskTemplate validators reject a blank
  template icon (`Icon.Value`, matching `UpdateCalendarIconValidator`).
- **Existing databases need a reset**: stored `ActiveProviderChanged` events with a null
  `Provider` no longer read. Old-shape idempotency rows read as in progress (a retry gets a 409
  instead of a replay) until the cleanup sweeps them, within about 20 minutes.

## Phase 6: drop `Before?` from events

**Decision: drop `Before` from `MealRated`, `MealAssignedToSlot`, `PickupAssigned` and
`WorkLocationOverridden`.**

A search for `.Before` in these features finds no reader. The prior state is already in the stream,
so any audit view can rebuild it by folding.

Splitting into first-set and changed events (`MealRated` + `MealRatingChanged`, and so on) was
considered and rejected. It doubles the event types and fold cases to keep data nobody reads.

Not in scope: events whose `Before` is non-null and *is* part of the event's meaning
(`TaskCompletionChanged`, `TimeZoneUpdated`, `LanguageUpdated`). `RecurrenceUpdated` becomes
non-null on both sides in Phase 5.5.

Golden files: `MealRated`, `MealAssignedToSlot`, `PickupAssigned_*`, `WorkLocationOverridden_*`.

### As built (Phase 6)

- The four events dropped `Before`, and `After` was renamed for what it holds: `MealRated.Rating`,
  `MealAssignedToSlot.Assignment`, `PickupAssigned.Assignment`, `WorkLocationOverridden.Override`.
  The handlers still compare against the current value to skip no-op appends; that value now
  comes only from the folded aggregate. `SetWorkLocationOverridesHandler` no longer needs a
  nullable `WorkDayOverride?` for it.
- Six golden files changed. The analysis docs (`mealplans.md`, `pickup-schedules.md`,
  `work-locations.md`) show the new shapes.
- **Existing databases need a reset**: stored events carry `Before`/`After`, not the new names.

## Phase 7: frontend state

**Decision: loads use Angular's built-in `resource()`; mutations use a small shared
`ActionState` helper.**

No helper exists today. 26 components hand-roll `loading = signal(true)`,
`error = signal<string | null>(null)`, a data signal, and a try/catch/finally. `work-locations.ts`
even forgets to reset `loading` on retry.

1. **Loads → `resource({ params, loader })`.** The services already return Promises, so the loaders
   are one-liners. This covers about 26 load triads and about 20 data holders, and removes roughly
   46 `| null` and 26 `loading` booleans.
   - For example, `my-profile.ts` becomes `resource(ensureCurrentUser)` plus `linkedSignal` form
     fields, which drops its 3 nulls and the 6 "overwritten before render" `''` placeholders.
   - Invite pages lose their template `?? ''`, because the value only exists in the loaded branch.
2. **Mutations → `createAction()` / `ActionState<Id>`** in `src/app/shared`:
   ```ts
   type ActionState<Id> = { status: 'idle' } | { status: 'busy'; id: Id } | { status: 'error'; id: Id; key: TranslationKey };
   ```
   This merges the 32 busy-id signals with their paired error signals, across about 15 components.
   The heaviest users are `manage-groups`, `manage-children`, `manage-calendars`, `manage-tasks`,
   `agenda` and `ai-provider-settings`.
3. Smaller frontend fixes:
   - `selectedChildId` becomes `linkedSignal(() => children()[0]?.id)` inside the loaded branch, in
     `manage-pickups`, `manage-medicines`, `manage-tasks` and `manage-progress-goals`.
   - Split pairs `sharedGroupId` + `sharedGroupName` become one `signal<SharedGroup | null>`, in
     `mealplan.ts` and `manage-medicines.ts`.
   - `Record<string, string | null>` errors become delete-the-key, in `manage-children.ts`.
   - Private `childId: string | null` fields that are set once on init move into the loaded state.
   - Inputs:
     - `stepper` `max` defaults to `Infinity`;
     - `loading-spinner` `label` defaults to `''`;
     - `progress-badge` `currentIcon` disappears after 5.8.
   - `agenda`'s `newRepeat: RecurrenceFrequency | null` gets a `'none'` member.
   - Add runtime guards `isTokenSet()` (token-storage) and `isRuntimeConfig()` for the unvalidated
     `JSON.parse(...) as T` casts.

## Testing

- **Phase 0:**
  - backend: tests for omitted fields expect `ErrorEnvelope` 400;
  - frontend: `npx tsc --noEmit -p tsconfig.app.json` and `-p tsconfig.spec.json` pass under the
    new flags.
- **Phase 1:**
  - 403 `user_not_provisioned` per route group;
  - the three "no buddy user yet" tests and the Create* unauthenticated tests are updated;
  - e2e confirms the UI never sees the 403.
- **Phase 2:** snapshot tests unchanged. Equal output before and after is the acceptance check.
- **Phases 3-6:**
  - regenerate golden files and review each diff by hand;
  - snapshot tests for every reshaped aggregate;
  - Alba tests for the new request shapes. This includes a missing-or-wrong `kind` discriminator
    returning 400, and the split SetTaskCompletion routes.
- **Phase 7:** component specs switch from asserting on the `loading`/`error` signals to asserting
  on rendered states. Stubbed services stay as they are.
- Every phase: `task test`, `task test:e2e` for phases that change the wire, and
  `node .claude/skills/i18n/check-parity.mjs` if any translation key changes.

## Docs to update as phases land

- [http-status-codes.md](../http-status-codes.md): `403 user_not_provisioned` (Phase 1); the
  shared-group `204` (5.8).
- [event-stream-snapshots.md](event-stream-snapshots.md),
  [aggregate-roots.md](aggregate-roots.md), [domain-model-diagram.md](domain-model-diagram.md):
  aggregate shapes (Phases 2, 5).
- [glossary.md](../glossary.md): `ItemSchedule`, `CompletionTarget`, `PickupAssignee`,
  `WorkDayStatus`.
- Feature flow docs under `docs/backend/<feature>/flow.md` that describe the reshaped events.
- [pickup-schedules.md](pickup-schedules.md) and [work-locations.md](work-locations.md), where they
  describe the old nullable shapes.

## Decisions made

| Question | Decision |
|---|---|
| Status for an authenticated caller with no Buddy user | `403` + `ErrorEnvelope("user_not_provisioned")` from one endpoint filter; tests updated |
| `Before?` on events | Dropped; nothing reads it and the stream has the history |
| `Icon?` meaning "inherit" | Kept as `Icon?`; an `ItemIcon` union adds churn without safety |
| Optional free text | Non-null `string`, `""` = none, normalized at the endpoint |
| Kind-correlated fields | Closed hierarchies with an explicit `kind` discriminator (`[JsonPolymorphic]`) |
| Back-compat defaults | Deleted; nothing has been deployed |
| Aggregate folds | Non-null create plus apply; `Rehydrate` is `T?` only for an empty stream |
| Frontend async state | `resource()` for loads, a shared `ActionState` for mutations |
| Order | Phases 0 → 7 as listed; Phase 5 one PR per domain |

## Remaining open questions

- **Recurrence on the wire (5.5).** Lean: keep `recurrence: null` / `until: null` on the wire and
  map at the endpoint, because the frontend's form already models "none". Exposing the union on
  the wire would be additive later.
- **`MealPlanEntry.Rating` redundancy.** `AllRatings` already holds the viewer's rating, and the
  frontend knows its own child id. Lean: keep it for now. Removing it would also remove
  `MealPlanExpansion`'s nullable `viewerId`. This is a separate small cleanup.
- **Provider wire DTOs.** Lean: leave them nullable. Polymorphic content blocks (`TextBlock`,
  `ToolUseBlock`, ...) would remove the `b.Id!`/`b.Name!` assertions in `AnthropicChatClient`, but
  they're optional polish.

## Diagram

```mermaid
flowchart TB
    P0["Phase 0: guardrails\nTS strict + strictTemplates + noUncheckedIndexedAccess\nSTJ RespectNullableAnnotations, ValidateOnStart"]
    P1["Phase 1: non-null caller id\nRequireProvisionedUser filter -> 403 user_not_provisioned"]
    P2["Phase 2: aggregate folds\nStart/Advance, no x! / Rehydrate!"]
    P3["Phase 3: delete back-compat\n= null defaults, legacy CalendarCreated, doc Icon?"]
    P4["Phase 4: free text = string\n\"\" means none, normalized at endpoint"]
    P5["Phase 5: unions (one PR per domain)\nPickupAssignee, ItemSchedule, CompletionTarget,\nOccurrenceTiming, Recurrence, User, WorkDayStatus"]
    P6["Phase 6: drop Before?"]
    P7["Phase 7: frontend state\nresource() + ActionState"]
    P0 --> P1 --> P2 --> P3 --> P4 --> P5 --> P6 --> P7
    P0 -. "compiler checks every later removal" .-> P7
```
