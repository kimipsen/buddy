# GDPR: erasure, export and data minimization

Status: Implemented in the code. Questions 1-8 ship: masking rules, an `IPersonalDataEraser` and
an `IPersonalDataExporter` per feature (`Common/Erasure`), `UserErasure`, `UserErasureService` and
`PersonalDataExport` (`Features/Privacy`), the `DELETE /users/me` cascade, `UserErased`, the
erasure ledger, `DeleteChild`, the account-deletion preview (`GET /users/me/deletion-preview`), the
export (`GET /users/me/export`), the AI minimization, 30-day retention and data-sharing
acknowledgement, the health-data read audit logs, and their screens. What remains is outside the
code (see "Remaining open questions"). See "Implementation notes" near the end.

## Context

Buddy stores data about children with ADHD and the adults around them: names and email
addresses, medicine schedules and every dose given or missed, nightly sleep entries with free-text
remarks, rewards, calendars, and the contact details of babysitters and playdate hosts. Under the
GDPR, medicines and sleep entries are special-category health data (Art. 9), the data subjects
include children (Art. 8), and every user has the right to get a copy of their data (Art. 15/20)
and to have it erased (Art. 17).

What exists today (all checked in the code):

- **Deleting an account erases nothing.** `DELETE /users/me`
  ([DeleteCurrentUser.Handler.cs](../../../src/backend/buddy/Features/Users/DeleteCurrentUser/DeleteCurrentUser.Handler.cs))
  appends `UserDeleted`, which only sets `User.IsDeleted`. `UserCreated` (Keycloak subject, email,
  username, name) stays in the `users` stream and in the `UserSnapshot`. The front end tells the
  user that deletion "permanently deletes your account" ([admin.ts](../../../src/frontend/buddy/src/app/core/i18n/translations/en/admin.ts)).
- **A deleted user keeps working.**
  [UserIdClaimsTransformation.cs](../../../src/backend/buddy/Features/Users/UserIdClaimsTransformation.cs)
  stamps the `UserId` claim without checking `IsDeleted`. The Keycloak account isn't touched
  ([IKeycloakAdminClient.cs](../../../src/backend/buddy/Features/Guardians/IKeycloakAdminClient.cs)
  can only create users), so the person can sign in again. Only `GET /users/me` and a handful of
  write handlers refuse them.
- **Nothing cascades.** Guardian links, group ownership, calendar roles, invites, share links, AI
  keys and AI sessions all outlive the account. A group whose owner deleted their account can never
  be deleted, because only the owner may do that
  ([DeleteGroup.Handler.cs](../../../src/backend/buddy/Features/Groups/DeleteGroup/DeleteGroup.Handler.cs)).
  A child whose last guardian left keeps all its data with nobody able to reach it.
- **There is no export.** `GET /users/me/events` lists the caller's own user stream only.
- **The AI assistant sends more than it needs**
  ([AiSessionPromptBuilder.cs](../../../src/backend/buddy/Features/Mealplans/AiAssistant/AiSessionPromptBuilder.cs),
  [AiSessionToolExecutor.cs](../../../src/backend/buddy/Features/Mealplans/AiAssistant/AiSessionToolExecutor.cs)):
  - the whole meal library, with every rating comment the children wrote, keyed by child `UserId`;
  - for `get_calendar_conflicts`, the titles of items in *every* calendar the guardian can see,
    including calendars other families share through a group.

  Sessions are kept forever, and the UI never says that any of this leaves Buddy.
- **Two smaller leaks.**
  - The secret iCal and share-link tokens are part of the URL path
    (`/calendars/{id}/ical/{token}`, `/sleep-diary/shared/{token}`), so they end up in traces as
    `url.path` ([ObservabilityFeature.cs](../../../src/backend/buddy/Common/Observability/ObservabilityFeature.cs)).
  - `IdempotencyRecord` keeps full POST response bodies for 24 hours
    ([IdempotencyKeyMiddleware.cs](../../../src/backend/buddy/Common/Idempotency/IdempotencyKeyMiddleware.cs)),
    including the child's temporary password returned by `CreateChild`.

An event store is append-only by design, which is exactly what erasure has to work around.
Marten 9 ships a feature for this, "protected information" masking: per-event-type masking rules
plus `store.Advanced.ApplyEventDataMasking(...)`, which rewrites the stored events of chosen streams
in place.

