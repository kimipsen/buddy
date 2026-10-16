# Feelings Flow

A child taps how they feel a few times a day: one of four colours, each drawn with a face and a word
(blue "slow", green "ready", yellow "wound up", red "too much"), optionally with up to three words
from a fixed list and a short note. A guardian logs observations on the child's behalf, sees a
14-day trend next to sleep and doses, sets reminder times and a faces-only mode for a young child,
and can share the check-ins with a doctor through a revocable, expiring link. Design:
[feelings-check-in.md](../analysis/feelings-check-in.md).

Two aggregates in the `feelings` schema:

- `FeelingsJournal`: one stream per child, with `FeelingsJournalId.Value == ChildId.Value` (no index
  document, same as `SleepDiary`). Check-ins keyed by a UUIDv7 `FeelingCheckInId`, plus the child's
  prompt times (0-4) and `FeelingsDisplayMode` (`FacesAndWords` or `FacesOnly`). Started lazily by
  the first write. A check-in's `LoggedBy` is the child for a self-report, a guardian for an
  observation; its `LocalDate`/`LocalTime` are the child's wall clock (`User.TimeZoneId`), and
  `RecordedAt` is the instant it was saved.
- `FeelingsShareToken`: a hashed, revocable, optionally expiring link, a copy of
  `SleepDiaryShareToken`, indexed by `FeelingsShareTokenDocument` (hash and child).

Two tiers. **Self**: the caller is a child with at least one active guardian link, on `/me` routes
that only reach their own journal. **Manage**: any active guardian, on `/children/{childId}`
routes; the child gets `403` there, anyone else `404`. "Now" comes from an injected `TimeProvider`.

```mermaid
sequenceDiagram
    actor Child
    actor Guardian
    actor Doctor
    participant App as Client app
    participant API as Buddy API
    participant F as Feelings feature
    participant Guardians as Guardian links
    participant Users as Users (time zone, name)
    participant Store as Feelings event store

    Child->>App: Tap "Wound up", "Worried", Done
    App->>API: POST /feelings/me/check-ins (Idempotency-Key)
    API->>F: CheckIn command
    F->>Guardians: ListForChildAsync(caller) (a child with a guardian, else 404)
    F->>Users: child's TimeZoneId -> local date and time
    F->>Store: FeelingsJournalStarted (lazily) + FeelingCheckedIn (400 past 20 a day)
    API-->>App: 200 the check-in
    Child->>App: Undo (within 10 minutes)
    App->>API: DELETE /feelings/me/check-ins/{id}
    F->>Store: FeelingCheckInRemoved (409 check_in_undo_window_closed after 10 minutes)

    Guardian->>App: /guardian/feelings
    App->>API: GET /feelings/children/{id}/check-ins?from&to (logged 11004)
    App->>API: GET /sleep-diary/... and /medicines/.../doses (each logs its own read)
    Guardian->>App: Create share link
    App->>API: POST /feelings/children/{id}/share-links (logged 11001)
    Doctor->>App: /shared/feelings/{token}
    App->>API: GET /feelings/shared/{token} (anonymous; logged 11003)
```

## Endpoints

| Method   | Route                                                    | Behavior                                                                                                                                                                                                                         |
| -------- | -------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `POST`   | `/feelings/me/check-ins`                                 | The child checks in now: `zone`, optional `tags` (0-3, feeling words only from that zone) and `note` (at most 280). `404` for a caller who isn't a child with an active guardian; `400` on the 21st check-in of the child's day. |
| `DELETE` | `/feelings/me/check-ins/{checkInId}`                     | The child's undo, within 10 minutes of `recordedAt`, else `409 check_in_undo_window_closed`. A guardian's observation is `404`; an id already gone is `204`.                                                                     |
| `GET`    | `/feelings/me/today`                                     | Today's date on the child's clock, the prompt times, the display mode and the child's own self-reports for today (never a guardian's observation).                                                                               |
| `POST`   | `/feelings/children/{childId}/check-ins`                 | A guardian's observation; optional `date` and `time` (both or neither; neither is now), not more than 5 minutes in the future.                                                                                                   |
| `GET`    | `/feelings/children/{childId}/check-ins?from&to`         | A range of at most 92 days, sorted by moment, with the prompt times and display mode.                                                                                                                                            |
| `PUT`    | `/feelings/children/{childId}/check-ins/{checkInId}`     | Full overwrite of a guardian's observation. A child's self-report is `409 check_in_is_self_report`. An identical edit appends nothing.                                                                                           |
| `DELETE` | `/feelings/children/{childId}/check-ins/{checkInId}`     | Removes any check-in, a self-report included; the event records which guardian. Idempotent.                                                                                                                                      |
| `PUT`    | `/feelings/children/{childId}/prompt-times`              | 0-4 distinct whole-minute times, stored sorted. The same times append nothing.                                                                                                                                                   |
| `PUT`    | `/feelings/children/{childId}/display-mode`              | `FacesAndWords` or `FacesOnly`. The same mode appends nothing.                                                                                                                                                                   |
| `POST`   | `/feelings/children/{childId}/share-links`               | Optional `expiresAt` (future, at most 365 days). Returns the plaintext token once.                                                                                                                                               |
| `GET`    | `/feelings/children/{childId}/share-links`               | Live links only.                                                                                                                                                                                                                 |
| `DELETE` | `/feelings/children/{childId}/share-links/{shareLinkId}` | Revokes a link. Idempotent.                                                                                                                                                                                                      |
| `GET`    | `/feelings/shared/{token}?from&to`                       | Anonymous. The child's name and check-ins; default the last 14 days on the child's clock, at most 92. Unknown, revoked, expired or erased: `404`.                                                                                |

All routes are mapped only while `Features:Feelings` is on. Erasing a child deletes the journal and
the share links; erasing a guardian revokes the links they created (their observations stay with
the child's record). The export's `feelings` section lists each child's check-ins, settings and
links (never a token hash).
