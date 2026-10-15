# Calendar Item Alarms

Status: Implemented. `ItemAlarms` on `CalendarItem` (`Types/ItemAlarms.cs`, rules in
`ItemAlarmRules.cs`), the `ItemAlarmsUpdated` event and the `UpdateItemAlarms` slice
(`PATCH /calendars/{calendarId}/items/{itemId}/alarms`), an optional `Alarms` on `CreateItem`,
`ScheduleTaskFromTemplate` and their three creation events, `alarms` on `CalendarItemResponse` and
`CalendarItemOccurrence`, `VALARM`s in `IcalFeedWriter`, and the `alarm-picker` preset chips in
the guardian agenda's create, edit and schedule-from-template forms with a bell on agenda rows.

## Context

A guardian who subscribes to a Buddy calendar from their phone's calendar app
([`GetIcalFeed`](../../../src/backend/buddy/Features/Calendars/GetIcalFeed/)) gets every event and
task, but no reminders. Today the feed writer
([`IcalFeedWriter.cs`](../../../src/backend/buddy/Features/Calendars/IcalFeedWriter.cs)) writes
`VEVENT`/`VTODO` components with no `VALARM`. The calendar app's own default alert, if any, then
applies to everything, or a guardian adds alerts by hand on every occurrence.

The ask: an alarm is set on **the item itself**. A dentist appointment can have an alarm two hours
before, while the weekly football practice in the same calendar has none. In the subscribed calendar
only the dentist appointment alerts.

This is a calendar-export feature and a nice-to-have. It is **not** related to
[transition warnings](transition-warnings.md). Those are a Buddy concept: lead times per child,
shown as a countdown on the child's screen, with defaults, quiet hours and per-item overrides
([`ItemWarningOverride.cs`](../../../src/backend/buddy/Features/TransitionWarnings/Types/ItemWarningOverride.cs)).
An alarm is an RFC 5545 `VALARM` that the subscriber's calendar app fires. Buddy never fires it,
never shows it to a child, and the two settings don't read each other.

The closest precedents are:

- [`RecurrenceUpdated`](../../../src/backend/buddy/Features/Calendars/Types/CalendarItemEvents.cs)
  and the `UpdateItemRecurrence` slice: an item property with its own `Before`/`After` event and its
  own `PATCH .../items/{itemId}/<property>` route.
- [calendar-all-day-items.md](calendar-all-day-items.md): a new field on the creation events, kept
  backward compatible through a constructor default, and the same feed writer.
