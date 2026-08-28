# Task library

Status: Implemented

Guardians define reusable per-child routines -- ordered task templates with
timed subtasks -- and schedule them onto the calendar as a single specific-time
task with consecutive subtask occurrences. The backend lifecycle,
authorization, and event model are documented in [Task Library
flow](../../backend/task-library/flow.md); this page records the implemented
Angular behavior and its one integration point outside the library screen
itself.

## Guardian screen

`/guardian/task-library` renders
[`GuardianTaskLibrary`](../../../src/frontend/buddy/src/app/features/guardian/task-library/task-library.ts),
a thin page shell (back link plus heading) around
[`ManageTasks`](../../../src/frontend/buddy/src/app/features/guardian/task-library/manage-tasks/manage-tasks.ts),
which does all the loading and editing. `ManageTasks` loads the guardian's
linked children, selects the first automatically, and offers a child switcher
only when there is more than one child. There is no group-sharing axis here
(unlike mealplans) -- a template belongs to exactly one child, so there is no
scope selector to load alongside the child list.

Changing the selected child calls
[`TaskLibraryService.listTaskTemplates(childId)`](../../../src/frontend/buddy/src/app/core/task-library.service.ts),
which replaces a shared `templates` signal rather than component-local state.
Every component reading `taskLibrary.templates()` -- `ManageTasks` and the
calendar agenda's template picker (below) -- sees the same list, so creating,
editing, archiving, or changing a template's subtasks anywhere is immediately
reflected everywhere else without a manual refetch.

Per template, the screen supports:

- **Create**: name, icon, and color; the new template starts with no subtasks
  and expands automatically once created.
- **Rename/recolor**: inline edit of name, icon, and color (`PATCH
  /task-templates/{templateId}`). Disabled once a template is archived.
- **Archive**: soft delete (`DELETE /task-templates/{templateId}`). Archived
  templates remain in the list, visually dimmed with an "archived" badge,
  rather than disappearing -- there is no un-archive, so the guardian can still
  see what happened to it. Rename and archive controls are hidden once a
  template is archived, but its subtasks remain viewable.
- **Timed subtasks**: add (with optional title, icon, and a whole-minutes
  duration), inline edit, and remove, scoped to one expanded template at a
  time.
- **Reorder subtasks**: up/down buttons swap a subtask with its neighbor and
  submit the full resulting ID order via `PUT
  /task-templates/{templateId}/subtasks/order` -- there is no drag-and-drop
  precedent elsewhere in the app to reuse, so this is the simplest correct v1.

Durations are edited and displayed as whole minutes
(`taskLibrary.service.ts`'s `parseDurationMinutes`/`formatDurationMinutes`
convert to and from the backend's `TimeSpan` wire format, e.g. `"01:30:00"`),
so components never handle the wire string directly. A template's total
duration is the sum of its subtask durations, kept in sync locally on every
add/update/remove without a full reload.

## Template picker

[`TaskPicker`](../../../src/frontend/buddy/src/app/features/guardian/task-library/task-picker/task-picker.ts)
is a searchable dropdown over a `templates` input, mirroring the calendar's
existing `MealPicker` contract (`templates`/`templateId`/`disabled` inputs, a
`templateIdChange` output, fixed-position dropdown anchored to the trigger).
It additionally surfaces each result's subtask count and total duration next
to its name, since a task template's subtask list is worth previewing before
picking it. It is a dumb component -- it does not fetch templates itself, only
renders whatever list its caller passes in.

## Calendar integration: scheduling from a template

The guardian calendar agenda's create-task form
([`agenda.ts`](../../../src/frontend/buddy/src/app/features/guardian/calendar/agenda/agenda.ts))
embeds `TaskPicker` as a second way to create a task, alongside the existing
manual title/icon/color entry. A `newTaskSource` signal (`'manual' |
'template'`) toggles between them; switching to `'template'` also forces
`newIsAllDay` off, since a template-scheduled task is never all-day.

The picker's template list (`taskTemplates`, filtered to non-archived) is
scoped to whichever assignee is currently selected in the form's `newAssignedTo`
field -- **not** to a fixed child or to the guardian's first child. A
constructor `effect()` tracks `newAssignedTo`, resolves it against the
guardian's loaded children, and:

- if it matches one of the guardian's children, calls
  `taskLibrary.listTaskTemplates(childId)` for that child;
- otherwise (a non-child assignee, such as another guardian, or no assignee
  selected at all) calls `taskLibrary.clearTemplates()`.

This matters because a `TaskTemplate` belongs to exactly one child, so there
is no single library to fall back to. Without the clearing branch, switching
the assignee away from a child (or to "unassigned") would leave a stale
child's templates showing in the picker as if they applied to the new
selection. `clearTemplates()` (added alongside this integration) exists on
`TaskLibraryService` specifically for this case -- it resets the shared
`templates` signal to empty rather than leaving the last-loaded child's
templates in place.

Picking a template (`onTemplateSelected`) is a one-time copy, not a live
binding: it pre-fills `newTitle`/`newIcon`/`newColor` from the chosen
template so the guardian can still edit them afterward, and the picker will
not overwrite those fields again on its own.

Submitting in template mode calls
[`CalendarsService.scheduleTaskFromTemplate(calendarId, ...)`](../../../src/frontend/buddy/src/app/core/calendars.service.ts)
(`POST /calendars/{calendarId}/items/from-template`) instead of `createItem`,
passing the picked `taskTemplateId`, the due date/time, recurrence, and
assignee. The backend stores the template reference rather than copying
subtasks, so later subtask edits in the task library affect any calendar item
already scheduled from that template; the agenda renders that template's
subtask occurrences as one bracketed run rather than one row per subtask (see
`core/task-run.ts`).

## Current limitations

- No un-archiving a template once archived.
- Subtask reordering is neighbor-swap only; there is no drag-and-drop or
  multi-position move.
- The template picker's per-assignee scoping depends on the calendar form's
  `children` list being loaded; until that load resolves, a child assignee's
  templates will not yet appear.
- No sharing of templates across siblings -- a template's owning child never
  changes after creation.
