# Client-ready OpenAPI documents

Status: Implemented. `Common/OpenApi` (`AddBuddyOpenApiDocument`, `WireSchemaTransformer`, `ErrorResponsesOperationTransformer`, `SharedResponses`, `ResponseSchemaRequirements`, `.ProducesErrorCode`), string enums over HTTP (`JsonStringEnumConverter`), the combined `/openapi/buddy.json` served in every environment and committed as `docs/backend/openapi/buddy.json` (`Meta/OpenApiDocumentTests`, `task docs:openapi`), and the Angular services' types generated from it (`src/app/core/api/buddy-api.ts`).

## Context

Buddy's API is described by 14 OpenAPI documents, generated at runtime by
`Microsoft.AspNetCore.OpenApi`: one per feature (`/openapi/<feature>.json`, registered in each
`Add<Feature>Feature`, for example
[BabysittersFeature.cs:33](../../../src/backend/buddy/Features/Babysitters/BabysittersFeature.cs))
plus `v1` for `/health` and `/version`
([Program.cs:86](../../../src/backend/buddy/Program.cs)). They are mapped in Development only, and
nothing reads them: the Angular frontend's types in `src/frontend/buddy/src/app/core/*.service.ts`
are written by hand.

The goal is a contract that any client can integrate against: a third-party or mobile client (see
the "Mobile app" item in [TODO.md](../../../TODO.md)), a generated TypeScript client for the
frontend, or a family's own scripts against its instance. A check of the generated documents on
2026-10-09 (170 operations) found that they don't describe the API well enough for that:

| Area | What the documents say today |
| --- | --- |
| Error statuses | Only `400`, `404` and `409`, and only where an endpoint's `Results<...>` declares them. `403` appears nowhere, although 111 endpoints can return it. |
| Middleware responses | `401`, `403 user_not_provisioned`, `409 concurrency_conflict`, the `Idempotency-Key` errors, `304`, `429`, `500` and `503` are missing everywhere. |
| Schemas | 20 component schemas are empty (`{}`): all 14 single-value wrappers (`UserId`, `GroupId`, `Color`, `Language`, `TimeZoneId`, ...) and 6 `kind`-discriminated DTOs (`ItemScheduleRequest`, `ItemScheduleResponse`, `ItemTimingRequest`, `OccurrenceTiming`, `PickupAssigneeDto`, `WorkDayStatus`). `TaskSourceResponse` is missing entirely, because it is only reachable through an empty schema. |
| Enums | 21 enums (`GroupRole`, `MealSlot`, `DoseStatus`, ...) are a bare `{"type": "integer"}` with no values. |
| Success responses | Both iCal feeds list no `200`. |
| Auth and headers | No security scheme; nothing about `Idempotency-Key`, `ETag`/`If-None-Match` or `Retry-After`. |
| Version | The documents are OpenAPI `3.2.0`, which most client generators don't read yet. |
| Availability | Development only, and never committed, so a change to the contract is invisible in a diff. |

What's already good: every operation has an `operationId` (from `.WithName(...)`), request and
response bodies are typed DTOs, and every validation failure, middleware rejection and server error
already shares one body, [`ErrorEnvelope`](../../../src/backend/buddy/Common/ErrorEnvelope.cs). The
behavior is documented by hand in [http-status-codes.md](../http-status-codes.md); this design makes
the machine-readable documents say the same thing.

Nothing here changes how the API behaves, with one exception: the body of one `409` (Decision 4).

This document answers seven questions: where the OpenAPI customization lives, which OpenAPI
version to emit, how the schemas get fixed, how endpoint-level errors are documented, how the
cross-cutting middleware responses are documented, how the documents are published, and how they
are kept from drifting.

## Decision: one `Common/OpenApi` registration that every document goes through

**Decision: a new `Common/OpenApi/OpenApiFeature.cs` with `AddBuddyOpenApiDocument(name, include)`,
which registers a document with the shared schema, operation and document transformers. The 13
feature registrations and `Program.cs` call it instead of `AddOpenApi` directly.**

Same shape as the other cross-cutting concerns under `Common/` (`Common/Http/ETagMiddleware.cs`,
`Common/RateLimiting/RateLimitingFeature.cs`): implemented once, applied everywhere. Transformers
are registered per document in `Microsoft.AspNetCore.OpenApi`, so without a shared helper each of
the 14 call sites would have to repeat the same five registrations.

Before ([BabysittersFeature.cs:33](../../../src/backend/buddy/Features/Babysitters/BabysittersFeature.cs)):