- [skip-occurrences.md, Existing snapshots](skip-occurrences.md#existing-snapshots): a new
  `CalendarItem` property that snapshot rows written before it existed read back as `null`.

### What subscribing clients actually do with `VALARM`

This decides how much the feature can promise:

| Client | `VALARM` in a subscribed (URL) feed |
|---|---|
| Apple Calendar (iOS/macOS) | Honoured, unless the subscription's "Remove alerts" option is on. It is **on by default** when subscribing. |
| Google Calendar | Ignored. Google drops alarms from subscribed calendars and applies the user's default notifications for that calendar. |
| Outlook (desktop/web) | Varies by version; Outlook on the web generally ignores them. |
| Thunderbird, DAVx5-based Android apps | Honoured. |

So alarms are best effort. The UI says so in one line (Question 6), and nothing else in Buddy
depends on them.

## Question 1: where do alarms live?

**Decision: a new `Alarms` property on `CalendarItem`, changed by its own event `ItemAlarmsUpdated`
and its own slice `UpdateItemAlarms` (`PATCH /calendars/{calendarId}/items/{itemId}/alarms`).** This
is the same shape as `RecurrenceUpdated`/`UpdateItemRecurrence`. The creation events
(`EventItemCreated`, `TaskItemCreated`, `TemplateTaskItemCreated`) gain an optional `Alarms`, so an
item can be created with alarms in one request.

```csharp
// CalendarItem.cs, today (line 12)
public sealed record CalendarItem(
    CalendarItemId Id, CalendarId CalendarId, UserId CreatedBy, string Title, Icon? Icon, Color Color,
    ItemSchedule Schedule, Recurrence Recurrence, ImmutableHashSet<CompletionKey> CompletionLog,
    ImmutableHashSet<DateOnly> SkippedDates, UserId LastModifiedBy, bool IsDeleted)

// proposed: one more property, after SkippedDates
    ImmutableHashSet<DateOnly> SkippedDates,
    ItemAlarms Alarms,
    UserId LastModifiedBy, bool IsDeleted)

// Advance gains one case
ItemAlarmsUpdated updated => item with { Alarms = updated.After, LastModifiedBy = updated.ModifiedBy },
```

```csharp
// CalendarItemEvents.cs, new
public sealed record ItemAlarmsUpdated(CalendarItemId Id, ItemAlarms Before, ItemAlarms After, UserId ModifiedBy, DateTimeOffset OccurredAt);

// EventItemCreated / TaskItemCreated / TemplateTaskItemCreated: one trailing optional parameter
    ItemAlarms? Alarms = null);   // null in history written before this change = no alarms
```

Blast radius: `CalendarItem`'s positional constructor is called only from `CalendarItem.New`, so
adding a property touches one call site. The three creation records gain a trailing optional
parameter, so their existing `new(...)` calls in `CreateItemHandler` and `ScheduleTaskFromTemplate`
compile unchanged until they start passing alarms. The `CalendarItemEvent` union, its `EventType`
switch and Marten's event type registration in `CalendarsFeature.cs` gain one case each.

Rejected alternatives:

- **Alarms inside [`ItemDetails`](../../../src/backend/buddy/Features/Calendars/Types/ItemDetails.cs)
  (`Title`, `Icon`, `Color`).** It would save one slice, since the edit form already calls
  `updateItemDetails`. But every title edit would then also record an alarm `Before`/`After`, and
  the `ItemDetailsUpdated` golden file would change shape for a feature that has nothing to do with
  how an item looks. Rejected, because recurrence already set the precedent: a property with its own
  rules gets its own event.
- **Alarms inside the schedule value objects (`Period`/`DueDate`), as `IsAllDay` was.** `IsAllDay`
  went there because it can never disagree with the dates it describes
  ([calendar-all-day-items.md](calendar-all-day-items.md)). Alarms are independent of the dates:
  rescheduling an appointment keeps "two hours before". Putting them there would also make every
  `EventRescheduled`/`TaskRescheduled` carry alarms it doesn't change. Rejected.
- **A per-calendar default alarm.** That is not what was asked (one item alerts, its neighbour
  doesn't). It could come later as a default for the create form (see open questions).

### Existing snapshots

A `CalendarItem` snapshot row written before `Alarms` existed reads back with `Alarms = null`. As
with `SkippedDates`, `Advance` and every reader treat `null` as `ItemAlarms.None`, so no snapshot
rebuild is needed at deploy. `Start` maps `created.Alarms ?? ItemAlarms.None`.

## Question 2: what is an alarm?

**Decision: an alarm is a signed offset in whole minutes from the occurrence's anchor. Negative
means before. The anchor is an event's start or a task's due time, and for an all-day item local
midnight at the start of its day. An item has at most 3 alarms, distinct and sorted.** This is
exactly RFC 5545's relative `TRIGGER` (`-PT2H`, `PT9H`), so the feed writer copies the offset as is.

```csharp
// Types/ItemAlarms.cs, new. Same shape as ItemWarningOverride.LeadTimes: an ImmutableArray with
// value equality, normalized (distinct, ascending) on the way in.
public sealed record ItemAlarms(ImmutableArray<int> OffsetMinutes)
{
    public const int Max = 3;
    public const int MinOffset = -4 * 7 * 24 * 60; // 4 weeks before
    public const int MaxOffset = 24 * 60;          // up to 24 h after the anchor (all-day "on the day at 09:00")
    public static readonly ItemAlarms None = new([]);
    ...
}
```

- **Signed, not "minutes before".** An all-day item's anchor is midnight, so "on the day at 09:00"
  is `+540` and "the day before at 18:00" is `-360`. With only "minutes before", an all-day alarm
  could at best go off at midnight or at an odd hour the day before. Apple Calendar uses the same
  model for all-day alerts.
- **Relative, not absolute.** An absolute trigger (`TRIGGER;VALUE=DATE-TIME`) can't follow a
  repeating item's occurrences, and it would go stale when the item is rescheduled. Rejected.
- **Max 3.** Apple shows two alerts per event and Outlook one. Three covers "the day before" plus
  "an hour before" plus one spare, and keeps the form small.
- **Offsets are not tied to all-day.** The validator accepts any offset in `MinOffset..MaxOffset`
  for any item, and only the UI decides which presets to offer. A positive offset on a timed item is
  valid RFC 5545 ("after it starts"). So `RescheduleItem` never needs to touch alarms when an item
  switches between all-day and timed. That avoids coupling two slices for a corner case.

Validation (FluentValidation, `UpdateItemAlarmsValidator` and the matching rules on `CreateItem` /
`ScheduleTaskFromTemplate`): at most 3 entries, each within range. Duplicates are silently merged
by normalization, not rejected, the same as `LeadMinutesRules.Normalize`.

## Question 3: what is written to the feed?

**Decision: each `VEVENT`/`VTODO` written for an occurrence of an item with alarms gets one
`VALARM` per offset, `ACTION:DISPLAY`, `DESCRIPTION` = the occurrence title. An item with no alarms
gets no `VALARM` at all.**

```csharp
// IcalFeedWriter.cs:33, inside the Event case, after the IcsEvent is built (and likewise for the Todo at :49)
foreach (var offset in occurrence.Alarms)
{
    icsEvent.Alarms.Add(new Alarm
    {
        Action = AlarmAction.Display,
        Description = occurrence.Title, // required for DISPLAY by RFC 5545
        Trigger = new Trigger(ToDuration(offset)),
    });
}
```

As built, `ToDuration` splits the offset into nominal days, hours and minutes with the offset's
sign (`new Duration(days:, hours:, minutes:)`), so `-1440` is written `-P1D`, `-2340` `-P1DT15H`
and `+540` `PT9H`; Ical.Net writes a zero offset as `P0D`. `Duration.FromMinutes` compiled too,
but writes everything in minutes (`PT540M`), which is valid but harder to read.

```
BEGIN:VALARM
ACTION:DISPLAY
DESCRIPTION:Dentist
TRIGGER:-PT2H
END:VALARM
```

- `CalendarItemOccurrence` gains `ImmutableArray<int> Alarms` (empty when none; for a routine,
  only the first subtask occurrence of each run carries them, see Question 4), set in the three
  `new CalendarItemOccurrence(...)` calls in
  [`CalendarOccurrenceExpansion.cs`](../../../src/backend/buddy/Features/Calendars/CalendarOccurrenceExpansion.cs)
  (lines 81, 97, 133) from `item.Alarms`. The feed, the agenda edit form and `ListOccurrences` all
  read it from there.
- A `VTODO`'s relative trigger is anchored on `DUE` when there's no `DTSTART`, which is how tasks
  are written today. No `DTSTART` needs adding.
- **`ACTION:AUDIO` and `ACTION:EMAIL` rejected.** `AUDIO` needs an attachment clients mostly ignore
  in feeds. `EMAIL` would suggest Buddy sends mail. Clients don't send it from a subscription
  anyway.
- **ETag:** alarms come from stored state only, so an unchanged feed still renders byte-identically
  within a day ([conditional-get-etags.md](conditional-get-etags.md)). Changing an alarm changes the
  bytes, so the ETag changes.
- The exact Ical.Net 5.2.3 `Trigger`/`Duration` constructor is an implementation-time check, as the
  date-only `CalDateTime` was in calendar-all-day-items.md. The semantics (a relative signed
  duration) don't depend on it.
- The meal plan feed (`MealPlanIcalFeedWriter`) is untouched. Meals aren't calendar items.

## Question 4: repeating items and routines

**Decision: every occurrence of a repeating item carries the item's alarms.** The feed already
writes one `VEVENT` per occurrence with its own UID, so each one gets its own `VALARM`s. A skipped
occurrence isn't written, so it doesn't alert.

**Decision (settled 2026-10-15): for a task scheduled from a template (a routine or packing list), only the first subtask's
`VTODO` of each run carries the alarms.** The feed writes one `VTODO` per subtask. Giving all of
them the alarm would fire one alert per subtask (a six-step morning routine = six alerts). Settled with the
user; see the decisions table.

As built, `CalendarOccurrenceExpansion` decides this, not the feed writer: the first subtask that
applies on a run's date gets the item's alarms on its occurrence and the later ones get none. So
`ListOccurrences` shows the same thing the feed writes, and the agenda puts the bell on the run's
block.

## Question 5: who can set alarms, and who sees them?

**Decision: the `Manage` tier on the item's calendar, the same as `UpdateItemRecurrence`
(`CalendarAuthorization`).** In the code that tier is `CalendarAuthorization.CheckContribute`
(the calendar's Owner and Contributor roles). `View` gets `403`. A missing item, or a calendar the user can't see,
gets `404`.

- `CalendarItemResponse` gains `alarms: number[]`. `ListOccurrences` returns them on each
  occurrence so the guardian agenda's edit form can prefill, as it does for `iconOverride`.
- The child endpoints return the same occurrence DTO. The array is harmless there, and the child UI
  never shows it. Stripping it was considered and rejected: it would need a second DTO for no
  privacy gain, because alarms are not personal data.
- GDPR: alarms are item configuration, not personal data. The calendar personal-data export
  ([`CalendarsPersonalDataExport.cs`](../../../src/backend/buddy/Features/Calendars/CalendarsPersonalDataExport.cs))
  serializes `CalendarItemResponse`, so they appear there once that DTO has them.

## Question 6: the guardian UI

**Decision: an "Alarms" picker in the agenda's create and edit forms
([`agenda.html`](../../../src/frontend/buddy/src/app/features/guardian/calendar/agenda/agenda.html)),
as preset chips, up to 3 selected. The presets depend on whether the item is all-day.**

| Timed item | Offset | All-day item | Offset |
|---|---|---|---|
| At start | `0` | On the day at 09:00 | `+540` |
| 5 / 15 / 30 minutes before | `-5` / `-15` / `-30` | The day before at 18:00 | `-360` |
| 1 / 2 hours before | `-60` / `-120` | The day before at 09:00 | `-900` |
| 1 day before | `-1440` | 2 days before at 09:00 | `-2340` |

- If the all-day toggle flips, any selected alarm that isn't a preset of the new mode is cleared, so
  the form never sends an offset the user can't see.
- An offset stored outside the presets (only possible through the API) is shown as one extra chip
  ("3 h 20 min before") so editing doesn't silently drop it.
- Under the picker: "Alarms only work in calendar apps subscribed to this calendar's link. Some apps
  (Google Calendar) ignore them." In Danish too, through the `i18n` skill.
- `saveEditItem` (`agenda.ts:873`) calls `calendars.updateItemAlarms(...)` only when the selection
  changed, after the existing `updateItemDetails`/`rescheduleItem` calls. Create sends `alarms` in
  the existing `CreateItemRequest`.
- The agenda row shows a small bell (🔔, `aria-label` "Has alarms") on occurrences with alarms. That
  is a visible change to the agenda page, so `task docs:screenshots` is re-run with one demo item
  given an alarm in `demo-family.ts`.
- `ScheduleTaskFromTemplate`'s form gets the same picker (timed presets only; a template task is
  never all-day).

## Command slices (`Features/Calendars/`)

| Slice | Change |
|---|---|
| `UpdateItemAlarms` (new) | Command, validator, handler, endpoint. Appends `ItemAlarmsUpdated`. **No-op when unchanged**: no event, `200` with the current item, as `UpdateItemRecurrence` does. |
| `CreateItem` | `CreateItemRequest.Alarms` (`int[]?`, default empty), validated, carried on `EventItemCreated`/`TaskItemCreated`. |
| `ScheduleTaskFromTemplate` | Same, on `TemplateTaskItemCreated`. |
| `GetIcalFeed` | No slice change. `IcalFeedWriter` writes `VALARM`s (Question 3). |
| `ListOccurrences` / `ListItems` | No slice change. The DTOs gain `alarms`. |

## Routes

| Method | Route | Tier | Responses |
|---|---|---|---|
| `PATCH` | `/calendars/{calendarId}/items/{itemId}/alarms` body `{ "alarms": [-120, -1440] }` | Manage | `200` item, `400` validation, `403`, `404` |

Run `task docs:openapi` after the endpoint and DTO changes. Add the request to `Calendars.http`.

## Testing

- **Event shapes** (`CalendarEventShapeTests`): a new golden file `ItemAlarmsUpdated.json`, and
  `EventItemCreated_WithAlarms.json` / `TaskItemCreated_WithAlarms.json`. The existing
  `EventItemCreated.json`, `TaskItemCreated.json` and `TemplateTaskItemCreated.json` change
  deliberately (`"alarms": null` or `[]`). That is the reviewed diff the suite exists to catch.
- **Old history**: a test that rehydrates an `EventItemCreated` JSON without `alarms` and a snapshot
  row without `Alarms`, and gets `ItemAlarms.None`.
- **`UpdateItemAlarms`** (Alba): `200` sets, `200` clears with `[]`, no-op appends nothing, `400`
  for 4 alarms or an offset out of range, `403` for a `View` member, `404` for a missing item and
  for a calendar the caller can't see.
- **`GetIcalFeed`**: an item with two alarms renders two `VALARM`s with the right `TRIGGER`s. A
  sibling without alarms renders none. A repeating item has them on every occurrence. A routine has
  them only on the first subtask. All-day `+540` renders `TRIGGER:PT9H`.
- **Frontend**: `calendars.service` spec for the PATCH URL/body. Agenda specs for the presets by
  mode, clearing on all-day toggle, the 3-alarm cap, and "only PATCH when changed".
- **e2e**: a guardian creates an event with "2 hours before", reopens it and sees the chip
  selected, and the feed (fetched with `request` through an iCal token) contains `TRIGGER:-PT2H`.

## Failure and edge-case behavior

| Case | Behavior |
|---|---|
| Item rescheduled | Alarms kept, re-anchored on the new start or due time. |
| Item switched timed ↔ all-day through the API | Alarms kept as stored. The UI clears non-matching presets before saving. |
| Occurrence skipped | Not in the feed, so no alarm. |
| Item deleted | Not in the feed. |
| Same offset sent twice | Merged; stored once. |
| `alarms` omitted on create | No alarms. |
| Subscriber's client ignores `VALARM` | Nothing Buddy can detect. The UI hint covers it. |
| Alarm in the past when the feed refreshes | The client decides (most skip it). |

## Estimate

| | |
|---|---|
| Complexity | Low to Medium -- one new event and slice, an optional field on three creation events, the feed writer, and a picker in two forms. No new aggregate, schema or route group. |
| Single developer | 2-3 days |
| AI agent | 1.5-2.5 hours, plus about 45 minutes of human review |

## Decisions made

| Question | Decision |
|---|---|
| Where alarms live | `CalendarItem.Alarms`, own event `ItemAlarmsUpdated`, own slice `PATCH .../alarms`; optional on the three creation events |
| Relation to transition warnings | None. Separate concept, separate data |
| Alarm shape | Signed whole-minute offset from start/due (all-day: local midnight), max 3, distinct, `-40320..+1440` |
| Feed output | One `VALARM` (`DISPLAY`, title as description) per offset; none when the item has none |
| Repeating items | Every occurrence carries the alarms |
| Who can set them | `Manage` tier on the calendar |
| UI | Preset chips in agenda create/edit and schedule-from-template, a bell on the agenda row, a one-line client-support hint |
| Routines and packing lists | Alarms only on the first subtask's `VTODO` of each run, so a routine alerts once |
| Custom offsets | Presets only in v1; the API accepts any offset in range, so a custom input needs no backend change later |
| Per-calendar default alarm | Not in v1 |
| Bell on agenda rows | Shown, with `aria-label` "Has alarms"; doc screenshots re-run |

## Remaining open questions

None. The four questions raised with the analysis were settled on 2026-10-15 (see the last four
rows of the decisions table).

## Diagram

```mermaid
flowchart TB
    subgraph Guardian["Guardian agenda"]
        Form["Create / edit form<br/>alarm preset chips"]
    end
    subgraph Api["Features/Calendars"]
        Create["CreateItem / ScheduleTaskFromTemplate<br/>(alarms optional)"]
        Patch["UpdateItemAlarms<br/>PATCH .../items/{id}/alarms (Manage)"]
        Item["CalendarItem.Alarms"]
        Expand["CalendarOccurrenceExpansion<br/>occurrence.Alarms"]
        Feed["IcalFeedWriter<br/>VALARM per offset"]
    end
    Client["Subscribed calendar app<br/>(Apple, Thunderbird honour it;<br/>Google ignores it)"]

    Form --> Create --> Item
    Form --> Patch -->|ItemAlarmsUpdated| Item
    Item --> Expand --> Feed -->|GET /calendars/{id}/ical/{token}| Client
```
