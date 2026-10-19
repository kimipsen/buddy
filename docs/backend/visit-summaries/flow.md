# Visit Summaries Flow

A guardian prepares a doctor's visit: a title, an optional visit date, a period of at most 92 days,
the sections the report shows (medicine, sleep, feelings) and their own questions for the doctor.
The report itself is never stored. It is read live from the sleep diary, medicines and feelings
every time the guardian opens it or a doctor opens its share link, so a corrected night shows up
in the report. Design: [doctor-visit-summary.md](../analysis/doctor-visit-summary.md).

One aggregate in the `visitsummaries` schema:

- `VisitSummary`: one stream per prepared visit, with a UUIDv7 `VisitSummaryId`, its
  `VisitSummaryDetails` (overwritten whole) and its share links. The links live on the summary
  stream, so deleting the summary revokes them all in one event. `VisitSummaryIndexDocument` lists a
  child's summaries; `VisitSummaryShareLinkDocument` finds a link by its token hash
  (`Common/Security/ShareSecret`), kept in step with the stream in the same transaction.

The report comes from `IVisitSummarySectionSource`, implemented by `SleepDiaryVisitSummarySource`,
`MedicineVisitSummarySource` and `FeelingsVisitSummarySource` in their own features, each returning
that feature's own response DTO. `VisitSummaryReports` asks the sources the summary chose whose
feature is on. A child's own check-in notes are blanked, and a medicine stopped during the period
keeps its taken and skipped doses (never invented pending ones).

One tier, **Manage**: any active guardian of the child. The child, group members and everyone else
get `404`. The doctor reads one summary through a live link.

```mermaid
sequenceDiagram
    actor Guardian
    actor Doctor
    participant App as Client app
    participant API as Buddy API
    participant V as VisitSummaries feature
    participant Guardians as Guardian links
    participant Store as VisitSummary event store
    participant Sources as Sleep, Medicine, Feelings sources

    Guardian->>App: New summary: title, period, sections, questions
    App->>API: POST /visit-summaries/children/{childId} (Idempotency-Key)
    API->>V: CreateVisitSummary
    V->>Guardians: FindActiveLinkAsync (else 404)
    V->>Store: VisitSummaryCreated + index document
    Guardian->>App: Open the summary
    App->>API: GET /visit-summaries/children/{childId}/{summaryId} (logged 12004)
    V->>Sources: ReadAsync(child, from, to) for each chosen, enabled section
    API-->>App: the summary and its report
    Guardian->>App: Create link
    App->>API: POST .../{summaryId}/share-links (logged 12001)
    V->>Store: VisitSummaryShareLinkCreated + link document
    Doctor->>App: /shared/visit-summary/{token}
    App->>API: GET /visit-summaries/shared/{token} (anonymous, rate limited; logged 12003)
    V->>Store: link document by hash, then the summary snapshot
    V->>Sources: ReadAsync for the fixed period
    API-->>App: the report and the link's expiry
```

## Endpoints

| Method   | Route                                                                  | Behavior                                                                                                                                                                                       |
| -------- | ---------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `POST`   | `/visit-summaries/children/{childId}`                                  | `title` (1-100), optional `visitDate`, `from`/`to` (at most 92 days), `sections` (at least one), optional `note` (at most 4000). Returns the summary.                                          |
| `GET`    | `/visit-summaries/children/{childId}`                                  | The child's summaries with their live links, newest visit first (a summary without a visit date by its period's end). No report, so no health data is read.                                    |
| `GET`    | `/visit-summaries/children/{childId}/{summaryId}`                      | The summary and its report. A deleted summary, or one of another child, is `404`.                                                                                                              |
| `PUT`    | `/visit-summaries/children/{childId}/{summaryId}`                      | Full overwrite of the details, same rules as create. The same details append nothing.                                                                                                          |
| `DELETE` | `/visit-summaries/children/{childId}/{summaryId}`                      | Soft delete; every link stops working. Idempotent.                                                                                                                                             |
| `POST`   | `/visit-summaries/children/{childId}/{summaryId}/share-links`          | Optional `expiresAt` (future, at most 365 days). At most 5 live links per summary (`400` beyond). Returns the plaintext token once.                                                            |
| `DELETE` | `/visit-summaries/children/{childId}/{summaryId}/share-links/{linkId}` | Revokes a link. Idempotent.                                                                                                                                                                    |
| `GET`    | `/visit-summaries/shared/{token}`                                      | Anonymous, `visit-summary-share` rate limit (30 per 10 minutes per token). The report for the summary's own period and the link's expiry. Unknown, revoked, expired or deleted summary: `404`. |

All routes are mapped only while `Features:VisitSummaries` is on, which follows its three sources:
it is off when the sleep diary, medicines and feelings are all off. Erasing a child deletes their
summaries and link documents; erasing a guardian revokes the links they created (the summaries
stay with a co-guardian). The export's `visitSummaries` section lists each child's summaries and
links (never a token hash, and no report).

## Validation

Every rule the endpoints enforce, validator rules first, then the handler's own checks.
A `400 validation_error` carries the message in the `ErrorEnvelope`'s `details` under the
key in the Field column, `""` for a check that isn't tied to one field (see
[HTTP status codes](../http-status-codes.md#error-response-shape-implemented)). A body
that can't be bound is rejected before these rules run, with the same envelope.

| Endpoint                                   | Field       | Rule                                                                                                           | Code               | Status |
| ------------------------------------------ | ----------- | -------------------------------------------------------------------------------------------------------------- | ------------------ | ------ |
| `CreateVisitSummary`, `UpdateVisitSummary` | `title`     | Required: not empty after trimming (a missing `title` counts as empty). At most 100 characters after trimming. | `validation_error` | 400    |
| `CreateVisitSummary`, `UpdateVisitSummary` | `note`      | At most 4000 characters after trimming. Optional.                                                              | `validation_error` | 400    |
| `CreateVisitSummary`, `UpdateVisitSummary` | `sections`  | At least one section, after duplicates are dropped (a missing `sections` counts as empty).                     | `validation_error` | 400    |
| `CreateVisitSummary`, `UpdateVisitSummary` | `sections`  | Every value a defined `VisitSummarySection` (`Medicine`, `Sleep`, `Feelings`).                                 | `validation_error` | 400    |
| `CreateVisitSummary`, `UpdateVisitSummary` | `to`        | Not before `from`.                                                                                             | `validation_error` | 400    |
| `CreateVisitSummary`, `UpdateVisitSummary` | `to`        | The period `from`–`to` spans at most 92 days, both ends included.                                              | `validation_error` | 400    |
| `CreateVisitSummaryShareLink`              | `expiresAt` | When given: in the future. Optional.                                                                           | `validation_error` | 400    |
| `CreateVisitSummaryShareLink`              | `expiresAt` | When given: at most 365 days from now.                                                                         | `validation_error` | 400    |
| `CreateVisitSummaryShareLink`              | `""`        | At most 5 live (not revoked, not expired) links per summary.                                                   | `validation_error` | 400    |
