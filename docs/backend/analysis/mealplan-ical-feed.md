# Meal Plan iCal Feed

Status: Implemented

## Context

[Calendars](../calendars/flow.md) already lets a guardian subscribe an external
calendar app to a family's calendar via a token-bearing `.ics` URL
([`GetIcalFeed`](../../../src/backend/buddy/Features/Calendars/GetIcalFeed/GetIcalFeed.Handler.cs),
[`IcalFeedWriter`](../../../src/backend/buddy/Features/Calendars/IcalFeedWriter.cs)). Meal plans
([mealplans.md](mealplans.md)) have no equivalent — a guardian who wants "tonight's dinner" to show
up in their phone's calendar app has no way to get it there short of re-entering it as a separate
calendar event.

This adds the same subscription mechanism to `Features/Mealplans`, reusing the Calendars feature's
pattern (token-in-URL, `Ical.Net` serialization, an anonymous feed route layered under an
authenticated management surface) almost unchanged. Two things don't carry over directly, though,
and are the actual design decisions in this doc:

1. A `MealPlanAssignment` ([`MealPlanAssignment.cs`](../../../src/backend/buddy/Features/Mealplans/Types/MealPlanAssignment.cs))
   is keyed by `(DateOnly Date, MealSlot Slot)` — it has no time-of-day. A calendar event needs one.
2. `MealPlan` ([`MealPlan.cs`](../../../src/backend/buddy/Features/Mealplans/Types/MealPlan.cs)) has
   no per-family timezone concept the way `Calendar` has `TimeZoneId` — the feed's rendering has to
   account for that absence rather than assume it away.

## Decision: per-slot times live on the `MealPlan`, not hardcoded

Three ways to get a time-of-day for a `MealSlot`:

1. **Hardcode a hidden constant per slot** in the feed writer (breakfast = 07:00, lunch = 12:00,
   dinner = 18:00) — simplest, but every family gets the same times whether or not they match how
   that family actually eats, with no way to change it short of a code change.
2. **A time on every `MealPlanAssignment`**, set each time a meal is assigned to a slot — most
   flexible (a family could genuinely eat dinner at a different time each night), but it means a
   guardian has to pick a time on every single `AssignMealToSlot` call, for a property that in
   practice is the same value on almost every assignment for a given slot.
3. **A configurable default time per slot, on the `MealPlan` itself** — set once (or left at a
   built-in default), reused by every assignment in that slot, with no new field needed on
   `MealPlanAssignment`.

