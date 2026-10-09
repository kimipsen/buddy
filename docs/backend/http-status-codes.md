# HTTP Status Code Semantics

This document defines how Buddy backend endpoints should use HTTP status codes.

Scope:
- REST endpoints across the `users`, `guardians`, `groups`, `calendars`,
   `medicines`, `mealplans`, `pickups`, `task-templates`, and `progress`
   API groups
- authenticated endpoints using bearer tokens
- query and command operations, including create, update, delete, verify, and
   resend

## Principles

- Use the most specific status code that explains the result.
- Keep success and error shapes consistent across endpoints.
- Never leak private resource existence to unauthorized callers.
- Prefer idempotent behavior where practical for retry safety.
- Return `4xx` for client problems and `5xx` for server problems.

## Status Code Classes

### 2xx Success
Request was valid and processed.

### 3xx Redirection
Rare for API responses. Avoid unless an endpoint is explicitly redirect-based.

### 4xx Client errors
The client request is invalid, unauthorized, forbidden, or conflicts with current resource state.

### 5xx Server errors
Unexpected server-side failures. Log and monitor these as defects or outages.

## Code-by-Code Guidance

### 200 OK
Use when returning a representation in the response body.

Typical Buddy use:
- `GET /users/me`
- `GET /calendars/{id}`
- `GET /calendars/{id}/items`

