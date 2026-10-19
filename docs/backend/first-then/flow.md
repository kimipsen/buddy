# First–Then Flow

A guardian pairs a task the child finds hard with something they look forward to ("first homework,
then iPad"). The child sees both cards, taps _Done_ on the first, and the second opens, with an
optional countdown. Design: [first-then-board.md](../analysis/first-then-board.md).

One aggregate in the `firstthen` schema:

- `FirstThenBoard`: one stream per child, with `FirstThenBoardId.Value == ChildId.Value` (no index
  document, same as `SleepDiary` and `FeelingsJournal`), started lazily by the first set. It holds
  the current board (`State` `None` / `Waiting` / `Unlocked`, the two `FirstThenCard`s, an optional
  `ThenMinutes`, `UnlockedAt`), a `Revision` that goes up on every set, and up to eight recently
  used cards per side, folded from the `FirstThenBoardSet` events. A card is a copy (title and
  emoji), never a reference to a task or a reward.

Lifecycle: `FirstThenBoardSet` → `Waiting`; `FirstThenFirstCompleted` → `Unlocked` (stamps
`UnlockedAt`); `FirstThenFirstReopened` → back to `Waiting`; `FirstThenBoardEnded` → `None`. A set
replaces whatever board is current. Completing an `Unlocked` board, reopening a `Waiting` one and
ending a `None` one succeed without appending; completing or reopening with no board is `404`.
Nothing ends a board automatically: the child's pages hide a board last changed before today.

Every write carries the `revision` the caller was shown. A mismatch is `409`
`first_then_board_changed`, checked before the state, so a child's tick can't land on a board a
guardian has just replaced.

Two tiers. **Own**: the caller is a child with at least one active guardian link, on `/me` routes
that only reach their own board. **Manage**: any active guardian, on `/children/{childId}`
routes; the child gets `403` there, anyone else `404`.

```mermaid
sequenceDiagram
    actor Guardian
    actor Child
    participant App as Client app
    participant API as Buddy API
    participant F as FirstThen feature
    participant Guardians as Guardian links
    participant Store as FirstThen event store

    Guardian->>App: /guardian/first-then, pick Homework and iPad, 20 min
    App->>API: PUT /first-then/children/{id} {revision, first, then, thenMinutes}
    API->>F: SetFirstThenBoard
    F->>Guardians: FindActiveLinkAsync(child, caller) (else 404; the child itself 403)
    F->>Store: FirstThenBoardStarted (lazily) + FirstThenBoardSet (409 on a stale revision)
    API-->>App: 200 the board

    Child->>App: /child (card on top), /child/first-then
    App->>API: GET /first-then/me (every 60 s and on wake)
    Child->>App: Done!
    App->>API: POST /first-then/me/first/complete {revision}
    F->>Guardians: ListForChildAsync(caller) (a child with a guardian, else 404)
    F->>Store: FirstThenFirstCompleted (UnlockedAt drives the countdown)
    API-->>App: 200 the board, Unlocked

    Guardian->>App: End board
    App->>API: POST /first-then/children/{id}/end {revision}
    F->>Store: FirstThenBoardEnded
```

## Endpoints

| Method | Route                                                          | Behavior                                                                                                         |
| ------ | -------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------- |
| `GET`  | `/first-then/children/{childId}`                               | The board plus the recent cards (`GuardianFirstThenResponse`); `State = None, Revision = 0` before the first set |
| `PUT`  | `/first-then/children/{childId}`                               | Set (or replace) the board                                                                                       |
| `POST` | `/first-then/children/{childId}/first/complete`                | `Waiting` → `Unlocked`; no-op when already unlocked; `404` with no board                                         |
| `POST` | `/first-then/children/{childId}/first/reopen`                  | `Unlocked` → `Waiting`; no-op when waiting; `404` with no board                                                  |
| `POST` | `/first-then/children/{childId}/end`                           | → `None`; no-op with no board                                                                                    |
| `GET`  | `/first-then/me`                                               | The child's own board (no recent cards)                                                                          |
| `POST` | `/first-then/me/first/complete`, `/me/first/reopen`, `/me/end` | The same actions on the child's own board                                                                        |

The actions are idempotent by design, so they take no `Idempotency-Key`. All routes are mapped only
when `Features:FirstThen` is on (`404` otherwise).

## Validation

Every rule the endpoints enforce, validator rules first, then the handler's own checks.
A `400 validation_error` carries the message in the `ErrorEnvelope`'s `details` under the
key in the Field column, `""` for a check that isn't tied to one field (see
[HTTP status codes](../http-status-codes.md#error-response-shape-implemented)). A body
that can't be bound is rejected before these rules run, with the same envelope.

A card's keys repeat the side (`First.first.title`): the card validator overrides its property
names with `first.title` / `then.title`, and FluentValidation still prefixes the parent property.

| Endpoint                                                                                                                                           | Field                                  | Rule                                                                                                                                                                                                                                            | Code                       | Status |
| -------------------------------------------------------------------------------------------------------------------------------------------------- | -------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | -------------------------- | ------ |
| `SetFirstThenBoard`                                                                                                                                | `Revision`                             | At least 0.                                                                                                                                                                                                                                     | `validation_error`         | 400    |
| `SetFirstThenBoard`                                                                                                                                | `First.first.title`, `Then.then.title` | Required: not empty after trimming. At most 60 characters after trimming. A First and a Then may share a title.                                                                                                                                 | `validation_error`         | 400    |
| `SetFirstThenBoard`                                                                                                                                | `First.first.icon`, `Then.then.icon`   | Required: not empty after trimming. At most 64 characters; a value starting with `pictogram:` must continue with a lowercase, hyphenated slug (`[a-z0-9]+(-[a-z0-9]+)*`); any other value (an emoji) at most 16 characters (UTF-16 code units). | `validation_error`         | 400    |
| `SetFirstThenBoard`                                                                                                                                | `ThenMinutes`                          | Between 5 and 120 inclusive. Optional (null means no countdown).                                                                                                                                                                                | `validation_error`         | 400    |
| `SetFirstThenBoard`                                                                                                                                | —                                      | `revision` must equal the current board's revision (0 before the first set). Checked after authorization.                                                                                                                                       | `first_then_board_changed` | 409    |
| `CompleteFirstThenFirst`, `CompleteMyFirstThenFirst`, `ReopenFirstThenFirst`, `ReopenMyFirstThenFirst`, `EndFirstThenBoard`, `EndMyFirstThenBoard` | `Revision`                             | At least 0.                                                                                                                                                                                                                                     | `validation_error`         | 400    |
| `CompleteFirstThenFirst`, `CompleteMyFirstThenFirst`, `ReopenFirstThenFirst`, `ReopenMyFirstThenFirst`, `EndFirstThenBoard`, `EndMyFirstThenBoard` | —                                      | `revision` must equal the current board's revision (0 with no board). Checked after authorization and before the board's state.                                                                                                                 | `first_then_board_changed` | 409    |

## Privacy

`FirstThenPersonalData` deletes the child's stream when the child is erased. Erasing a guardian
writes nothing: the events' `SetBy`, `CompletedBy`, `ReopenedBy` and `EndedBy` keep the user id,
which is a pseudonym once the user is gone, as for sleep diary entries; the board stays for the
other guardians. The export has a `firstThen` section per child: the
current board and the recent cards.