**Decision: option 3.** `MealPlan` is already a family-wide singleton
([mealplans.md's "family, not child" framing](mealplans.md)), so "what time does this family eat
breakfast" is exactly the kind of fact that belongs on the plan, not repeated per assignment. It
also means a family that never touches this setting still gets a working feed — hardcoded defaults
(`Breakfast` 07:00, `Lunch` 12:00, `Dinner` 18:00, `Snack` 15:00 —
[`MealSlotDefaultTimes.cs`](../../../src/backend/buddy/Features/Mealplans/Types/MealSlotDefaultTimes.cs))
apply wherever the plan hasn't configured a slot, so existing plans need no backfill.

Option 2 (per-assignment time) is left as a possible future addition — nothing in this design
forecloses it — but isn't built now: it's a feature nobody asked for, and per-slot defaults cover
the actual ask ("breakfast is early in the day, lunch around midday, dinner around evening").

## Decision: `Snack` is a first-class slot, not a special case

`MealSlot` ([`MealSlot.cs`](../../../src/backend/buddy/Features/Mealplans/Types/MealSlot.cs))
already has four values: `Breakfast`, `Lunch`, `Dinner`, `Snack`. Because slot times are configured
on the plan rather than hardcoded per slot in the writer, `Snack` needs no special-casing to appear
in the feed — it gets a default time (15:00, a generic afternoon snack) exactly like the other three,
overridable the same way. Rejected alternative: omit `Snack` from the feed unless a time is
explicitly configured — this would mean a family's snack assignments silently never show up until
they discover and use a settings screen, which is worse than a defensible default that a guardian
can change.

## Domain model changes

`MealPlan` gains two fields:

```csharp
ImmutableDictionary<MealSlot, TimeOnly> SlotTimes
ImmutableDictionary<IcalTokenId, IcalTokenInfo> Tokens
```

Both default to empty on `MealPlanCreated` — a slot missing from `SlotTimes` falls back to
`MealSlotDefaultTimes` at render time, never written to the aggregate itself.

New events in [`MealPlanEvents.cs`](../../../src/backend/buddy/Features/Mealplans/Types/MealPlanEvents.cs):

- `MealPlanSlotTimeSet(MealPlanId Id, MealSlot Slot, TimeOnly Time, UserId ModifiedBy, DateTimeOffset OccurredAt)`
  — one slot per event, the same granularity `MealAssignedToSlot` already uses for one date/slot at
  a time. Rehydrate: `SlotTimes.SetItem(Slot, Time)`.
- `MealPlanIcalTokenIssued(MealPlanId Id, IcalTokenId TokenId, string Hash, UserId IssuedBy, DateTimeOffset OccurredAt)`
- `MealPlanIcalTokenRevoked(MealPlanId Id, IcalTokenId TokenId, UserId RevokedBy, DateTimeOffset OccurredAt)`

`MealPlan.FindMatchingToken(string hash)` mirrors
[`Calendar.FindMatchingToken`](../../../src/backend/buddy/Features/Calendars/Types/Calendar.cs) —
constant-time comparison (`CryptographicOperations.FixedTimeEquals`) over stored hashes, so an
anonymous feed request can't learn anything from timing.

## Token issuance/management: a fourth, feature-local token type

`IcalToken`/`IcalTokenId`/`IcalTokenInfo`
([`Features/Calendars/Types/`](../../../src/backend/buddy/Features/Calendars/Types/IcalToken.cs))
are duplicated into `Features/Mealplans/Types/` under the same names, rather than extracted into a
shared type both features reference. This isn't an oversight — it follows a convention this codebase
already has three times over: `EmailVerificationToken`
([`Features/Users/Types/`](../../../src/backend/buddy/Features/Users/Types/EmailVerificationToken.cs)),
`GroupInviteToken`, and `GuardianInviteToken` are each defined separately in their own feature,
despite being structurally similar "random token, only the hash persisted" primitives. A meal-plan
`IcalToken` is the fourth instance of that same shape, not a new pattern, and keeping it feature-local
avoids a cross-feature dependency that this codebase's screaming-architecture boundaries otherwise
don't have.

Management endpoints — `CreateMealPlanIcalToken`, `ListMealPlanIcalTokens`,
`RevokeMealPlanIcalToken` — are keyed by `childId` (resolved internally to the family `MealPlanId`
via [`MealFamilyResolution.ResolveFamilyMealPlanIdAsync`](../../../src/backend/buddy/Features/Mealplans/MealFamilyResolution.cs)),
the same way every other guardian-facing mealplan route is, rather than by `MealPlanId` directly —
there is no existing route that exposes a raw `MealPlanId` to a client today. Issuing a token (or
setting slot times) lazily creates the plan stream if the family doesn't have one yet, copying
[`AssignMealToSlotHandler.AssignForChildAsync`](../../../src/backend/buddy/Features/Mealplans/AssignMealToSlot/AssignMealToSlot.Handler.cs)'s
existing lazy-create pattern rather than requiring an assignment to exist first.

All three are guardian-only (`MealplanAuthorization.CheckManage`
([`MealplanAuthorization.cs`](../../../src/backend/buddy/Features/Mealplans/MealplanAuthorization.cs))).
A child's `Rate` tier never gets to mint a subscription link — consistent with the existing asymmetry
where a child can view and rate but never write the plan.

## The feed: route, window, and rendering

`GetMealPlanIcalFeed` — `GET /mealplans/{mealPlanId:guid}/ical/{token}` — is, unlike every other
mealplans route, keyed directly by `MealPlanId`. This is deliberate: the feed itself is anonymous
(`.AllowAnonymous()` overriding the route group's `.RequireAuthorization()`, same override
[`GetIcalFeed.Endpoint.cs`](../../../src/backend/buddy/Features/Calendars/GetIcalFeed/GetIcalFeed.Endpoint.cs)
uses), so there's no `childId`/caller identity to resolve from — the token is the only credential,
and `CreateMealPlanIcalToken`'s response is the one place `MealPlanId` becomes visible to a client,
folded into the ready-to-use `SubscriptionPath`.

The handler reads the `MealPlan` stream by id, rehydrates, and checks the token exactly like
`GetIcalFeedHandler` does for `Calendar`: a plan that doesn't exist and a plan that exists but has no
matching token both collapse to the same `NotFound` response, so a guesser learns nothing from the
difference.

**Window.** `GetIcalFeedHandler` uses a 90-day-behind/365-day-ahead rolling window. This feed uses a
narrower one — 14 days behind, 60 days ahead — because meal plans are realistically filled in much
closer to the day itself than a general calendar is (nobody plans dinner a year out); a smaller
window keeps the per-request expansion work cheaper without losing anything a subscribed calendar app
would actually show.

**Rendering (`MealPlanIcalFeedWriter`, modeled on `IcalFeedWriter`).** One `VEVENT` per
`MealPlanEntry` ([`MealPlanEntry.cs`](../../../src/backend/buddy/Features/Mealplans/Types/MealPlanEntry.cs)):
`Summary` is `"{Slot}: {MealName}"` (e.g. "Breakfast: Pancakes"), `Description` is the assignment's
notes when present, `DtStart` is the entry's date combined with `plan.SlotTimes.GetValueOrDefault(entry.Slot,
MealSlotDefaultTimes[entry.Slot])`, and `DtEnd` is `DtStart + 30 minutes` (a fixed constant — not
made configurable, since nobody asked for per-meal duration and a 30-minute block is a reasonable
placeholder for a "when to eat" reminder either way).

The `DtStart`/`DtEnd` values are written as **floating local time** — no trailing `Z`, no `TZID` —
unlike `IcalFeedWriter`, which anchors everything to UTC using `Calendar.TimeZoneId`. This is a
deliberate difference, not a gap: `MealPlan` has no per-family timezone field to anchor to, and a
meal time is inherently a local-wall-clock fact ("breakfast at 7am," wherever the family is) rather
than an instant that needs to survive a timezone conversion. Introducing a timezone field on
`MealPlan` purely to match `Calendar`'s approach was considered and rejected — it would be new state
with no other use in this feature, addressing a problem (multi-timezone families) nobody has raised.

`Uid` is `{mealId:N}-{date:yyyyMMdd}-{slot}@buddy` — deterministic per `(meal, date, slot)`, so it's
stable across feed regenerations the same way `IcalFeedWriter.BuildUid` is for calendar occurrences.

## API surface

| Route | Method | Auth | Purpose |
|---|---|---|---|
| `/mealplans/children/{childId}/slot-times` | `PUT` | Guardian (`CheckManage`) | Set/override one or more slot default times |
| `/mealplans/children/{childId}/ical-tokens` | `POST` | Guardian (`CheckManage`) | Mint a subscription token; returns the plaintext token once and a ready-to-use `SubscriptionPath` |
| `/mealplans/children/{childId}/ical-tokens` | `GET` | Guardian (`CheckManage`) | List issued tokens (hash-free) |
| `/mealplans/children/{childId}/ical-tokens/{tokenId}` | `DELETE` | Guardian (`CheckManage`) | Revoke a token |
| `/mealplans/{mealPlanId}/ical/{token}` | `GET` | Anonymous, token-in-URL | The `.ics` feed itself |

No group-keyed sibling route for slot-times or token management — both are plan-wide settings, not
a per-request action like `AssignMealToSlotForGroup`, so there's nothing for a group-scoped variant
to do differently.

## Testing

`GetMealPlanIcalFeedTests.cs` mirrors
[`GetIcalFeedTests.cs`](../../../src/backend/buddy.IntegrationTests/Features/Calendars/GetIcalFeed/GetIcalFeedTests.cs):
valid token returns a feed containing the expected `VEVENT`s with correct `DTSTART`, invalid token
and revoked token both return 404. Additional cases specific to this feature: a configured slot time
overrides the default in the rendered feed, and an unconfigured `Snack` slot still renders using its
built-in default rather than being omitted.

## Decisions made

| Question | Decision |
|---|---|
| Where does a meal's time-of-day come from | A configurable default per `MealSlot`, stored on `MealPlan`, not per-assignment |
| Does `Snack` need special-casing | No — same configurable-default mechanism as the other three slots |
| Shared `IcalToken` type across Calendars/Mealplans | No — duplicated per feature, matching the existing `EmailVerificationToken`/`GroupInviteToken`/`GuardianInviteToken` convention |
| Feed route keyed by `childId` or `MealPlanId` | `MealPlanId` — the feed is anonymous and has no caller identity to resolve `childId` from |
| Timezone handling in the feed | Floating local time — no per-family timezone field exists or is being added for this |
| Feed lookback/lookahead window | 14 days behind / 60 days ahead — narrower than Calendars' 90/365, since meal plans are filled in closer to the day |
| Event duration | Fixed 30 minutes, not configurable |

## Open questions

- **Per-assignment time override.** Not built now (see "Decision" above) — if a family's mealtimes
  genuinely vary night to night, this would need a nullable time on `MealPlanAssignment` itself,
  falling back to the plan's slot default when unset.
- **Resetting a slot to its default.** `UpdateMealSlotTimes` only ever sets an explicit time; there's
  no way to remove an override and fall back to the built-in default other than setting it to that
  same value again. Worth adding if it comes up in practice.
- **Configurable event duration.** Fixed at 30 minutes for now; could become a second per-slot
  setting alongside `SlotTimes` if guardians want, say, a longer dinner block.
