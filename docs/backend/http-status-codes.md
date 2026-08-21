# HTTP Status Code Semantics

This document defines how Buddy backend endpoints should use HTTP status codes.

Scope:
- REST endpoints in the `users` and `calendars` API groups
- authenticated endpoints using bearer tokens
- command-style operations (create, update, delete, verify, resend)

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

Do not use when:
- a new resource was created (use `201`)
- the response intentionally has no body (use `204`)

### 201 Created
Use when a new resource is created successfully.

Requirements:
- include response body with created resource summary where useful
- include `Location` header when canonical URI is known

Typical Buddy use:
- create calendar
- create calendar item
- issue new iCal token

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
- resend email verification during cooldown window
- optimistic concurrency or version mismatch (if surfaced)

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
- optional alternative to `400` for detailed domain validation failures

Team rule:
- choose one style (`400` or `422`) for validation and apply consistently

### 429 Too Many Requests
Use when request rate exceeds limits.

Typical Buddy use:
- auth or verification endpoints under abuse protection

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
   - yes: `201`
8. Did we succeed with no body?
   - yes: `204`
9. Otherwise successful read/update with body
   - `200`

## Suggested Defaults For This Project

- Reads: `200`, `401`, `404`
- Creates: `201`, `400`, `401`, `403`, `404`, `409`
- Updates/Patches: `200` or `204`, plus `400`, `401`, `403`, `404`, `409`
- Deletes: `204`, plus `401`, `403` or `404`
- Verification flows: `204` or `200`, plus `400`, `401`, `404`, `409`, optional `429`

## Endpoint Status Mapping

This section maps current Buddy endpoints to their expected status codes.

Notes:
- `401` applies to all endpoints protected by `.RequireAuthorization()` when token authentication fails.
- Some write endpoints intentionally collapse private-resource visibility into `404` for non-members.
- `500` remains possible for unexpected failures even when omitted from endpoint-level mappings.

### Users API (`/users`)

| Endpoint | Success | Client error statuses | When to use |
| --- | --- | --- | --- |
| `GET /users/me` | `200` | `401`, `404` | `404` when local user is deleted or not available for the authenticated subject. |
| `GET /users/me/events` | `200` | `400`, `401` | `400` for invalid paging cursor or page-size input. |
| `PATCH /users/me/name` | `200` | `401`, `404` | `404` when local user does not exist or is deleted. |
| `PATCH /users/me/email` | `200` | `400`, `401`, `404` | `400` for invalid email payload. |
| `POST /users/me/email/verify/resend` | `204` | `401`, `404`, `409` | `409` during resend cooldown; `204` for already-verified or resend accepted. |
| `POST /users/me/email/verify` | `200` | `400`, `401`, `404` | `400` for invalid/expired token or malformed request. |
| `DELETE /users/me` | `204` | `401` | Idempotent delete: repeated deletes remain `204`. |

### Calendars API (`/calendars`)

| Endpoint | Success | Client error statuses | When to use |
| --- | --- | --- | --- |
| `GET /calendars` | `200` | `401` | Authenticated list of caller-visible calendars. |
| `POST /calendars` | `200` | `400`, `401`, `403` | Current implementation returns `200`; consider `201` if response contract changes. |
| `GET /calendars/{calendarId}` | `200` | `401`, `404` | `404` for unknown/deleted/hidden calendar. |
| `DELETE /calendars/{calendarId}` | `204` | `401`, `403`, `404` | `403` for authenticated non-owner; `404` for unknown/hidden calendar. |
| `PUT or PATCH /calendars/{calendarId}/members/{memberId}` (set role) | `204` | `400`, `401`, `403`, `404` | `400` for invalid role payload; owner-level operation. |
| `DELETE /calendars/{calendarId}/members/{memberId}` | `204` | `401`, `403`, `404` | Removes membership when caller has required permission. |
| `POST /calendars/{calendarId}/items` | `200` | `400`, `401`, `403`, `404` | `400` for invalid schedule/recurrence/details payload. |
| `GET /calendars/{calendarId}/items` | `200` | `401`, `404` | Returns item list for visible calendar. |
| `GET /calendars/{calendarId}/occurrences` | `200` | `400`, `401`, `404` | `400` for invalid `from/to` range or query validation failures. |
| `PATCH /calendars/{calendarId}/items/{itemId}/details` | `200` | `401`, `403`, `404` | `403` for viewer/non-contributor. |
| `PATCH /calendars/{calendarId}/items/{itemId}/schedule` | `200` | `400`, `401`, `403`, `404` | `400` for invalid period/due-date transition rules. |
| `PATCH /calendars/{calendarId}/items/{itemId}/recurrence` | `200` | `400`, `401`, `403`, `404` | `400` for invalid recurrence values. |
| `DELETE /calendars/{calendarId}/items/{itemId}` | `204` | `401`, `403`, `404` | Idempotent deletion behavior recommended. |
| `POST /calendars/{calendarId}/ical-tokens` | `200` | `401`, `403`, `404` | Token issuance for authorized member. |
| `GET /calendars/{calendarId}/ical-tokens` | `200` | `401`, `403`, `404` | Token summary listing for authorized member. |
| `DELETE /calendars/{calendarId}/ical-tokens/{tokenId}` | `204` | `401`, `403`, `404` | Revokes existing token when authorized. |
| `GET /calendars/{calendarId}/ical/{token}` | `200` | `404` | Anonymous endpoint; `404` for unknown calendar or invalid token. |

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

## Error Response Shape (Recommended)

Use a consistent error envelope across endpoints:

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

Consistency matters more than exact field names. Keep the schema stable for clients.