This document answers eight questions: how erasure works in the event store, what deleting a
guardian account does, what deleting a child does, how groups survive their owner, what an export
contains, what the AI assistant may send and keep, how reads of health data are audited, and what
to fix right away.

## Question 1: how is data erased from an event store?

**Decision: two tools, chosen per stream.**

- **Delete** a stream that belongs entirely to the erased person, along with its snapshot and
  index documents, using `store.Advanced.Clean.DeleteSingleEventStreamAsync`. That covers a
  child's sleep diary and share tokens, medicine schedules and sharing, progress and pickups; a
  guardian's babysitter list, work locations and print templates; and the family-level streams
  of a family with no child left.
- **Mask** with Marten's protected-information masking, using rules for the personal fields of
  each event type, where a stream must remain:
  - the user stream itself, which keeps `IsDeleted` for every lookup that resolves a `UserId`;
  - streams shared with other people: calendar items, groups, invites, and family meals with
    their ratings.

  After masking, rebuild the stream's snapshot and delete or rewrite its lookup documents.

```csharp
// <Domain>Feature.cs, inside the store configuration -- one rule per event type with personal data
options.Events.AddMaskingRuleForProtectedInformation<UserCreated>(e => e with
{
    Email = Email.Unverified(Erased.Text),
    UserName = Erased.Text,
    Name = Name.New(Erased.Text, Erased.Text),
});

// The erasure step for one stream
await store.Advanced.ApplyEventDataMasking(x => x.IncludeStream(userId.Value), cancellationToken);
await store.Advanced.RebuildSingleStreamAsync<UserSnapshot>(userId.Value, cancellationToken);
```

- **Event shapes and stream versions stay intact.** A masked event keeps its type, version and
  ids, so rehydration, the inline snapshots, optimistic concurrency and the golden-file event
  shape tests keep working. Only the personal values become `Erased.Text` (`"[erased]"`), an empty
  list, or `null` where the field allows it.
- **Masking doesn't run projections.** `ApplyAsync` overwrites the events
  (`IEventStoreOperations.OverwriteEvent`) and nothing else, so every erasure step ends with
  `RebuildSingleStreamAsync<TSnapshot>` for the masked streams. Lookup documents that copy personal
  values (`GuardianInviteDocument.InvitedEmail`, `GroupMembershipDocument.GroupName`, ...) are
  deleted or rewritten in the same step.
- **Ids are pseudonyms, not erased.** Other streams reference a person only by `UserId`. Once the
  user's own stream is masked and the Keycloak account is gone, a `UserId` no longer leads to a
  person, so `CreatedBy`, `AssignedBy` and the like stay. Free text a person wrote in a *shared*
  family space (a calendar item title, a meal plan note) stays too: it is the family's plan, not the
  author's personal record. That free text is erased with the child or group it belongs to, not with
  its author.

Masking alone was the first plan. It would leave the substance of a person's own streams: an
erased child's bedtimes and dose statuses, still tied by id to the guardians who logged them.
Masking every field of every event type would fix that, but it would take some 40 rules where
deleting the stream takes one call.

Considered and rejected:

- **Crypto-shredding** (encrypt each person's fields with a per-person key, delete the key).
  Every event type would need encrypted fields and a key lookup on every read and replay, and
  shared streams would mix keys from several people. Masking achieves the same with no read-path
  cost.
- **Deleting every stream, shared ones included.** A calendar holds items from several people,
  and a group outlives any one member.
- **A soft delete with a filter.** That is today's `UserDeleted`, and it erases nothing.

## Question 2: what does deleting a guardian's account do?

**Decision: deleting an account locks the person out at once, then runs an idempotent erasure that
cascades as described below. A background sweeper finishes any erasure that failed halfway.**

1. **Lock out (synchronous, in the request).**
   - Append `UserDeleted`.
   - Mark the `KeycloakIdentity` document `Deleted` in the same session, so
     `UserIdClaimsTransformation` stops stamping the `UserId` claim. Every endpoint then answers
     `403 user_not_provisioned`, and `GetOrCreateUser` refuses to provision the subject again.
2. **Children.** For each child linked to the user:
   - If the user is the child's only active guardian, the child is erased too (Question 3). This
     was the user's decision. The family data that child anchors passes to a sibling who stays,
     the heir; the heirs are chosen before anything is revoked.
   - If the child has other active guardians, the user's own `GuardianLink` is revoked
     (`GuardianRevoked`) and the child stays.
3. **Groups.**
   - A group the user owns passes to a new owner (Question 4).
   - In other groups, the user's role is revoked (`GroupMemberRoleRevoked`), and so are their
     calendar roles (`MemberRoleRevoked`).
4. **Things that are only theirs.**
   - Invites they sent or received (any status) are revoked if pending, and their email and the
     child's first name masked.
   - Sleep diary share links they created are revoked. iCal tokens stay: a feed belongs to the
     shared calendar, holds no personal data, and the other members can revoke it.
   - AI keys they added are removed (`ProviderApiKeyRemoved`), and the encrypted key is masked in
     the history. AI sessions they started are masked.
   - Their print templates are deleted.
   - Their babysitter list and work-location schedule are deleted. Both streams are keyed by the
     guardian's `UserId`, so this also erases the babysitters' names and contact details.
5. **The person.**
   - Delete the Keycloak account through the admin API (`DELETE /admin/realms/buddy/users/{id}`,
     covered by the service account's existing `manage-users` role).
   - Mask the user stream (`UserCreated`, `NameUpdated`, `EmailUpdated`) and rebuild
     `UserSnapshot`, then append `UserErased`, which marks the end of the erasure.
   - Add the user to the erasure ledger (see "Backups").
   - The flagged `KeycloakIdentity` stays: it holds only the subject id of a Keycloak account that
     no longer exists, and it keeps that subject from ever being provisioned again.

A child can't delete their own account: `DELETE /users/me` answers `403` for a child, because the
child's data is their guardians' to erase (Question 3).

Each step is idempotent: revoking a revoked link is a no-op, and masking a masked stream changes
nothing. So the request runs the steps in order, and if any of them throws, the
`UserErasureService` (a `BackgroundService` like
[IdempotencyCleanupService.cs](../../../src/backend/buddy/Common/Idempotency/IdempotencyCleanupService.cs))
picks up every user with `UserDeleted` but no `UserErased` and runs them again. No single database
transaction spans the nine Marten stores and Keycloak, so the process has to be resumable.

The erasure runs in the request, not only in the background, so a user who deletes their account
sees it gone at once. The sweeper is the safety net.

Considered and rejected:

- **A grace period** ("you can undo this within 30 days"). It needs a cancel flow and a scheduled
  erasure, and the UI already promises an immediate, irreversible delete. It can be added later as
  a delay before step 2.
- **Disabling the Keycloak account instead of deleting it.** Keycloak would keep the email and
  name.

## Question 3: deleting a child

**Decision: a new slice, `DeleteChild` (`DELETE /users/me/children/{childId}`), allowed only when
the caller is the child's only active guardian. Otherwise it answers
`409 child_has_other_guardians`. The same erasure also runs when a guardian's account deletion
leaves a child with no guardian (Question 2).**

This follows the user's decision "sole guardian only". With co-guardians, each of them has to give
up their link first (`RevokeGuardianLink`), so no parent can erase what another relies on.
`GuardianKind` doesn't gate it, the same as everywhere else
([GuardianKind.cs](../../../src/backend/buddy/Features/Guardians/Types/GuardianKind.cs)).

What is erased:

| Data | How |
|---|---|
| The child's user stream and `UserSnapshot`; the Keycloak account | mask + rebuild; admin API delete |
| Medicine schedules and dose logs, and their group sharing | delete every stream found through `MedicineIndexDocument`/`MedicineSharingIndexDocument`, with snapshots and index documents |
| Sleep diary (`SleepDiaryId.ForChild`) and share tokens | delete |
| Progress (`ProgressId.ForChild`): stars, milestones, goal-post labels | delete |
| Pickup schedule (playdate host names, addresses and contacts, notes) | delete |
| Meal ratings by the child (`MealRated.Comment`) | mask the comment; the stars stay, keyed by a now-anonymous id |
| Calendar items assigned to the child | `ItemDeleted`, then mask the title |
| Group memberships and calendar roles | revoke |
| Guardian links to the child; guardian invites for the child (which hold `ChildGivenName`) | revoke; mask the invites |

Family-level data -- meals, the meal plan, AI credentials and sessions, task templates -- is
anchored to one child in its index documents (`MealIndexDocument.ChildId`, ...), and
[MealFamilyResolution.cs](../../../src/backend/buddy/Features/Mealplans/MealFamilyResolution.cs)
finds it through the *active* guardian links of the whole family. Once an erased child's links are
revoked, what is anchored to that child would be unreachable for the siblings. So `UserErasure`
picks an heir first: a sibling under the same guardians who isn't being erased. Each eraser
re-anchors the child's index documents to the heir, and the events stay as they are. With no heir,
the family data is deleted with the child (the user's decision "keep while siblings remain").

## Question 4: a group whose owner leaves

**Decision: ownership passes to the longest-standing admin, else the longest-standing member, with
the events that already exist: `GroupMemberRoleGranted` (the successor, as `Owner`), then
`GroupMemberRoleRevoked` (the leaving owner). The group is erased and deleted only when nobody else
is left: its calendars masked and deleted, its name and invites masked, then `GroupDeleted`.**

- **No new event and no change to `Group`.** A group has no separate owner field: the owner is the
  member whose role is `Owner`, so a grant plus a revoke is the transfer. `SetGroupMemberRole`
  refuses to grant `Owner` over the API; the eraser appends the event directly.
- **"Longest-standing"** is the start of a member's current, unbroken membership. The eraser reads
  it from the group's events (`GroupCreated`, then each grant and revoke), so `Group` needs no
  `JoinedAt`.
- **Who can inherit.** Children and deleted users never inherit a group; if only they are left,
  the group is erased.
- **Why.** Other families keep their shared calendars and everything they entered there. This was
  the user's decision; deleting the group, or blocking the deletion until ownership was handed
  over, were the rejected options.

The order inside the erasure makes a rerun safe. Each calendar is erased before `CalendarDeleted`,
which removes the document that finds the calendar. `GroupDeleted` comes last, because it removes
the membership documents that lead the eraser to the group.

## Question 5: what does an export contain?

**Decision: `GET /users/me/export` returns one JSON document with everything about the caller and
about every child they are an active guardian of. Each feature contributes a section through an
`IPersonalDataExporter`. The endpoint has its own rate-limit policy (one export per user every 10
minutes).**

```csharp
public sealed record ExportSubject(UserId UserId, string? Email, IReadOnlyCollection<UserId> Children);

public interface IPersonalDataExporter
{
    Type Store { get; }       // the feature's Marten store, for the coverage meta test
    string Section { get; }   // "account", "children", "medicines", "sleepDiary", ...
    Task<object?> ExportAsync(ExportSubject subject, CancellationToken cancellationToken);
}
```

- **Guardians export their children's data too**, because they exercise the child's rights under
  parental responsibility. A child account can export only its own `account` section.
- **The export holds data, not events**: the current state from the snapshots, plus the history
  that matters to a person (the dose log, the sleep entries). Raw events would expose internal ids
  and masked placeholders.
- It is served with `Content-Disposition: attachment; filename="buddy-export-<date>.json"`. The
  frontend adds a "Download my data" button next to "Delete account".
- Registering exporters like the erasers (one per feature, found by DI) means a new feature that
  stores personal data has a clear place to add its section. `PersonalDataExporterCoverageTests`
  fails for a Marten store without an exporter, the same rule as for erasers.
- **Shared data is limited to what is the caller's.** A shared calendar exports the items the
  caller created or last changed and the items assigned to their children, not every item in it;
  a group exports the caller's and the children's memberships and the invites they sent or
  received, not the other members. Meals, the meal plan, AI keys and AI sessions belong to the
  family (`MealFamilyResolution`), so that section is per family rather than per child.

## Question 6: the AI assistant

**Decision: send less, keep it 30 days, and say so before the first use.**

1. **Minimization.**
   - Children appear as "child 1", "child 2" in the prompt, not by `UserId`.
   - Rating comments are still sent, because they are what makes suggestions useful ("too
     spicy").
   - `get_calendar_conflicts` returns titles only for items assigned to the session's child or to
     nobody, from calendars of the child's own family. Everything else becomes `"busy"` with its
     time.
2. **Retention.** This was the user's decision. A daily job masks `AiSessionStarted.Notes`,
   `AiUserMessageSent.Text`, `AiAssistantMessageRecorded.Text` and both JSON fields of
   `AiToolInvocationRecorded` 30 days after a session is applied or discarded, or 30 days after the
   last activity of a session that was never closed. The meal plan it produced stays.
   `AiSessionIndexDocument` gains `ClosedAt`/`LastActivityAt` for the job's query.
3. **Disclosure.**
   - Before a family's first session, the assistant shows what is sent (meal names, ratings and
     comments, the notes, the chat, calendar times) and to whom (the provider of the family's own
     key).
   - The guardian's acknowledgement is recorded as `AiDataSharingAcknowledged(AiCredentialId,
     UserId, OccurredAt)`, and `StartAiSession` refuses with `409 ai_data_sharing_not_acknowledged`
     until it exists.
   - The provider settings page gets the same text.

The family brings its own key, so the provider is the family's own processor. Buddy's backend is
still the party that assembles the data and sends it, so minimization and disclosure are Buddy's
responsibility.

## Question 7: auditing access to health data

**Decision: structured audit logs for every read of a child's medicines or sleep diary, in the same
form as the existing audit logs ([observability.md](../observability.md)).** New events:

- `ListMedicineSchedules`, `ListTodaysDoses` and their group variants (new `MedicinesLog`, EventIds
  9001+);
- `ListSleepDiaryEntries` (`SleepDiariesLog` 5004).

Each logs the reader's `UserId`, the `ChildId` and the access path (guardian link, group share).
`GetSharedSleepDiary` already logs every view (5003).

A persisted access log that guardians can see in the app ("who viewed my child's data") was
considered and rejected (user's decision): Buddy is meant to be run by each family, which has
someone technical who can read the logs. The logs answer the accountability question (Art. 5(2)),
provided they are kept somewhere: see the OTLP item in the TODO.

## Question 8: what to fix right away

**Decision: four fixes that need no new feature, shipped first.**

- **Lock deleted users out** (Question 2, step 1).
- **Delete the Keycloak account** when a user is deleted. This needs `DeleteUserAsync` on
  `IKeycloakAdminClient`.
- **Redact tokens from traces.** The ASP.NET Core instrumentation's `EnrichWithHttpRequest` replaces
  `url.path` with the route template whenever the route has a `{token}` parameter.
- **Encrypt stored idempotency responses** with Data Protection, the same as AI keys. A body that
  can't be decrypted (e.g. its key was deleted from the `dataprotection` schema) is treated as
  expired.

## Backups

Deployments keep `pg_dump` backups ([deploy/README.md](../../../deploy/README.md), step 7), and a
restore brings erased people back. Two measures:

- **The erasure ledger.** Every erasure adds the erased `UserId` (a pseudonym, nothing else) to an
  `ErasureLedgerEntry` document in its own schema, `erasure`. A ledger inside the database would be
  rolled back by a restore too, so the restore procedure exports it first, restores the backup,
  then re-imports it:
  1. `COPY erasure.mt_doc_erasureledgerentry TO` a file.
  2. Restore the backup.
  3. Re-import the ledger file.
  4. Start the API.

  On start, `UserErasureService` re-erases everyone on the ledger whose data is back. The deploy
  READMEs get the steps.
- **Rotation.** Backups older than 30 days are deleted. The privacy notice states it.

## Events

```
UserErased(UserId UserId, DateTimeOffset OccurredAt)
    // the end of an erasure; UserDeleted remains the start
AiDataSharingAcknowledged(AiCredentialId Id, UserId AcknowledgedBy, DateTimeOffset OccurredAt)
```

Each gets a golden file in `EventShapeTests`. Masking adds no event types: it rewrites existing
events.

## Command slices

| Slice | Tier | Notes |
|---|---|---|
| `DeleteCurrentUser` (changed) | self, adults only | Lock out, then run `UserErasure`; `204`. A child gets `403` |
| `DeleteChild` | Manage, sole guardian | `DELETE /users/me/children/{childId}`; `409 child_has_other_guardians` |
| `GetAccountDeletionPreview` | self | `GET /users/me/deletion-preview`: children erased, groups handed over or deleted; the same rules as the erasure |
| `ExportPersonalData` | self | `GET /users/me/export`; a JSON file (`Content-Disposition: attachment`, enums by name); rate-limit policy `personal-data-export` (1 per 10 minutes) |
| `AcknowledgeAiDataSharing` | Manage | `PUT /mealplans/children/{childId}/ai/data-sharing-acknowledgement` |

Background services: `UserErasureService` (finishes interrupted erasures) and
`AiSessionRetentionService` (masks closed sessions after 30 days).

## Frontend

- **Admin, "Danger zone":**
  - The delete dialog lists the children who will be erased with the account, and the groups that
    pass to someone else or are deleted (`GetAccountDeletionPreview`).
  - A "Download my data" button, in its own "Your data" section above the danger zone. It fetches
    the export as a blob (the request needs the bearer token) and saves it through an object URL;
    a `429` says to wait 10 minutes.
- **Each child's row in "Children":** a "Delete" action next to "Remove", with an inline
  confirmation. A child with other guardians gets the `409` explained ("each of them must remove
  the child first") rather than a hidden button: the list doesn't know who else guards a child.
- **AI assistant:** the data-sharing notice and an acknowledgement step before the first session.
- All of this as en and da strings (the `i18n` skill), plus the screenshots.

## Testing

- **Erasure.** For every feature, an integration test writes data for a user, erases them, and
  then asserts two things:
  - none of the user's personal values (the same approach as
    [AuditLogTests.cs](../../../src/backend/buddy.IntegrationTests/Common/Observability/AuditLogTests.cs))
    appears in that feature's events (`mt_events.data`), its snapshots, or its documents, read
    straight from Postgres;
  - the shared data of others is unchanged.
- **Coverage.** A meta test fails when a registered event type has a `string` field and no masking
  rule, unless it is on an explicit allow-list (non-personal strings such as `TimeZoneId`).
- **Snapshots.** The existing `SnapshotTests` (snapshot == full replay) are extended to masked
  streams.
- **Lock-out and cascades:** the deleted user gets `403` everywhere; ownership transfer; the
  orphaned child is erased; a co-guarded child is not; `DeleteChild` returns `409` with
  co-guardians.
- **Export:** every section is present, and a child account sees only its own data.
- **AI:** the prompt contains no child `UserId`; other families' calendar titles appear as "busy";
  retention masks a session after 30 days (with a fake `TimeProvider`).

## Failure and edge-case behavior

| Case | Behavior |
|---|---|
| Erasure fails halfway (Postgres or Keycloak down) | The user is already locked out; `UserErasureService` retries until `UserErased` |
| `DELETE /users/me` repeated | `204`; each step is idempotent |
| The deleted user's still-valid access token | `403 user_not_provisioned` from the next request on |
| Sign-in again before Keycloak deletion completes | `GetOrCreateUser` sees `KeycloakIdentity.Deleted` and answers `404`; no new user is provisioned |
| A child with two guardians; one deletes their account | That guardian's link is revoked; the child and its data stay |
| `DeleteChild` with co-guardians | `409 child_has_other_guardians`; nothing changes |
| A group owner deletes their account; nobody else is in the group | The group and its calendars are deleted and erased |
| A database backup restored after an erasure | The restored data contains the erased user again; the restore procedure re-imports the erasure ledger and the sweeper re-erases everyone on it (see "Backups") |
| A snapshot whose event stream is gone (events wiped or partially restored, snapshots kept) | The snapshot can't be rebuilt, so `StreamErasure` deletes it instead; for the user stream that finishes the erasure without a `UserErased` |
| One user's erasure keeps failing | The sweep logs it (10004) and goes on with the other users; that user is retried on the next sweep |
| An export while an erasure is running | The export reads whatever is masked so far; the caller is locked out anyway |

## Decisions made

| Question | Decision |
|---|---|
| Erasure technique | Delete streams that belong only to the erased person; mask the user stream and shared streams (Marten protected-information masking plus a snapshot rebuild); no crypto-shredding |
| A child left without a guardian by an account deletion | Erased with the guardian (user's decision) |
| Who may delete a child | Only its sole active guardian (user's decision) |
| A group whose owner leaves | Passes to the longest-standing admin, else member; deleted only when empty (user's decision) |
| AI session retention | Masked 30 days after closing (user's decision) |
| Erasure execution | In the request, idempotent, with a background sweeper for failures |
| Export | One JSON document; guardians include their children |
| Health-data access audit | Structured audit logs only; no in-app access log. Each family runs its own Buddy and has someone technical who can read the logs (user's decision) |
| Family data anchored to an erased child (meals, meal plan, AI keys and sessions, task templates) | Erased with the child only when no sibling under the same guardians remains (user's decision) |
| AI calendar titles | Only items assigned to the session's child or to nobody, in the child's family calendars; the rest are sent as "busy" (user's decision) |
| Backups | An erasure ledger plus 30-day backup rotation (user's decision); see below |
| Free text in shared spaces that names an erased person | Stays. It is part of the family's shared plan, the other members can edit or delete it, and the author's identity is erased (user's decision) |

## Remaining open questions

- **Legal work outside the code.** These are decisions for the controller, not code:
  - a privacy notice;
  - a record of processing activities;
  - a DPIA (likely required for children's health data);
  - data processing agreements with the hosting provider (Oracle or Azure);
  - what the AI disclosure has to say.

## Implementation order

1. Question 8 (no new feature).
2. Erasure infrastructure: masking rules per feature, `Erased`, the coverage meta test, and the
   erasure step per store.
3. `DeleteCurrentUser` cascade with `UserErasureService`, group ownership transfer and the ledger.
4. `DeleteChild`, the deletion preview, and their screens.
5. Export.
6. AI minimization, retention and disclosure.
7. Health-data audit logs.

Each step can ship on its own.

## Implementation notes

What shipped with steps 1-7, and where it differs from the design above:

- **Infrastructure.** `Common/Erasure`:
  - `Erased.Text` (`"[erased]"`);
  - `IPersonalDataEraser` and `ErasureSubject`;
  - `StreamErasure`, with `DeleteStreamAsync` and `MaskStreamAsync`. Both first make sure the
    store's event table exists: Marten creates tables lazily, and erasure must also work for a
    feature the person never used.
- **Erasers and masking rules.** One `<Domain>PersonalData.cs` per feature: Babysitters,
  Calendars, Groups, Guardians (covering the Users store), Mealplans, Medicines, Pickups,
  PrintTemplates, Progress, SleepDiaries, TaskLibrary and WorkLocations.
  - Masking rules exist only in the four stores that keep shared streams: users, groups,
    calendars and meal plans.
  - `PersonalDataEraserCoverageTests` fails for a Marten store without an eraser. The idempotency
    store is exempt: short-lived and encrypted.
- **Orchestration.** `Features/Privacy`:
  - `UserErasure` runs the cascade.
  - `UserErasureService` sweeps 30 seconds after startup and then every 15 minutes, finishing
    unfinished erasures and re-erasing ledger entries. A failure is logged per user and doesn't
    stop the sweep.
  - New log events 10001-10005.
- **Changed from the design:**
  - Ownership transfer uses the existing events (Question 4).
  - Family data passes to an heir by re-anchoring index documents (Question 3).
  - The `KeycloakIdentity` is kept, flagged (Question 2).
  - iCal tokens a guardian issued are not revoked (Question 2).
- **Export (step 5).** `IPersonalDataExporter` and `ExportSubject` (`Common/Erasure`), one
  `<Domain>PersonalDataExport.cs` per feature (Users and Guardians both cover the Users store),
  orchestrated by `PersonalDataExport` (`Features/Privacy`), log event 10006.
  - Sections: `account`, `children`, `groups`, `calendars`, `taskLibrary`, `medicines`,
    `mealplans`, `babysitters`, `pickups`, `workLocations`, `printTemplates`, `progress`,
    `sleepDiary`. Each reuses the feature's response DTOs where they leave out secrets, so no
    token hashes, iCal tokens, Keycloak subjects or encrypted AI keys (the last four characters
    only).
  - The document is `{ exportedAt, userId, sections }`, indented, enums by name: it is read by
    people and other services, not by the Buddy frontend.
  - Tests: `Features/Users/ExportPersonalData/ExportPersonalDataTests.cs` (every section present,
    personal values found, no secret properties; a child gets `account` only; another family's
    data absent), `PersonalDataExporterCoverageTests`, and the rate limit in `RateLimitingTests`.
- **AI assistant (step 6).** All in `Features/Mealplans/AiAssistant`.
  - Minimization: `AiSessionPromptBuilder` numbers children by `UserId` order. `CalendarConflictLookup`
    keeps a title only for an item assigned to the session's child (`AiSessionStarted.ChildId`) or
    to nobody, in a *family calendar*: one whose owning group's members and explicit calendar
    members are all family (the session child's family from `MealFamilyResolution` plus their
    active guardians). Everything else goes out as `"busy"` with its date and time.
  - Retention: `AiSessionRetention`, run by `AiSessionRetentionService` a minute after startup and
    then daily, masks a session with the existing masking rules 30 days after its last event. Applying or
    discarding is always a session's last event, so the index gains `LastActivityAt` only (no
    separate `ClosedAt`), plus `ContentErasedAt` so an erased session is never picked again. Rows
    from before this shipped have no `LastActivityAt`; the sweep picks them by `StartedAt` and
    decides from the stream. A session nobody closed gets `AiSessionExpired` first, which closes it
    as `Discarded`, so its `"[erased]"` history can't be continued. Log events 6009-6010.
  - Disclosure: `AiDataSharingAcknowledged` on the credential stream,
    `PUT .../ai/data-sharing-acknowledgement` (`AcknowledgeAiDataSharing`, log 6011), and
    `DataSharingAcknowledgedAt` on `AiProviderSettings` (and so in the export). `StartAiSession`
    and `SendAiSessionMessage` both return `AiSessionOutcome`, whose `DataSharingNotAcknowledged`
    case is the `409`, so a session started before this shipped can't be continued without it.
    One acknowledgement covers the family. Frontend: `AiDataSharingNotice` on the
    assistant page (instead of the start form until acknowledged) and on the provider settings.
  - Tests: `AiDataMinimizationTests`, `AiSessionRetentionTests`, `AcknowledgeAiDataSharingTests`.
- **Health-data audit logs (step 7).** `MedicinesLog` 9001 (schedules) and 9002 (doses, with the
  date range) and `SleepDiariesLog` 5004 (diary entries, with the date range), written after a
  successful read only, so a denied request isn't logged as a read. Each carries `ChildId`,
  `UserId` and `AccessPath` (`Common/Observability/HealthDataAccessPath`): `Guardian`, `Self` (a
  child reading their own doses, which `CheckMark` allows) or `Group`, with the `GroupId` for a
  group read. `GetSharedMedicineGroup` only says whether the medicine is shared, so it isn't
  logged. Tests: `Common/Observability/AuditLogTests.cs`.
- **Earlier deletions.** Users deleted before this shipped (`UserDeleted` only) are erased by the
  first sweep, which also locks them out.
- **Tests.** `buddy.IntegrationTests/Features/Privacy/AccountErasureTests.cs`.
  `PersonalDataScanner` searches every `mt_events` and `mt_doc_*` table for the erased people's
  names, emails and free text. The scenarios cover:
  - a co-guarded child staying with the other guardian;
  - a group passing to its longest-standing admin;
  - family data passing to a sibling;
  - a child's `403`;
  - the sweep, both for a legacy deletion and for a ledger entry after a restore, and for a
    deleted user whose snapshot outlived its event stream.

## Diagram

```mermaid
flowchart TB
    subgraph Request["DELETE /users/me"]
        Lock["1. UserDeleted + KeycloakIdentity.Deleted\n(locked out at once)"]
        Steps["2-5. Erasure steps, each idempotent"]
        Done["UserErased"]
        Lock --> Steps --> Done
    end
    Sweeper["UserErasureService\n(retries users with UserDeleted, no UserErased)"] -.-> Steps
    subgraph Cascade["Erasure steps"]
        Children["Children\nsole guardian: erase child\nelse: revoke own GuardianLink"]
        Groups["Owned groups\ntransfer to longest-standing admin/member\nelse delete"]
        Own["Own streams\nbabysitters, work locations, print templates,\ninvites, tokens, AI keys"]
        Person["User stream masked + snapshot rebuilt\nKeycloak account deleted"]
    end
    Steps --> Children & Groups & Own & Person
    subgraph Marten["Per store"]
        Mask["ApplyEventDataMasking(IncludeStream)"]
        Rebuild["RebuildSingleStreamAsync<TSnapshot>"]
        Docs["Delete/rewrite lookup documents"]
        Mask --> Rebuild --> Docs
    end
    Children & Own & Person --> Mask
```