```csharp
services.AddOpenApi(OpenApiDocumentName, options =>
{
    options.ShouldInclude = api => api.GroupName == OpenApiDocumentName;
});
```

After:

```csharp
services.AddBuddyOpenApiDocument(OpenApiDocumentName);
```

Blast radius: 13 `Add<Feature>Feature` methods and `Program.cs`, one line each. No endpoint changes
for this decision.

Rejected: **an endpoint convention per route group** (`group.WithOpenApiResponses()`, next to
`.WithETag()`), which would add `ProducesResponseTypeMetadata` with `Finally(...)`. It reaches
ApiExplorer as well, but it needs a line on each of the 18 route groups, and a group without it is
silently under-documented. A document transformer sees every operation in every document.

Rejected: **Swashbuckle or NSwag** in place of `Microsoft.AspNetCore.OpenApi`. Both are third-party
generators that would duplicate what the framework already does. Everything this design needs is
available through the built-in transformer API.

## Decision: emit OpenAPI 3.1

**Decision: `options.OpenApiVersion = OpenApiSpecVersion.OpenApi3_1` in `AddBuddyOpenApiDocument`.**

.NET 11 defaults to `3.2.0`. The common client generators (openapi-typescript, openapi-generator,
NSwag, Kiota) read 3.0 and 3.1; 3.2 support is partial or missing. Buddy uses nothing that needs
3.2. 3.1 still has JSON Schema 2020-12, so the generator's nullable unions
(`"type": ["null", "string"]`) stay as they are. Moving to 3.2 later is a one-line change.

Rejected: **3.0.** It would turn every nullable member into the older `nullable: true` form for no
benefit.

## Decision: schemas describe what goes over the wire

Three schema transformers, each driven by the same rule the serializer uses, so they can't
disagree with it.

### Single-value wrappers

**Decision: a type that
[`StronglyTypedIdJsonConverterFactory`](../../../src/backend/buddy/Serialization/StronglyTypedIdJsonConverterFactory.cs)
converts gets the schema of its `Value` type.** `UserId` becomes `{"type": "string", "format":
"uuid"}`, and `Color` becomes `{"type": "string"}`. The factory's private `GetValueProperty` becomes
an `internal static bool TryGetValueType(Type, out Type)` that both the factory and the transformer
call. The component keeps its name (`UserId`), so a generated client still gets a named alias.

### `kind`-discriminated DTOs

**Decision: a type whose `[JsonConverter]` derives from
[`KindDiscriminatedJsonConverter<TBase>`](../../../src/backend/buddy/Serialization/KindDiscriminatedJsonConverter.cs)
gets `oneOf` its case types, each case schema carrying a required `kind` with a single-value
integer `enum`.** `kind` stays a number on the wire even though enums became strings (next
decision): it is the converter's case ordinal, not an enum value, and the converter only accepts a
number. The converter's `Cases` map (ordinal -> case type) is already the
single source of truth; it becomes readable from the transformer (`internal`, through a small
non-generic interface). All 7 such converters are covered at once, including
`TaskSourceResponse`, which then appears in the document.

```json
"PickupAssigneeDto": {
  "oneOf": [
    { "$ref": "#/components/schemas/GuardianAssigneeDto" },
    { "$ref": "#/components/schemas/SelfEscortAssigneeDto" },
    ...
  ],
  "description": "One of 5 cases, picked by the numeric \"kind\": 0 = GuardianAssigneeDto, ..."
}
"GuardianAssigneeDto": {
  "required": ["guardianId", "kind"],
  "properties": { "guardianId": { ... }, "kind": { "type": "integer", "enum": [0] } }
}
```

No `discriminator` object: OpenAPI's mapping keys are strings, and the single-value `enum` already
lets a generated TypeScript client narrow on `kind`.

Rejected: **switching the DTOs to `[JsonPolymorphic]`**, which the generator understands natively.
The converter's comment explains why it exists: with an abstract base, a body missing `kind` makes
System.Text.Json throw `NotSupportedException`, which surfaces as a `500` instead of a `400`.

Converters used only for events (`RecurrenceJsonConverter`, `ItemScheduleJsonConverter`,
`PickupAssigneeJsonConverter`, ...) never appear in an HTTP body and need nothing.

### Enums

**Decision: enums go over the wire by member name (`"Owner"`, not `0`): `JsonStringEnumConverter`
in the HTTP JSON options ([Program.cs](../../../src/backend/buddy/Program.cs)).** The generator
then emits a string `enum` with the names, so the contract describes itself:

```json
"GroupRole": { "type": "string", "enum": ["Owner", "Admin", "Member"] }
```

This was a breaking change for every request and response that carries one of the 21 enums. The
frontend's types and the e2e and screenshot seeding changed with it. Numbers are still accepted on
the way in (the converter's `AllowIntegerValues` default), so a client built before the switch
keeps working until it reads an enum back. Dictionary keys of an enum type already serialized as
member names, so they didn't change. Marten's event storage was already `EnumStorage.AsString` and
is unaffected.

The schema transformer lists the names itself, because the generator lets a nullable use of an
enum (`ImportWeekStart?`) add `null` to the shared component's values. The property's own
`oneOf: [null, $ref]` already says it can be null.

Considered and rejected: **keep integers and add `x-enum-varnames`**. It is documentation only, but
every client would still have to map ordinals to meanings by hand, and the frontend did exactly
that.

### Other schema fixes

Generating a client from the document showed four more places where the schema didn't match the
wire. The schema transformer and a document transformer fix them:

- **Numbers.** The web defaults also read a number from a JSON string, so the generator typed
  every number as `integer | string` with a digit pattern. Buddy writes numbers and its clients
  send numbers, so the contract says `integer`.
- **Nullable wrappers and lists of wrappers.** `UserId? ChildId` lost its `null`, and
  `IReadOnlyList<CalendarId>` lost its item schema. Both now follow the wrapper's value type.
- **Computed properties** (`CalendarItemOccurrence.SortAt`,
  `PreviewMealPlanImportRequest.FormatOrAuto`) are `readOnly`, because they are only ever written.
- **Response-only schemas require every property**
  ([ResponseSchemaRequirements.cs](../../../src/backend/buddy/Common/OpenApi/ResponseSchemaRequirements.cs)).
  The generator only requires constructor parameters without a default. That is right for a
  request, but the server always writes every property of a response. A schema that is also sent
  (`PrintTemplateRow`, `RecurrenceRuleRequest`, ...) keeps the request's view, and the frontend
  narrows it with `Required<...>` where it needs to.

## Decision: endpoint-level errors are read from the handler's return type

### `403`

**Decision: an operation transformer reads the handler's declared return type and adds a bodiless
`403` when the `Results<...>` union contains `ForbidHttpResult`.** For minimal APIs the handler's
`MethodInfo` is in the endpoint metadata, so the transformer unwraps `Task<Results<...>>` and checks
its type arguments. `ForbidHttpResult` doesn't implement `IEndpointMetadataProvider`, which is why
the 111 endpoints that return it are undocumented today.

Rejected: **`.Produces(403)` on each endpoint** (111 lines, kept in sync by hand), and **a Buddy
`Forbidden` result type** that implements `IEndpointMetadataProvider` (the same 111 endpoints
change, for no behavior change).

### Responses the framework can't infer

**Decision: three explicit annotations where the result type carries no metadata.**

- Both iCal feeds
  ([GetIcalFeed.Endpoint.cs:15](../../../src/backend/buddy/Features/Calendars/GetIcalFeed/GetIcalFeed.Endpoint.cs),
  [GetMealPlanIcalFeed.Endpoint.cs:15](../../../src/backend/buddy/Features/Mealplans/GetMealPlanIcalFeed/GetMealPlanIcalFeed.Endpoint.cs))
  return `ContentHttpResult`: add `.Produces<string>(200, "text/calendar")`.
- The two invite-accept endpoints return `email_not_verified` through `JsonHttpResult<ErrorEnvelope>`
  ([EmailNotVerified.cs](../../../src/backend/buddy/Features/Users/EmailNotVerified.cs)): add
  `.Produces<ErrorEnvelope>(403)`. The `403` from `ForbidHttpResult` on the same endpoints has no
  body, so the merged response's body is described as optional.

### Error codes

**Decision: each error response lists the `ErrorEnvelope.code` values it can carry, in its
description and in an `x-error-codes` array.** Clients branch on `code` (`resend_cooldown`,
`ai_data_sharing_not_acknowledged`, ...), so the status code alone isn't enough.

- Responses added by the cross-cutting rules (next decision) know their codes.
- `400` from `BadRequest<ErrorEnvelope>` is always `validation_error`.
- The 9 endpoints that return an endpoint-specific `409` or `403` code (including `CreateChild`, below) declare it with new
  metadata: `.ProducesErrorCode(409, ResendCooldown.ErrorCode)`, the same pattern as
  `.AllowUnprovisionedUser()`. Each error-code constant already exists (`ResendCooldown.ErrorCode`,
  `EmailNotVerifiedExtensions.ErrorCode`, ...).

`ErrorEnvelope.code` stays `{"type": "string"}`, with every known code in its description. It is not
an `enum`, because a generated client rejects an enum value it doesn't know, so adding a code would
break existing clients.

### `CreateChild`'s `409`

**Decision: `POST /users/me/children` answers its username conflict with an `ErrorEnvelope`
(`username_unavailable`) instead of a bare JSON string.**
[CreateChild.Endpoint.cs:35](../../../src/backend/buddy/Features/Guardians/CreateChild/CreateChild.Endpoint.cs)
is the only error in the API whose body isn't an `ErrorEnvelope`.

```csharp
// before
CreateChildOutcome.UsernameUnavailable => TypedResults.Conflict("That username is already in use."),
// after
CreateChildOutcome.UsernameUnavailable => TypedResults.Conflict(new ErrorEnvelope(
    UsernameUnavailableCode, "That username is already in use.", new Dictionary<string, string[]>(), httpContext.TraceIdentifier)),
```

Blast radius: one endpoint and its integration test. The frontend checks only the status
([children-step.ts:64](../../../src/frontend/buddy/src/app/features/guardian/onboarding/children-step/children-step.ts),
[manage-children.ts:111](../../../src/frontend/buddy/src/app/features/guardian/admin/manage-children/manage-children.ts)),
so it doesn't change.

## Decision: middleware responses are added by rule, from the same metadata the middleware reads

**Decision: an operation transformer adds each cross-cutting response from the same endpoint
metadata that decides whether the middleware acts.** The transformer and the middleware can then
only disagree if one of them changes its condition, and the meta test (last decision) checks the
conditions.

| Response | Codes | Added when | Source |
| --- | --- | --- | --- |
| `401` | -- (`WWW-Authenticate` header) | the endpoint has `IAuthorizeData` and no `IAllowAnonymous` | `UseAuthorization` |
| `403` | `user_not_provisioned` | as `401`, and no `AllowUnprovisionedUserMetadata` | [ProvisionedUserMiddleware](../../../src/backend/buddy/Features/Users/ProvisionedUserMiddleware.cs) |
| `400` | `validation_error` | the operation has a request body, or a route/query parameter that can fail to bind | [RequestBindingFailureMiddleware](../../../src/backend/buddy/Common/Validation/RequestBindingFailureMiddleware.cs) |
| `400` | `invalid_idempotency_key` | `POST` | [IdempotencyKeyMiddleware](../../../src/backend/buddy/Common/Idempotency/IdempotencyKeyMiddleware.cs) |
| `409` | `idempotency_key_reused`, `idempotency_key_in_progress`, `idempotency_response_unavailable` | `POST` | same |
| `409` | `concurrency_conflict` | any method other than `GET` | [ConcurrencyConflictMiddleware](../../../src/backend/buddy/Common/Concurrency/ConcurrencyConflictMiddleware.cs) |
| `304` | -- (no body) | `GET` with `ETagMetadata` | [ETagMiddleware](../../../src/backend/buddy/Common/Http/ETagMiddleware.cs) |
| `429` | `rate_limited` (`Retry-After` header) | no `DisableRateLimitingAttribute` | [RateLimitingFeature](../../../src/backend/buddy/Common/RateLimiting/RateLimitingFeature.cs) |
| `500` | `internal_error` | always | [ExceptionHandlingFeature](../../../src/backend/buddy/Common/Errors/ExceptionHandlingFeature.cs) |
| `503` | `dependency_unavailable` (`Retry-After` header) | always | same |

When a response for the status already exists (a `409 resend_cooldown` declared by the endpoint
and the generic `409 concurrency_conflict`), the transformer merges them: one response, the union
of the codes, and the `ErrorEnvelope` schema.

The responses that are identical everywhere (`401`, `304`, `429`, `500`, `503`) are defined once
under `components/responses`
([SharedResponses.cs](../../../src/backend/buddy/Common/OpenApi/SharedResponses.cs)) and referenced,
which keeps the committed document about a third smaller.

`409 concurrency_conflict` on every non-`GET` overstates it slightly: a command that appends
nothing can't lose the race. The description says "may", which is accurate, and it is safer than
missing the case. Working out which handlers append would mean inspecting Wolverine handlers, which
isn't worth it.

## Decision: security scheme and protocol headers in the document

**Decision: a document transformer declares one `openIdConnect` security scheme, `keycloak`, whose
`openIdConnectUrl` is `{KeycloakOptions.Authority}/.well-known/openid-configuration`.** Operations
under the `401` rule above get `security: [{ "keycloak": [] }]`, and anonymous ones get none. The
Authority comes from configuration
([KeycloakOptions.cs](../../../src/backend/buddy/Features/Users/KeycloakOptions.cs)), so each
family's instance publishes its own Keycloak, and a client discovers the authorize and token
endpoints from it.

**Headers documented:**

- `Idempotency-Key`: optional request header on every authenticated `POST`, string, 1-200
  characters, with the replay semantics from
  [http-status-codes.md](../http-status-codes.md#idempotency-key-post).
- `If-None-Match`: optional request header, and `ETag` and `Cache-Control` response headers on
  `200`, for `GET`s with `ETagMetadata`.
- `Retry-After` on `429` and `503`.

## Decision: one combined document, committed to the repo, served by every instance

**Decision: a 15th document, `buddy`, that includes every operation, is the published contract.**
The per-feature documents stay for browsing, but a client generator wants one input. The existing
`v1` document stays as is, because the e2e setup and the `run-buddy` skill use
`/openapi/v1.json` as a readiness probe.

**Decision: the combined document is committed as `docs/backend/openapi/buddy.json`**, written by a
test against the Alba host, the same way the event golden files pin event shapes
([EventShapeTestSupport.cs](../../../src/backend/buddy.IntegrationTests/EventShapeTests/EventShapeTestSupport.cs)).
`Meta/OpenApiDocumentTests` fetches `/openapi/buddy.json` and compares it with the committed file.
The comparison ignores two values that depend on the host: `servers` and the Keycloak URL, which is
replaced by a fixed placeholder. On a mismatch it fails with the differing paths. With
`BUDDY_UPDATE_OPENAPI=1` it rewrites the file instead, wrapped as `task docs:openapi` like
`task docs:screenshots`. Every API contract change then shows up as a reviewable diff of that file.

Rejected: **build-time generation** with `Microsoft.Extensions.ApiDescription.Server`. It starts the
app's host during `dotnet build`, which in Buddy validates options on start (`ValidatedOptions`),
registers 13 Marten stores and runs `RunJasperFxCommands`. Getting that to start without Postgres or
Keycloak config is fragile, and the Alba host already starts the real app with both.

**Decision: `MapOpenApi()` runs in every environment, not only Development**, anonymous, under the
default per-IP rate limit. The source code is public (MIT, linked from the profile menu), so the
document reveals nothing new. A family's technical member, or an app pointed at their instance, can
read the contract of exactly the version deployed there.

## Decision: meta tests keep the documents honest

Same approach as `Meta/ETagCoverageTests` and `Meta/RateLimitingCoverageTests`: they enumerate
the real endpoints or document and fail with a list.

`Meta/OpenApiDocumentTests`:

- **Golden file**: `buddy.json` matches the committed copy (above).
- **No empty schemas**: no component schema is `{}`, so a new converter can't silently produce an
  untyped schema.
- **Enums list their names**: every schema backed by a .NET enum has a string `enum`.
- **Every endpoint's `403` is documented**: every endpoint whose handler can return
  `ForbidHttpResult` or `JsonHttpResult<ErrorEnvelope>` documents `403`.
- **Every error response has a body or is listed**: each `4xx` and `5xx` response has the
  `ErrorEnvelope` schema, unless it is on a short explicit list of bodiless responses (`401`, `403`
  from `Forbid`, `404`, `304`).
- **Every operation is in `buddy`**: the per-feature documents together contain exactly the
  operations of the combined one, so a route group without `.WithGroupName` is caught.

## Testing

- Unit-level tests for each transformer against a small hand-built `OpenApiOperation` or schema,
  next to the existing `Common` tests in `buddy.IntegrationTests/Common/`.
- `Meta/OpenApiDocumentTests` as above, against the shared `BuddyApiFixture` (Development
  environment, so `/openapi/*.json` is already mapped there today).
- `CreateChild`'s existing `409` test asserts the `username_unavailable` envelope.
- The frontend's generated types are the consumability check: `frontend-tests.yml` regenerates
  `src/app/core/api/buddy-api.ts` from the committed `buddy.json` with openapi-typescript and fails
  if it differs, and the type check then fails on any service that no longer matches the contract.

## Failure and edge-case behavior

| Case | Behavior |
| --- | --- |
| A new endpoint with a new `Forbid` path | Documented automatically (return-type rule); the golden file diff shows it. |
| A new middleware that answers with a new status | Not documented until a rule is added; the "every error response has a body" test doesn't catch a status that was never documented. Handled by review, like `http-status-codes.md` today. |
| A new single-value wrapper or `kind`-discriminated DTO | Covered by the converter-driven transformers; "no empty schemas" fails if it isn't. |
| A new enum | Covered automatically; "enums have values" fails if not. |
| A new error code | Description and `x-error-codes` update through the constant; the golden file diff shows it. Existing generated clients keep working because `code` is not an `enum`. |
| Same status from endpoint and middleware | Merged into one response with the union of codes. |
| Anonymous endpoint (iCal feeds, invite previews, shared sleep diary, `/version`) | No `security`, no `401`/`403 user_not_provisioned`; still `429`, `500`, `503`. |
| Contract change without regenerating `buddy.json` | `OpenApiDocumentTests` fails in `task test:backend` and CI. |

## Decisions made

| Question | Decision |
| --- | --- |
| Where the customization lives | `Common/OpenApi`, one `AddBuddyOpenApiDocument` used by all 14 registrations |
| OpenAPI version | 3.1 (3.2 isn't read by most generators yet) |
| Wrapper and `kind` schemas | Derived from the converters' own rules, so schema and serializer can't disagree |
| Enums | Strings over HTTP (`JsonStringEnumConverter`); numbers still accepted on input |
| `kind` discriminators | Stay numeric; `oneOf` cases with a single-value `enum` |
| Response-only schemas | Every property required; computed properties `readOnly` |
| Serve the documents in production | Yes, anonymous under the default rate limit |
| Per-endpoint tables in `http-status-codes.md` | Replaced by a pointer to `buddy.json` |
| Frontend types | Generated from `buddy.json` with openapi-typescript (`npm run api:types`); the services alias them |
| `403` documentation | Read from the handler's `Results<...>` return type, not annotated per endpoint |
| Error codes | Per response, in the description and `x-error-codes`; `code` itself stays an open string |
| `CreateChild` `409` body | Becomes an `ErrorEnvelope` (`username_unavailable`), the one behavior change |
| Middleware responses | Added by rule from the same metadata each middleware reads |
| Auth | One `openIdConnect` scheme from the configured Keycloak Authority |
| Published contract | A combined `buddy` document, committed as `docs/backend/openapi/buddy.json` and pinned by a golden-file test |

## Remaining open questions

- **A UI for the document** (Scalar or Swagger UI). Lean: no. Any OpenAPI viewer can open the
  served JSON, and a UI adds a frontend dependency to the API.
- **String `kind` discriminators.** `kind` is still a number while every enum is a name. Making it
  the case name would need an enum per converter (three of them are keyed by bare ordinals today)
  and another breaking change for pickups, calendar items and work days. Lean: only if a
  third-party client asks.

## Diagram

```mermaid
flowchart TB
    subgraph Host["Buddy API host"]
        direction TB
        Endpoints["Endpoints\nResults<...> return types\n+ metadata (IAuthorizeData, ETagMetadata,\nDisableRateLimiting, ProducesErrorCode)"]
        Middleware["Middleware\nauth, provisioning, binding failures,\nidempotency, concurrency, ETag,\nrate limiting, exception handling"]
        Converters["JSON converters\nStronglyTypedIdJsonConverterFactory\nKindDiscriminatedJsonConverter"]
        subgraph OpenApi["Common/OpenApi (AddBuddyOpenApiDocument)"]
            Schema["Schema transformers\nwrappers, kind oneOf, enums"]
            Operation["Operation transformers\nForbid from return type,\nerror codes, middleware rules, headers"]
            Document["Document transformer\nkeycloak security scheme"]
        end
        Docs["/openapi/{feature}.json\n/openapi/buddy.json (3.1)"]
        Endpoints -- "metadata the middleware also reads" --> Operation
        Middleware -. "same conditions" .-> Operation
        Converters -- "same type rules" --> Schema
        Schema --> Docs
        Operation --> Docs
        Document --> Docs
    end
    Docs --> Test["Meta/OpenApiDocumentTests\ngolden file + coverage checks"]
    Test --> Golden["docs/backend/openapi/buddy.json\n(committed contract)"]
    Golden --> Clients["Clients\ngenerated TS client, mobile app,\nfamily scripts"]
```