Also used for creates: every create-style `POST` in Buddy (create calendar, item, group,
child, meal, invite, iCal token, ...) returns `200` with the created resource in the body --
see [201 Created](#201-created).

Do not use when:
- the response intentionally has no body (use `204`)

### 201 Created
Not used. Buddy's create endpoints return `200 OK` with the created resource in the body, and
no endpoint uses `TypedResults.Created` or sets a `Location` header. The frontend and the
integration tests depend on `200`, so a switch to `201` would be a deliberate API contract
change across every create endpoint, not a per-endpoint choice. New create endpoints follow
the `200` convention.

### 202 Accepted
Use only when work is queued for asynchronous processing and not completed yet.

Typical Buddy use:
- none currently (most operations are synchronous)

### 204 No Content
Use when the operation succeeded and no response body is needed.

Typical Buddy use:
- idempotent delete where repeating delete remains successful
- resend verification accepted without payload
- revoke token when successful

### 304 Not Modified
Use with conditional requests (`ETag` or `If-Modified-Since`).

Typical Buddy use:
- any GET in a feature route group, when `If-None-Match` matches the current `ETag` (see [Conditional GET (ETag)](#conditional-get-etag))

### 400 Bad Request
Use when request syntax or domain validation fails.

Typical Buddy use:
- invalid recurrence rule
- invalid date range (`from > to`)
- malformed verify token payload

Return guidance:
- include machine-readable error code
- include field-level validation details when possible

### 401 Unauthorized
Use when authentication is missing or invalid.

Typical Buddy use:
- missing bearer token
- expired/invalid JWT

Notes:
- this is about authentication, not permissions
- include authentication challenge headers where applicable

### 403 Forbidden
Use when caller is authenticated but lacks required permission tier.

Typical Buddy use:
- viewer attempting contributor-only operation
- contributor attempting owner-only operation

- an authenticated caller with no Buddy user yet (`user_not_provisioned`): the Keycloak token is
  valid but its subject never called `GET /users/me`, which creates the user.
  `ProvisionedUserMiddleware` returns this once, before any handler runs, on every authorized
  endpoint except `GET /users/me` itself. Anonymous endpoints (iCal feeds, invite previews) are
  not affected. It is `403` rather than `401` because the token is valid, so a client that
  refreshes its token on `401` would loop.
- accepting an invite sent to the caller's own email before they've verified it
  (`email_not_verified`): `POST /invites/{token}/accept` and
  `POST /guardian-invites/{token}/accept` return this code with the `ErrorEnvelope` body
  (`Features/Users/EmailNotVerified`), so the client can ask the user to verify first. An
  invite sent to a different address is a plain `403` with no body.

Security note:
- if the route uses privacy-preserving existence hiding, you may intentionally return `404` instead of `403`

### 404 Not Found
Use when resource does not exist, is deleted, or should be hidden from caller.

Typical Buddy use:
- unknown calendar id
- deleted user
- non-member access to private calendar (existence-hiding)

### 405 Method Not Allowed
Use when resource exists but HTTP method is not supported by that route.

Typical Buddy use:
- framework-generated for unsupported verbs

### 409 Conflict
Use when request is valid but conflicts with current resource state.

Typical Buddy use:
- a resend during the shared one-minute resend cooldown (`resend_cooldown`):
  `POST /users/me/email/verify/resend`, `POST /groups/{groupId}/invites` and
  `POST /users/me/children/{childId}/guardian-invites` all use
  `Common/RateLimiting/ResendCooldown` and return this code with the `ErrorEnvelope` body
- optimistic concurrency (`concurrency_conflict`): another request appended to the same event
  stream between this request's read and its append. Any command endpoint can return it (it is
  produced centrally by `ConcurrencyConflictMiddleware`, not declared per endpoint); the client
  should reload and retry
- `Idempotency-Key` reused with a different request body (`idempotency_key_reused`), or a
  request with that key still in flight (`idempotency_key_in_progress`) -- see
  [Idempotency-Key (POST)](#idempotency-key-post) below
- the family hasn't acknowledged what the AI assistant shares with its provider
  (`ai_data_sharing_not_acknowledged`): `POST /mealplans/children/{childId}/ai/sessions` and
  `POST /mealplans/children/{childId}/ai/sessions/current/messages`, until
  `PUT /mealplans/children/{childId}/ai/data-sharing-acknowledgement`

### 410 Gone
Use when resource used to exist but is permanently removed and this distinction is useful.

Typical Buddy use:
- usually not needed because deleted resources are treated as `404`

### 412 Precondition Failed
Use when conditional headers are provided and preconditions fail.

Typical Buddy use:
- future support for `If-Match` concurrency control

### 415 Unsupported Media Type
Use when request content type is unsupported.

Typical Buddy use:
- non-JSON payload on JSON endpoints

### 422 Unprocessable Content
Use for semantically invalid payloads when syntax is correct.

Typical Buddy use:
- not used in this project

Team rule (resolved):
- `400` is used uniformly for all validation failures, structural and
  semantic alike — see the [validation rules analysis](analysis/validation-rules.md).
  `422` is not used anywhere; this keeps the status-code decision independent
  of *why* a request was rejected.

### 429 Too Many Requests
Use when request rate exceeds limits.

Typical Buddy use:
- any endpoint, from the rate limiter (`Common/RateLimiting/RateLimitingFeature`):
  a caller over its per-user or per-IP bucket, an iCal feed link over its
  per-link bucket, or a user over the `ai-assistant` / `outbound-email` / `personal-data-export` policy.
  See the [rate limiting analysis](analysis/rate-limiting.md).
- not for the resend cooldown: that is a state conflict and returns `409`
  (`resend_cooldown`), see above

Return guidance:
- `Retry-After` in whole seconds, plus the `ErrorEnvelope` with code
  `rate_limited`
- endpoints don't declare it in their `Results<...>`; the middleware renders it
  before any endpoint runs

### 500 Internal Server Error
Use for unexpected application errors.

Typical Buddy use:
- unhandled exceptions
- unexpected persistence/runtime failures

Return guidance:
- do not leak stack traces or secrets
- include correlation/request id for support

Implemented by `buddy.Common.Errors.UnhandledExceptionHandler`: any exception no
other middleware handles becomes `500` with the `ErrorEnvelope`, code
`internal_error`, an empty `details`, and the `requestId` that matches the logged
exception. The message never includes the exception's text or type.

### 502 Bad Gateway / 503 Service Unavailable / 504 Gateway Timeout
Use when upstream dependencies fail or are unavailable.

Typical Buddy use:
- identity provider unavailable
- SMTP provider outage or timeout
- database temporarily unavailable

Implemented for `503`: when the exception, or one of its inner exceptions, says a
dependency couldn't be reached, the same handler answers `503` with code
`dependency_unavailable` and `Retry-After: 5`. That covers a transient
`NpgsqlException`, a `SocketException` (SMTP, for example), and an
`HttpRequestException` for a connection or DNS failure (Keycloak's admin API, AI
providers). An error status from a dependency, such as Keycloak answering `403`,
is a `500`: retrying won't help.

## Decision Checklist

When selecting a status code, ask in order:

1. Did the request authenticate?
   - no: `401`
2. Is the caller allowed to know the resource exists?
   - no: `404`
3. Is the caller authenticated but under-privileged?
   - yes: `403`
4. Is request syntax/shape invalid?
   - yes: `400` or `415`
5. Is request semantically invalid?
   - yes: `400` or `422`
6. Does request conflict with current state?
   - yes: `409`
7. Did we create something?
   - yes: `200` with the created resource (Buddy does not use `201`)
8. Did we succeed with no body?
   - yes: `204`
9. Otherwise successful read/update with body
   - `200`

## Suggested Defaults For This Project

- Reads: `200`, `401`, `404`
- Creates: `200`, `400`, `401`, `403`, `404`, `409`
- Updates/Patches: `200` or `204`, plus `400`, `401`, `403`, `404`, `409`
- Deletes: `204`, plus `401`, `403` or `404`
- Verification flows: `204` or `200`, plus `400`, `401`, `404`, `409`, optional `429`

## Idempotency-Key (POST)

`DELETE` and most `PUT`/`PATCH` handlers in this codebase are idempotent by construction: each
reads current state and appends an event only when something actually changes (see e.g.
`UpdateMealDetailsHandler`, `ClearMealSlotHandler`). `POST` create endpoints (`CreateMeal`,
`CreateGroup`, `CreateChild`, `InviteGuardian`, ...) can't follow that pattern -- a fresh id is
the point of a create -- so a network-timeout retry or a client double-tap would otherwise create
a second resource.

`IdempotencyKeyMiddleware` (`buddy.Common.Idempotency`) closes that gap for any `POST` as an
opt-in: a client that includes an `Idempotency-Key` header (any client-generated string, at most
200 characters) gets retry safety; a client that doesn't is completely unaffected.

The Angular frontend opts in for all of its create-style `POST` calls through `postIdempotent`
(`src/frontend/buddy/src/app/core/http-idempotency.ts`), which generates a fresh key per call and
retries a transient failure (network error or `5xx`) with that same key instead of a new one.

- First request with a given `(caller, key)`: executes normally; the response (status, content
  type, and body) is cached against the key.
- A retry with the same key and the same request (method, path, query, and body): replays the
  cached response verbatim instead of re-executing -- no second resource, no second email, etc.
- A retry with the same key but a *different* request: `409 idempotency_key_reused`.
- A concurrent request still holding the same key: `409 idempotency_key_in_progress`.
- Malformed key (empty, or over 200 characters): `400 invalid_idempotency_key`.

Completed entries are kept for 24h, then swept by a background cleanup pass; an entry with no stored
response yet whose owning request never completed (a crash mid-request) is swept after 5 minutes so the
key becomes claimable again.

## Conditional GET (ETag)

Every GET endpoint in a feature route group carries an `ETag` on its `200` response
(`ETagMiddleware`, `buddy.Common.Http`; opted in per route group with `.WithETag()`). The tag is a
SHA-256 hash of the response body, so it changes whenever any byte of the output changes -- including
output that depends on the date rather than on events (the iCal feeds' rolling windows, "today"
endpoints).

- `200` GET: `ETag: "<hash>"` and `Cache-Control: private, no-cache` (unless the endpoint set its
  own `Cache-Control`, as the iCal feeds do with the same value).
- A request whose `If-None-Match` matches (weak comparison, a list, or `*`): `304` with no body,
  the same `ETag` and `Cache-Control`.
- Any non-`200` status, and every non-GET method: no `ETag`, never `304`.
- `/version` and the OpenAPI documents are not tagged; `Meta/ETagCoverageTests`
  holds that exclusion list.

The Angular frontend needs no code for this: the browser's HTTP cache sends `If-None-Match` and
turns a `304` back into the cached `200`. `no-cache` means every reuse is revalidated, so a
response is never stale. `If-Match` / `412` on writes is not implemented. Design:
[conditional-get-etags.md](analysis/conditional-get-etags.md).

## Endpoint Status Mapping

The statuses each endpoint can answer with, and the `ErrorEnvelope` codes for each, are in the
API contract: [openapi/buddy.json](openapi/buddy.json), also served by every running instance at
`/openapi/buddy.json` (and per feature at `/openapi/<feature>.json`). It is generated from the code,
so it can't drift. `Meta/OpenApiDocumentTests` fails when it changes, and `task docs:openapi`
regenerates it. How it's built: [openapi-client-contract.md](analysis/openapi-client-contract.md).

What the contract lists for every operation, besides the endpoint's own results:

- `401` and `403 user_not_provisioned` on every endpoint that requires authorization (`GET /users/me`
  only gets the `401`). Anonymous endpoints (iCal feeds, invite previews, shared sleep diaries,
  `/version`, the OpenAPI documents) have neither.
- `403` with no body wherever the endpoint can return `TypedResults.Forbid()`.
- `400 validation_error` wherever there is a body or query to bind.
- `400 invalid_idempotency_key` and `409 idempotency_key_*` on authenticated `POST`s.
- `409 concurrency_conflict` on every method other than `GET`.
- `304` on `GET`s with an `ETag`.
- `429 rate_limited`, `500 internal_error` and `503 dependency_unavailable` everywhere except
  `/health`.

Some write endpoints intentionally collapse private-resource visibility into `404` for non-members.
The contract can't show which `404`s are deliberate, so that rule stays here
([404 Not Found](#404-not-found)).

## Changing an endpoint's contract

- Pick statuses with the rules above, then let the code document them: a result type in the
  endpoint's `Results<...>`, `.ProducesErrorCode(status, code)` for an `ErrorEnvelope` code the
  endpoint returns itself, or `.Produces<T>(status, contentType)` when the result type carries
  no metadata (`ContentHttpResult`, `JsonHttpResult<T>`).
- A new middleware that answers with its own status needs a rule in
  `Common/OpenApi/ErrorResponsesOperationTransformer.cs`.
- Run `task docs:openapi` and review the diff of `openapi/buddy.json` (and the regenerated frontend
  types) in the same PR.

## Error Response Shape (Implemented)

Every `400` produced by a `Result<T>.Validation` (or feature-specific outcome
union's `Validation` case) renders through this envelope
(`buddy.Common.ErrorEnvelope` / `ValidationProblemExtensions.ToEnvelope`,
built from FluentValidation's `ValidationResult` via
`buddy.Common.Validation.ValidationProblem`):

```json
{
  "code": "validation_error",
  "message": "One or more fields are invalid.",
  "details": {
    "field": ["must not be empty"]
  },
  "requestId": "..."
}
```

`details` keys are field names as FluentValidation derives them from the
command's property names (or `""` for a general, non-field-specific error,
e.g. a resend-cooldown rejection). `requestId` is `HttpContext.TraceIdentifier`, which is the
request's trace id (see [observability.md](observability.md#logs-and-correlation)).
`NotFound`/`Forbidden` outcomes are unaffected — they keep their existing,
endpoint-specific mappings (some deliberately collapse `Forbidden` into `404`
for privacy). Keep the schema stable for clients.

An unhandled exception renders through the same envelope: `500` with code
`internal_error`, or `503` with code `dependency_unavailable` (see the 500 and 503
sections above).

A request body that can't be bound renders through the same envelope with
`code: "validation_error"` (`buddy.Common.Validation.RequestBindingFailureMiddleware`,
enabled by `RouteHandlerOptions.ThrowOnBadRequest`). The HTTP JSON options set
`RespectRequiredConstructorParameters` and `RespectNullableAnnotations`, so an
omitted request-record parameter without a default, or `null` for a non-nullable
one, is rejected before the handler runs. Here `details` keys are the JSON path
of the field (`name`, `days[0].locationId`), or `""` for malformed JSON.
Optional request fields are nullable with a `= null` default.
