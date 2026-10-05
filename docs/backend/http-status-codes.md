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
- `GET /users/me/events`
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
- not currently used

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
- not currently used: the resend cooldown is a state conflict and returns `409`
  (`resend_cooldown`), see above

Return guidance:
- include `Retry-After` when known

### 500 Internal Server Error
Use for unexpected application errors.

Typical Buddy use:
- unhandled exceptions
- unexpected persistence/runtime failures

Return guidance:
- do not leak stack traces or secrets
- include correlation/request id for support

### 502 Bad Gateway / 503 Service Unavailable / 504 Gateway Timeout
Use when upstream dependencies fail or are unavailable.

Typical Buddy use:
- identity provider unavailable
- SMTP provider outage or timeout
- database temporarily unavailable

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

## Endpoint Status Mapping

This section maps current Buddy endpoints to their expected status codes.

Notes:
- `401` applies to all endpoints protected by `.RequireAuthorization()` when token authentication fails.
- Some write endpoints intentionally collapse private-resource visibility into `404` for non-members.
- `500` remains possible for unexpected failures even when omitted from endpoint-level mappings.
- `409 concurrency_conflict` is possible on any command endpoint that reads and then appends to
  an event stream (a lost optimistic-concurrency race) and is omitted from the tables below.
- `403 user_not_provisioned` is possible on every authorized endpoint except `GET /users/me`
  (see [403 Forbidden](#403-forbidden)) and is omitted from the tables below.

### Users API (`/users`)

| Endpoint | Success | Client error statuses | When to use |
| --- | --- | --- | --- |
| `GET /users/me` | `200` | `401`, `404` | `404` when local user is deleted or not available for the authenticated subject. |
| `GET /users/me/events` | `200` | `400`, `401` | `400` for invalid paging cursor or page-size input. |
| `PATCH /users/me/name` | `200` | `401`, `404` | `404` when local user does not exist or is deleted. |
| `PATCH /users/me/email` | `200` | `400`, `401`, `404` | `400` for invalid email payload. |
| `POST /users/me/email/verify/resend` | `204` | `401`, `404`, `409` | `409 resend_cooldown` during the resend cooldown; `204` for already-verified or resend accepted. |
| `POST /users/me/email/verify` | `200` | `400`, `401`, `404` | `400` for invalid/expired token or malformed request. |
| `DELETE /users/me` | `204` | `401` | Idempotent delete: repeated deletes remain `204`. |

### Calendars API (`/calendars`)

| Endpoint | Success | Client error statuses | When to use |
| --- | --- | --- | --- |
| `GET /calendars` | `200` | `401` | Authenticated list of caller-visible calendars. |
| `POST /calendars` | `200` | `400`, `401`, `403` | Creates return `200` with the created calendar (see [201 Created](#201-created)). |
| `GET /calendars/{calendarId}` | `200` | `401`, `404` | `404` for unknown/deleted/hidden calendar. |
| `DELETE /calendars/{calendarId}` | `204` | `401`, `403`, `404` | `403` for authenticated non-owner; `404` for unknown/hidden calendar. |
| `PUT or PATCH /calendars/{calendarId}/members/{memberId}` (set role) | `204` | `400`, `401`, `403`, `404` | `400` for invalid role payload; owner-level operation. |
| `DELETE /calendars/{calendarId}/members/{memberId}` | `204` | `401`, `403`, `404` | Removes membership when caller has required permission. |
| `POST /calendars/{calendarId}/items` | `200` | `400`, `401`, `403`, `404` | `400` for invalid schedule/recurrence/details payload. |
| `GET /calendars/{calendarId}/items` | `200` | `401`, `404` | Returns item list for visible calendar. |
| `GET /calendars/{calendarId}/occurrences` | `200` | `400`, `401`, `404` | `400` for invalid `from/to` range or query validation failures. |
| `PATCH /calendars/{calendarId}/items/{itemId}/details` | `200` | `401`, `403`, `404` | `403` for viewer/non-contributor. |
| `PATCH /calendars/{calendarId}/items/{itemId}/schedule` | `200` | `400`, `401`, `403`, `404` | `400` for invalid period/due-date transition rules. |
| `PATCH /calendars/{calendarId}/items/{itemId}/recurrence` | `200` | `400`, `401`, `403`, `404` | `400` for invalid recurrence values (interval below 1, `until` before the item's first date). |
| `PATCH /calendars/{calendarId}/items/{itemId}/completion` | `200` | `400`, `401`, `403`, `404` | `400` for a future occurrence or a template-scheduled task (completed per subtask); `403` for a viewer completing a task not assigned to them. |
| `PATCH /calendars/{calendarId}/items/{itemId}/subtasks/{subtaskId}/completion` | `200` | `400`, `401`, `403`, `404` | `400` for a future occurrence or a plain task; `404` also for an unknown subtask. |
| `DELETE /calendars/{calendarId}/items/{itemId}` | `204` | `401`, `403`, `404` | Idempotent deletion behavior recommended. |
| `POST /calendars/{calendarId}/ical-tokens` | `200` | `401`, `403`, `404` | Token issuance for authorized member. |
| `GET /calendars/{calendarId}/ical-tokens` | `200` | `401`, `403`, `404` | Token summary listing for authorized member. |
| `DELETE /calendars/{calendarId}/ical-tokens/{tokenId}` | `204` | `401`, `403`, `404` | Revokes existing token when authorized. |
| `GET /calendars/{calendarId}/ical/{token}` | `200` | `404` | Anonymous endpoint; `404` for unknown calendar or invalid token. |

### Groups API (`/groups`, plus `/invites/{token}/...`)

| Endpoint | Success | Client error statuses | When to use |
| --- | --- | --- | --- |
| `POST /groups` | `200` | `401` | Creating a group cannot fail for a provisioned caller. |
| `GET /groups` | `200` | `401` | Lists groups visible to the caller. |
| `GET /groups/{groupId}` | `200` | `401`, `404` | `404` for unknown group or non-member. |
| `PUT /groups/{groupId}/members/{memberId}` | `204` | `400`, `401`, `403`, `404` | `400` for attempting to grant `Owner` through this endpoint; `403` for a caller without member-management role. |
| `DELETE /groups/{groupId}/members/{memberId}` | `204` | `401`, `403`, `404` | `403` for a caller without member-management role. |
| `PUT /groups/{groupId}/calendar-permission-policy` | `204` | `400`, `401`, `403`, `404` | `400` when the policy is missing an entry for every group role. |
| `PUT /groups/{groupId}/mealplan-permission-policy` | `204` | `400`, `401`, `403`, `404` | `400` for a missing role entry or an invalid `Rate` policy value. |
| `PUT /groups/{groupId}/medicine-permission-policy` | `204` | `400`, `401`, `403`, `404` | `400` for a missing role entry or an invalid `Mark` policy value. |
| `PUT /groups/{groupId}/children/{childId}` | `204` | `401`, `403`, `404` | Adds a child directly, skipping invite/accept; `403` when the caller lacks group-management role or an active guardian link to the child. |
| `DELETE /groups/{groupId}` | `204` | `401`, `403`, `404` | `403` for an authenticated non-owner. |
| `POST /groups/{groupId}/invites` | `200` | `400`, `401`, `403`, `404`, `409` | `400` for a malformed invite payload; `403` for a non-owner/admin or an `Owner` role invite; `409 resend_cooldown` when the same email was invited less than a minute ago. |
| `GET /groups/{groupId}/invites` | `200` | `401`, `403`, `404` | Owner/admin-only listing of pending invites. |
| `DELETE /groups/{groupId}/invites/{inviteId}` | `204` | `401`, `403`, `404` | Revokes a pending invite. |
| `GET /invites/{token}/preview` | `200` | `404` | Anonymous; `404` for unknown, accepted, or expired token. |
| `POST /invites/{token}/accept` | `204` | `401`, `403`, `404` | `403` when the caller's own verified email doesn't match the invite. |

### Guardians API (`/users/me/children`, `/users/me/guardians`, `/users/me/siblings`, `/guardian-invites`)

| Endpoint | Success | Client error statuses | When to use |
| --- | --- | --- | --- |
| `POST /users/me/children` | `200` | `400`, `401`, `409` | `400` for invalid child-profile fields; `409` when the requested username is already in use. |
| `GET /users/me/children` | `200` | `401` | Lists the caller's own child accounts. |
| `GET /users/me/children/{childId}/guardians` | `200` | `401`, `404` | `404` for unknown child or no guardian relationship. |
| `DELETE /users/me/children/{childId}/guardian-link` | `204` | `401`, `404` | `404` for unknown child or no active link to revoke. |
| `PATCH /users/me/children/{childId}/language` | `200` | `400`, `401`, `404` | `400` for an unsupported language code. |
| `PATCH /users/me/children/{childId}/timezone` | `200` | `400`, `401`, `404` | `400` for an invalid time zone id. |
| `GET /users/me/guardians` | `200` | `401` | Lists guardians linked to the caller. |
| `GET /users/me/siblings` | `200` | `401` | Lists the caller's sibling children resolved from the shared guardian-link graph. |
| `POST /users/me/children/{childId}/guardian-invites` | `200` | `400`, `401`, `404`, `409` | `400` for a malformed invite payload; `404` for unknown child or caller without an active guardian link; `409 resend_cooldown` when the same email was invited less than a minute ago. |
| `GET /users/me/children/{childId}/guardian-invites` | `200` | `401`, `404` | `404` for unknown child or caller without an active guardian link. |
| `DELETE /users/me/children/{childId}/guardian-invites/{inviteId}` | `204` | `401`, `404` | `404` for unknown invite or caller without an active guardian link. |
| `GET /guardian-invites/{token}/preview` | `200` | `404` | Anonymous; `404` for unknown, accepted, or expired token. |
| `POST /guardian-invites/{token}/accept` | `204` | `401`, `403`, `404` | `403` when the caller's own verified email doesn't match the invite. |

### Mealplans API (`/mealplans`)

| Endpoint | Success | Client error statuses | When to use |
| --- | --- | --- | --- |
| `POST /mealplans/children/{childId}/meals` | `200` | `400`, `401`, `403`, `404` | `400` for invalid name/icon/color; `403` for the child (`Rate` tier) attempting to create; `404` for no guardian-child relationship. |
| `GET /mealplans/children/{childId}/meals` | `200` | `401`, `403`, `404` | `403` is declared but not currently reachable (`CheckView` never returns `Forbidden`); `404` for no guardian-child relationship. |
| `PATCH /mealplans/children/{childId}/meals/{mealId}/details` | `200` | `400`, `401`, `403`, `404` | `400` for invalid field values; `403` for the child attempting to edit; `404` for no relationship, unknown/archived meal, or a meal outside the caller's family. |
| `DELETE /mealplans/children/{childId}/meals/{mealId}` | `204` | `401`, `403`, `404` | `403` for the child attempting to archive; `404` for no relationship, unknown meal, or a meal outside the caller's family. |
| `PUT /mealplans/children/{childId}/meals/{mealId}/rating` | `200` | `400`, `401`, `403`, `404` | `400` for an out-of-range rating; `403` for a guardian (`Manage` tier) attempting to rate -- only the child may rate; `404` for no relationship or a meal outside the caller's family. |
| `PUT /mealplans/children/{childId}/plan?date=...&slot=...` | `200` | `400`, `401`, `403`, `404` | `400` for validation failure or assigning an archived meal; `403` for the child attempting to write; `404` for no relationship or a meal outside the caller's family. |
| `DELETE /mealplans/children/{childId}/plan?date=...&slot=...` | `204` | `401`, `403`, `404` | Idempotent: clearing an already-empty slot still returns `204`; `403` for the child attempting to write. |
| `GET /mealplans/children/{childId}/plan?from=...&to=...` | `200` | `400`, `401`, `404` | `400` for an invalid or out-of-range `from`/`to`; no `403` is declared on this route -- `CheckView` never returns `Forbidden`. |
| `PUT /mealplans/children/{childId}/slot-times` | `204` | `401`, `403`, `404` | `403` for the child attempting to configure default meal times. |
| `POST /mealplans/children/{childId}/ical-tokens` | `200` | `401`, `403`, `404` | `403` for the child attempting to mint a subscription token. |
| `GET /mealplans/children/{childId}/ical-tokens` | `200` | `401`, `403`, `404` | `403` for the child attempting to list issued tokens. |
| `DELETE /mealplans/children/{childId}/ical-tokens/{tokenId}` | `204` | `401`, `403`, `404` | Idempotent-style revoke; `403` for the child attempting to revoke a token. |
| `GET /mealplans/{mealPlanId}/ical/{token}` | `200` | `404` | Anonymous; a missing plan and a wrong/revoked token both collapse to `404`. |
| `PUT /mealplans/children/{childId}/plan/groups/{groupId}` | `204` | `401`, `403`, `404` | `403` for the child, or for a caller lacking `Manage` role on the target group -- sharing needs both sides' consent; `404` for no relationship or no group access. |
| `DELETE /mealplans/children/{childId}/plan/groups/{groupId}` | `204` | `401`, `403`, `404` | Idempotent unshare from a group not currently shared with; `403` for the child attempting to unshare. |
| `GET /mealplans/children/{childId}/plan/groups` | `200`, `204` | `401`, `403`, `404` | `403` for the child attempting to view sharing status. `200` with the group, or `204` when nothing is shared. |
| `GET /mealplans/groups/{groupId}/plan` | `200` | `400`, `401`, `404` | `400` for an invalid/out-of-range `from`/`to`; no `403` is declared -- group `View` access never returns `Forbidden`. |
| `PUT /mealplans/groups/{groupId}/plan` | `200` | `400`, `401`, `403`, `404` | `403` for a `View`-tier group member attempting to write; `404` for no group access or a plan not shared with the group. |
| `DELETE /mealplans/groups/{groupId}/plan` | `204` | `401`, `403`, `404` | `403` for a `View`-tier group member attempting to write; `404` for no group access or a plan not shared with the group. |
| `GET /mealplans/groups/{groupId}/meals` | `200` | `401`, `404` | `404` for no group access or a plan not shared with the group; no `403` is declared on this route. |
| `POST /mealplans/groups/{groupId}/meals` | `200` | `400`, `401`, `403`, `404` | `403` for a `View`-tier group member; `404` for no group access or an unshared plan. |
| `PATCH /mealplans/groups/{groupId}/meals/{mealId}/details` | `200` | `400`, `401`, `403`, `404` | `403` for a `View`-tier group member; `404` for no group access, an unshared plan, or a meal outside the family. |
| `DELETE /mealplans/groups/{groupId}/meals/{mealId}` | `204` | `401`, `403`, `404` | `403` for a `View`-tier group member attempting to archive; `404` for no group access, an unshared plan, or unknown meal. |

Note: `PUT /groups/{groupId}/mealplan-permission-policy` is documented under the Groups API above, not here.

### Medicines API (`/medicines`)

| Endpoint | Success | Client error statuses | When to use |
| --- | --- | --- | --- |
| `POST /medicines/children/{childId}/schedules` | `200` | `400`, `401`, `403`, `404` | `400` for invalid schedule fields; `403` for a caller without `Manage` tier; `404` for no guardian-child relationship. |
| `GET /medicines/children/{childId}/schedules` | `200` | `401`, `403`, `404` | `403` for a caller without `Manage` tier (e.g. the child); `404` for no guardian-child relationship. |
| `PATCH /medicines/children/{childId}/schedules/{medicineId}/details` | `200` | `400`, `401`, `403`, `404` | `400` for invalid field values; `403` for a caller without `Manage` tier; `404` for no relationship or unknown medicine. |
| `PATCH /medicines/children/{childId}/schedules/{medicineId}/schedule` | `200` | `400`, `401`, `403`, `404` | `400` for invalid times or date range; `403` for a caller without `Manage` tier; `404` for no relationship or unknown medicine. |
| `DELETE /medicines/children/{childId}/schedules/{medicineId}` | `204` | `401`, `403`, `404` | `403` for a caller without `Manage` tier; `404` for no relationship or unknown medicine. |
| `GET /medicines/children/{childId}/doses?from=...&to=...` | `200` | `400`, `401`, `404` | `400` for an invalid `from`/`to` range; no `403` is declared on this route. |
| `PUT /medicines/children/{childId}/doses/{medicineId}?date=...&time=...` | `200` | `400`, `401`, `403`, `404` | `400` for an invalid status value; `403` for a caller without `Mark` tier; `404` for no relationship, unknown medicine, or no dose at that date/time. |
| `PUT /medicines/children/{childId}/group-share/{groupId}` | `204` | `401`, `403`, `404` | `403` for a caller without `Manage` tier; `404` for no relationship or an ineligible group. |
| `DELETE /medicines/children/{childId}/group-share/{groupId}` | `204` | `401`, `403`, `404` | `403` for a caller without `Manage` tier; `404` for no relationship or no active share with that group. |
| `GET /medicines/children/{childId}/group-share` | `200`, `204` | `401`, `403`, `404` | `403` for a caller without `Manage` tier; `404` for no guardian-child relationship. `204` when nothing is shared. |
| `POST /medicines/groups/{groupId}/children/{childId}/schedules` | `200` | `400`, `401`, `403`, `404` | `403` for a group caller without `Manage`-tier medicine policy -- there is no `View` tier; `404` for a child not shared with the group. |
| `GET /medicines/groups/{groupId}/children/{childId}/schedules` | `200` | `401`, `403`, `404` | `403` for a group caller without `Manage`-tier policy; `404` for a child not shared with the group. |
| `PATCH /medicines/groups/{groupId}/children/{childId}/schedules/{medicineId}/details` | `200` | `400`, `401`, `403`, `404` | `403` for a group caller without `Manage`-tier policy; `404` for a child not shared with the group or unknown medicine. |
| `PATCH /medicines/groups/{groupId}/children/{childId}/schedules/{medicineId}/schedule` | `200` | `400`, `401`, `403`, `404` | `403` for a group caller without `Manage`-tier policy; `404` for a child not shared with the group or unknown medicine. |
| `DELETE /medicines/groups/{groupId}/children/{childId}/schedules/{medicineId}` | `204` | `401`, `403`, `404` | `403` for a group caller without `Manage`-tier policy; `404` for a child not shared with the group or unknown medicine. |
| `GET /medicines/groups/{groupId}/children/{childId}/doses?from=...&to=...` | `200` | `400`, `401`, `403`, `404` | `400` for an invalid `from`/`to` range; `403` for a group caller without `Manage`-tier policy; `404` for a child not shared with the group. |
| `PUT /medicines/groups/{groupId}/children/{childId}/doses/{medicineId}?date=...&time=...` | `200` | `400`, `401`, `403`, `404` | `403` for a group caller without `Manage`-tier policy; `404` for a child not shared with the group, unknown medicine, or no dose at that date/time. |

Note: `PUT /groups/{groupId}/medicine-permission-policy` is documented under the Groups API above, not here. `MedicinePermissionPolicy` is all-or-nothing (`None`/`Manage`) -- there is no group-level `View` tier for medicines.

### Pickups API (`/pickups`)

| Endpoint | Success | Client error statuses | When to use |
| --- | --- | --- | --- |
| `PUT /pickups/children/{childId}/assignments?date=...&slot=...` | `200` | `400`, `401`, `403`, `404` | `400` for invalid assignee-kind fields; `403` for a child attempting a write; `404` for no relationship to the child (privacy-hiding). |
| `DELETE /pickups/children/{childId}/assignments?date=...&slot=...` | `204` | `401`, `403`, `404` | Idempotent: clearing an already-empty slot still returns `204`; `403` for a child attempting a write. |
| `GET /pickups/children/{childId}/schedule?from=...&to=...` | `200` | `400`, `401`, `404` | `400` for a date range longer than 31 days or otherwise invalid. |

A babysitter assignee (`kind` 4) is also `400` on `PUT` when its `guardianId` isn't an active guardian of the child or its `babysitterId` isn't an active babysitter on that guardian's list.

### Babysitters API (`/babysitters`)

| Endpoint | Success | Client error statuses | When to use |
| --- | --- | --- | --- |
| `GET /babysitters/me` | `200` | `401`, `403` | The caller's own list, archived babysitters included and flagged; `403` for a child account. |
| `POST /babysitters/me` | `200` | `400`, `401`, `403` | `400` for a blank/oversized name, oversized contact info, a name already used by an active babysitter, or a 21st active babysitter; `403` for a child account. |
| `PATCH /babysitters/me/{babysitterId}` | `200` | `400`, `401`, `403`, `404` | `404` for an unknown or archived babysitter; unchanged details are an idempotent `200` with no event. |
| `DELETE /babysitters/me/{babysitterId}` | `204` | `401`, `403`, `404` | Archives (never deletes). Idempotent on an already-archived babysitter; `404` for an unknown one. |
| `GET /babysitters/children/{childId}` | `200` | `401`, `404` | Active babysitters of every active guardian of the child; `404` unless the caller is an active guardian of the child (the child included). |

### Work Locations API (`/work-locations`)

| Endpoint | Success | Client error statuses | When to use |
| --- | --- | --- | --- |
| `POST /work-locations/me/locations` | `200` | `400`, `401`, `403` | `400` for blank/oversized name, icon or color, a name already used by an active location, or a 13th active location; `403` for a child account. |
| `PATCH /work-locations/me/locations/{locationId}` | `200` | `400`, `401`, `403`, `404` | `404` for an unknown or archived location; unchanged details are an idempotent `200` with no event. |
| `DELETE /work-locations/me/locations/{locationId}` | `204` | `401`, `403`, `404` | Archives (never deletes). Idempotent on an already-archived location; `404` for an unknown one. |
| `PUT /work-locations/me/pattern` | `200` | `400`, `401`, `403` | `400` for a cycle outside 1–4 weeks, an anchor that isn't a Monday, a day outside the cycle, two entries for one week+weekday, or an unknown/archived location. |
| `PUT /work-locations/me/overrides` | `200` | `400`, `401`, `403` | `400` for an invalid range (`to - from` over 31 days, or backwards) or an unknown/archived location. Returns the resolved days for the range. |
| `DELETE /work-locations/me/overrides?from=...&to=...` | `204` | `400`, `401`, `403` | Idempotent: clearing a range with no overrides still returns `204`. |
| `GET /work-locations/guardians/{guardianId}` | `200` | `401`, `404` | `404` unless the caller is that guardian or a co-guardian; children always get `404`. |
| `GET /work-locations/guardians/{guardianId}/days?from=...&to=...` | `200` | `400`, `401`, `404` | Same access as above; `400` for an invalid range. |

### Print Templates API (`/print-templates`)

| Endpoint | Success | Client error statuses | When to use |
| --- | --- | --- | --- |
| `POST /print-templates` | `200` | `400`, `401`, `403`, `404` | `400` for a blank or over-80-character name; `403` for a child account; `404` for a `groupId` the caller isn't a guardian member of. |
| `GET /print-templates` | `200` | `401` | The caller's own templates plus their groups'; a child gets an empty list. |
| `GET /print-templates/{templateId}` | `200` | `401`, `404` | `404` outside the Manage tier (not the owner / not a guardian member of the owning group), for children, and for deleted templates. |
| `PATCH /print-templates/{templateId}/name` | `200` | `400`, `401`, `404` | `400` for an invalid name; unchanged names are an idempotent `200`. |
| `PATCH /print-templates/{templateId}/layout` | `200` | `400`, `401`, `404` | `400` for an unknown paper size or weekday; unchanged layout is an idempotent `200`. |
| `PUT /print-templates/{templateId}/rows` | `200` | `400`, `401`, `404` | `400` for 0 or over 12 rows, a row missing what its kind needs or setting fields its kind doesn't use, or a reference the caller can't reach (child, group, calendar, guardian, work location). |
| `PUT /print-templates/{templateId}/colors` | `200` | `400`, `401`, `404` | `400` for two colors for one guardian, a blank color, or a guardian who isn't the caller or a co-guardian. |
| `DELETE /print-templates/{templateId}` | `204` | `401`, `404` | A second delete is `404`: a deleted template is treated as missing. |

### Progress API (`/progress`)

| Endpoint | Success | Client error statuses | When to use |
| --- | --- | --- | --- |
| `GET /progress/me` | `200` | `401` | Always resolves to the caller's own progress; no existence-hiding case applies. |
| `GET /progress/children/{childId}` | `200` | `401`, `404` | `404` for unknown child or caller without an active guardian link (and not the child themself). |

There is no write endpoint in this API: star awarding happens internally via `RecordStarChange`, invoked from the task-completion handler, and is never exposed over HTTP.

### Task Library API (`/task-templates`, plus `/calendars/{calendarId}/items/from-template`)

| Endpoint | Success | Client error statuses | When to use |
| --- | --- | --- | --- |
| `POST /task-templates/children/{childId}` | `200` | `400`, `401`, `403`, `404` | `400` for invalid name/icon/color; `403` for the child attempting to create a template; `404` for a caller with no relationship to the child. |
| `GET /task-templates/children/{childId}` | `200` | `401`, `403`, `404` | `404` for a caller with no relationship to the child. |
| `PATCH /task-templates/{templateId}` | `200` | `400`, `401`, `403`, `404` | `400` for invalid name/icon/color; `403` for the child attempting to edit; `404` for unknown template or no relationship. |
| `DELETE /task-templates/{templateId}` | `204` | `401`, `403`, `404` | `403` for the child attempting to archive; `404` for unknown template or no relationship. |
| `POST /task-templates/{templateId}/subtasks` | `200` | `400`, `401`, `403`, `404` | `400` for invalid title/duration or requested position; `403` for the child attempting to add a subtask; `404` for unknown template or no relationship. |
| `PATCH /task-templates/{templateId}/subtasks/{subtaskId}` | `200` | `400`, `401`, `403`, `404` | `400` for invalid title/duration; `403` for the child attempting to edit; `404` for unknown template/subtask or no relationship. |
| `DELETE /task-templates/{templateId}/subtasks/{subtaskId}` | `204` | `401`, `403`, `404` | `403` for the child attempting to remove; `404` for unknown template/subtask or no relationship. |
| `PUT /task-templates/{templateId}/subtasks/order` | `200` | `400`, `401`, `403`, `404` | `400` when the new order omits or duplicates a current subtask ID; `403` for the child attempting to reorder; `404` for unknown template or no relationship. |
| `POST /calendars/{calendarId}/items/from-template` | `200` | `400`, `401`, `403`, `404` | `400` for an empty or archived template; `403` for a caller without contributor access to the calendar; `404` for unknown calendar/template or a template not owned by the assignee or caller. |

## OpenAPI Alignment Checklist

Use this checklist whenever endpoint contracts change.

- Route and verb: ensure method/path in this document matches the endpoint mapping.
- Success codes: verify documented success code matches endpoint result type and behavior.
- Error codes: verify all declared client error outcomes are documented with semantics.
- Auth behavior: confirm protected endpoints include `401`; anonymous endpoints do not.
- Privacy behavior: confirm existence-hiding rules (`404` vs `403`) are still accurate.
- Validation behavior: keep `400` vs `422` usage consistent with team convention.
- OpenAPI docs: confirm endpoint response metadata in generated OpenAPI matches this guide.
- Review trigger: for each PR touching endpoint files, update this document in the same PR.

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
e.g. a resend-cooldown rejection). `requestId` is `HttpContext.TraceIdentifier`.
`NotFound`/`Forbidden` outcomes are unaffected — they keep their existing,
endpoint-specific mappings (some deliberately collapse `Forbidden` into `404`
for privacy). Keep the schema stable for clients.

A request body that can't be bound renders through the same envelope with
`code: "validation_error"` (`buddy.Common.Validation.RequestBindingFailureMiddleware`,
enabled by `RouteHandlerOptions.ThrowOnBadRequest`). The HTTP JSON options set
`RespectRequiredConstructorParameters` and `RespectNullableAnnotations`, so an
omitted request-record parameter without a default, or `null` for a non-nullable
one, is rejected before the handler runs. Here `details` keys are the JSON path
of the field (`name`, `days[0].locationId`), or `""` for malformed JSON.
Optional request fields are nullable with a `= null` default.
