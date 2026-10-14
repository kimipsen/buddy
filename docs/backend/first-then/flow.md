# First–Then Flow

A guardian pairs a task the child finds hard with something they look forward to ("first homework,
then iPad"). The child sees both cards, taps *Done* on the first, and the second opens, with an
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

| Method | Route | Behavior |
| --- | --- | --- |
| `GET` | `/first-then/children/{childId}` | The board plus the recent cards (`GuardianFirstThenResponse`); `State = None, Revision = 0` before the first set |
| `PUT` | `/first-then/children/{childId}` | Set (or replace) the board; `400` on titles outside 1–60 characters, a missing emoji or `thenMinutes` outside 5–120 |
| `POST` | `/first-then/children/{childId}/first/complete` | `Waiting` → `Unlocked`; no-op when already unlocked; `404` with no board |
| `POST` | `/first-then/children/{childId}/first/reopen` | `Unlocked` → `Waiting`; no-op when waiting; `404` with no board |
| `POST` | `/first-then/children/{childId}/end` | → `None`; no-op with no board |
| `GET` | `/first-then/me` | The child's own board (no recent cards) |
| `POST` | `/first-then/me/first/complete`, `/me/first/reopen`, `/me/end` | The same actions on the child's own board |

The actions are idempotent by design, so they take no `Idempotency-Key`. All routes are mapped only
when `Features:FirstThen` is on (`404` otherwise).

## Privacy

`FirstThenPersonalData` deletes the child's stream when the child is erased. Erasing a guardian
writes nothing: the events' `SetBy`, `CompletedBy`, `ReopenedBy` and `EndedBy` keep the user id,
which is a pseudonym once the user is gone, as for sleep diary entries; the board stays for the
other guardians. The export has a `firstThen` section per child: the
current board and the recent cards.
