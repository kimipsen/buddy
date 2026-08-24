# Users Flow

The users feature authenticates requests with Keycloak and uses the authenticated Keycloak subject as the stable identity for a local, event-sourced user. The first request for a subject creates a local user from the token claims. Later requests rehydrate that user from its event stream.

```mermaid
sequenceDiagram
    actor User
    participant App as Client app
    participant Keycloak
    participant API as Buddy API
    participant Users as Users feature
    participant Store as User event store
    participant Email as Email sender

    User->>App: Open app
    App->>Keycloak: Start login
    Keycloak-->>App: Return access token
    App->>API: GET /users/me with bearer token
    API->>API: Validate JWT using Keycloak authority
    API->>Users: Dispatch GetOrCreateUser message
    Users->>Store: Read events for Keycloak subject

    alt New user
        Store-->>Users: No user events
        Users->>Users: Build UserCreated from token claims
        Users->>Store: Append UserCreated event
        opt Email is unverified
            Users->>Store: Append EmailVerificationRequested event
            Users->>Email: Send verification email
        end
        Users->>Users: Rehydrate user from initial events
        Users-->>API: Created local user
        API-->>App: 200 OK with user profile
    else Returning user
        Store-->>Users: Existing user events
        Users->>Users: Rehydrate existing user
        Users-->>API: Existing local user
        API-->>App: 200 OK with user profile
    end
```

## Endpoints

All users endpoints require a bearer token issued by the configured Keycloak authority and are included in the `users` OpenAPI document.

| Method | Route | Behavior |
| --- | --- | --- |
| `GET` | `/users/me` | Gets or creates the local user for the authenticated Keycloak subject. Returns `404 Not Found` when the user has been deleted. |
| `GET` | `/users/me/events` | Returns a page of the persisted event history for the authenticated subject, oldest first. Accepts optional `cursor` and `pageSize` (default 50, max 200) query parameters; an unknown subject has an empty event history. |
| `PATCH` | `/users/me/name` | Updates the authenticated user's given and family name. Returns the updated profile, or `404 Not Found` when no local user exists. |
| `PATCH` | `/users/me/email` | Changes the authenticated user's email and starts email verification; submitting the current email is a no-op. Returns the updated profile, or `404 Not Found` when no local user exists. |
| `PATCH` | `/users/me/timezone` | Sets the authenticated user's preferred IANA time zone, used to format timestamps for them. Returns the updated profile, `404 Not Found` when no local user exists, or `400 Bad Request` for an unrecognized time zone identifier. |
| `POST` | `/users/me/email/verify/resend` | Sends another verification email for an unverified address. Returns `204 No Content` for a sent email or an already verified address, `404 Not Found` when no local user exists, or `409 Conflict` during the resend cooldown. |
| `POST` | `/users/me/email/verify` | Verifies the email using the submitted token. Returns the updated profile, `404 Not Found`, or `400 Bad Request` for an invalid or expired token. |
| `DELETE` | `/users/me` | Appends `UserDeleted` for an existing, active user. Repeating the request is a no-op and returns `204 No Content`. |

The API passes work to Wolverine handlers rather than accessing the event store directly from the endpoint. The handlers are responsible for looking up the user stream, creating or rehydrating the aggregate, and appending deletion events.

## User Events

The user stream currently supports these event types:

- `UserCreated`
- `UserDeleted`
- `NameUpdated`
- `EmailUpdated`
- `EmailVerificationRequested`
- `EmailVerified`
- `TimeZoneUpdated`

The user profile is built from `UserCreated`; `UserDeleted` marks the rehydrated user as deleted. Changing an email appends `EmailUpdated` and starts a new verification request; submitting the existing email leaves the stream unchanged. Verification requests store only a hash of the token in the event stream; the plaintext token is sent through the configured email sender. `EmailVerified` clears the pending verification state. A user with no `TimeZoneUpdated` event yet implicitly defaults to UTC, the same sparse-log convention Medicines' dose status already uses, so `UserCreated` didn't need to change to carry an initial value. All event types are registered for persistence and are returned by the event-history endpoint.

### Event-history pagination

The event-history endpoint paginates by the event's stream version rather than an offset, so pages stay stable even as new events are appended. The response includes a `nextCursor`, an opaque token wrapping the version of the last returned event; pass it back as the `cursor` query parameter to fetch the next page. `nextCursor` is `null` once the last page has been returned.
