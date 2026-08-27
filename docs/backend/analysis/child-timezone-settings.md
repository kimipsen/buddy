1. # Guardian-Managed Child Time Zone

Status: Implemented

## Context

This mirrors
[child-language-settings.md](child-language-settings.md) end-to-end for
`TimeZoneId` instead of `Language`. `TimeZoneId` is already a first-class,
self-service field on `User` — see
[Types/TimeZoneId.cs](../../../src/backend/buddy/Features/Calendars/Types/TimeZoneId.cs)
and the self-service `PATCH /users/me/timezone` endpoint
([UpdateTimeZone.Handler.cs](../../../src/backend/buddy/Features/Users/UpdateTimeZone/UpdateTimeZone.Handler.cs)).
Calendar event times already render through `UserDatePipe`
([user-date.pipe.ts](../../../src/frontend/buddy/src/app/core/user-date.pipe.ts))
using the viewer's stored time zone, so once a guardian sets a child's time
zone it takes effect immediately for that child's calendar views — no
display code needed to change.

The same gap as language existed here: guardians had no way to set a
child's time zone, only their own.

## Design

Same three decisions as `child-language-settings.md` apply unchanged (any
active guardian may set it; the capability lives in the Guardians feature
rather than the Users feature; the UI is inline on `manage-children`), so
they are not repeated here.

**Backend** — `Features/Guardians/UpdateChildTimeZone/`:

- `UpdateChildTimeZone(UserId? GuardianId, UserId ChildId, TimeZoneId TimeZoneId)`,
  built via `FromClaims(principal, childId, timeZoneId)` — same shape as
  `UpdateChildLanguage.Command.cs`.
- Handler takes the same `IGuardianLinkEventStore` and `IUserEventStore`
  pair `UpdateChildLanguageHandler` uses:
  1. `guardians.FindActiveLinkAsync(childId, guardianId, …)` — `null` ⇒
     `Result.NotFound`.
  2. `TimeZoneResolution.IsValid(timeZoneId)` ⇒ `Result.Validation`
     otherwise, identical check to `UpdateTimeZoneHandler`.
  3. Rehydrate the child's `User`; no-op if `ResolvedTimeZoneId` is already
     the requested value; otherwise append
     `TimeZoneUpdated(childId, before, after, now)` — the existing event, no
     new event type or migration needed.
- Route: `PATCH /users/me/children/{childId:guid}/timezone`, mapped in
  [GuardiansFeature.cs](../../../src/backend/buddy/Features/Guardians/GuardiansFeature.cs)
  next to `MapUpdateChildLanguage`.
- `ChildSummary`
  ([ListMyChildren.Handler.cs](../../../src/backend/buddy/Features/Guardians/ListMyChildren/ListMyChildren.Handler.cs))
  gains a `TimeZoneId` field (the child's `ResolvedTimeZoneId`) alongside
  `Language`, so the frontend can show the current value without a second
  round trip.

**Frontend**:

- `GuardiansService.ChildSummary` gains `timeZoneId: string`; a new
  `updateChildTimeZone(childId, timeZoneId)` method calls the new endpoint,
  mirroring `updateChildLanguage`.
- `ManageChildren` gains a per-child time zone `<select>` next to the
  language one (options from `listTimeZoneIds()` in
  [date-utils.ts](../../../src/frontend/buddy/src/app/core/date-utils.ts)),
  saving immediately on change with a per-child saving/error signal keyed by
  child id, the same pattern the language select uses.
- New translation keys under `admin.manageChildren.timeZone.*` in both
  `en/admin.ts` and `da/admin.ts`.

## Decisions made

| Question | Decision |
|---|---|
| Which guardians can set a child's time zone | Any active `GuardianLink`, regardless of `GuardianKind` — same rule as child language |
| Where the endpoint/handler lives | Guardians feature (`/users/me/children/{childId}/timezone`), not an extension of the Users feature's self-service endpoint |
| New event type needed | No — reuses the existing `TimeZoneUpdated` event verbatim |
| Where the UI lives | Inline on the existing `manage-children` screen, next to the language select |
| Does `ChildSummary` need a `TimeZoneId` field | Yes — added so the guardian UI can show/edit the current value without an extra request |
