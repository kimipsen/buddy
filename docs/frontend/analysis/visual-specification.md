# Visual specification

Status: Proposed (not yet implemented)

Buddy has two audiences with almost nothing in common visually: guardians
doing precise record-keeping and children glancing at a screen they can't
necessarily read yet. Today that split exists at the route level
(`features/guardian` vs. `features/child`,
[frontend README](../README.md#feature-layout)) but not as a documented
visual language -- there's no shared rule for, say, "a boolean is a toggle,"
so each feature reinvents its own controls (see
[Gaps and recommendations](#gaps-and-recommendations) below). This records
that language: one visual treatment per data shape, and how it diverges
between guardian and child screens.

## Two audiences

| Dimension | Guardian | Child |
| --- | --- | --- |
| Density | Dense: tables, agenda grids, multi-field inline forms | Sparse: one task or one message per screen |
| Primary controls | Precise native inputs (select, checkbox, date/time) | Big tap targets, few or no typed fields |
| Iconography | Line SVGs for navigation and chrome | Emoji as the primary icon set |
| Color use | Semantic only -- emerald = active/confirm, red = destructive | Decorative too -- color marks progress and celebration |
| Motion | Minimal, functional (focus rings, transitions) | Celebratory (badge bounce on milestones) |
| Copy tone | Neutral, task-labeled ("Edit pickup", "Add medicine") | Encouraging, second-person ("You did it!") |
| Editing model | Inline edit-in-place, no modals | View and react; rarely edits raw data |

Rule of thumb: if a screen asks someone to *record accurate information*,
it's a guardian pattern. If it asks someone to *see how they're doing*, it's
a child pattern.

## Data type to component map

Each data shape gets one visual treatment, used everywhere it appears -- a
boolean shouldn't look like a checkbox in one feature and a switch in
another. Children almost never type raw data; where a type has no child
input, the child app only ever displays it.

| Data type | Guardian input | Child-facing view | Notes |
| --- | --- | --- | --- |
| Boolean (flag, done/not done) | Toggle switch | Filled icon or star when true, outline when false | Replaces today's `<input type="checkbox">` -- a checkbox reads as "select this item," a toggle reads as "this is currently on" |
| Single date | Date field with calendar icon | Plain day label, not editable | Wraps the existing `date-select` component |
| Single time | Time field, 12-hour display | Rounded time chip | Wraps the existing `time-select` component |
| Time range (e.g. bedtime-ritual window) | Two time fields joined by an arrow ("7:30 PM -> 8:00 PM") | Not shown as a range -- collapse to the single relevant time | New composite control; no existing equivalent |
| Repeatable time+duration entries (wake-ups, naps) | Stacked rows, each a time + duration pair, with "+ Add" below and a remove control per row | Tally only -- a count badge ("2 wake-ups"), never the individual rows | New repeatable-row pattern; nothing like it exists yet |
| Computed duration (e.g. total sleep) | Read-only pill, never an editable field | Large friendly number with a progress ring or star | Visually distinct from input fields so it reads as derived, not entered |
| Closed set, <= 4 options (e.g. pickup assignee) | Segmented control | Icon or avatar of the chosen option only | Replaces raw `<select>` when options are few and known in advance |
| Closed set, > 4 options | Dropdown (styled, not bare `<select>`) | Icon or avatar of the chosen option only | |
| Short free text (name, location, contact) | Single-line text input | Not shown as text -- summarized visually (e.g. an avatar) | |
| Long free text (remarks, notes) | Auto-expanding textarea | Not shown | |
| Quantity / dosage | Stepper (- / value / +) | Not shown | Replaces free-typed numeric fields for small bounded counts |
| Color (e.g. medicine schedule tag) | Preset swatch grid, not the native color picker | Color used as a border or background accent, never named | |
| Icon / emoji tag | Emoji picker grid | The emoji itself, shown large | |
| Status or streak (derived) | Colored badge, label + color, never color alone | Gamified badge with stars and animation (`progress-badge`) | Guardian badges inform; child badges celebrate -- same data, different weight |

## Component states

One rule holds across every control: the focus ring is always emerald, even
on a destructive or error control -- focus state and validation state must
never fight for the same color.

| Control | Default | Focus | Error | Disabled |
| --- | --- | --- | --- | --- |
| Text input / textarea / date / time field | Slate-300 border, slate-50 fill | Emerald-500 border + ring-2 emerald-500/300 | Red-500 border, red-600 helper text below | Slate-100 fill, slate-400 text, no pointer cursor |
| Toggle | Slate-300 track, thumb left | Emerald ring around the whole control | N/A (booleans don't validate) | Slate-200 track, thumb frozen, 60% opacity |
| Dropdown / segmented control | Slate-300 border; selected segment emerald-500 fill, white text | Ring on the focused segment only | Red-500 border on the control | Slate-100 fill, muted text |
| Stepper | Slate-100 buttons | Emerald ring on the focused button | N/A | Grey out only the button at its bound (max/min), not the whole control |
| Primary button | Emerald-600 fill, white text | Emerald ring offset outside the button | -- | Slate-300 fill, slate-500 text, no hover |
| Destructive button | Red-600 fill, white text | Emerald ring (not red) | -- | Slate-300 fill, slate-500 text |
| Badge / pill | Paired color + label, never color alone (e.g. emerald-50 fill + emerald-700 text + the word "Done") | -- | -- | -- |

`color-scheme: light/dark` in [`styles.css`](../../../src/frontend/buddy/src/styles.css)
already makes native widgets (checkbox, date/color pickers, scrollbars)
follow the theme automatically. Every custom component (toggle, stepper,
segmented control, badge) doesn't get this for free and must ship its own
`dark:` variant -- typically one step darker on fills (`emerald-600` ->
`emerald-500`) and one step up on borders (`slate-300` -> `slate-700`),
matching the pattern already used by
[`theme.service.ts`](../../../src/frontend/buddy/src/app/core/theme.service.ts)
and its consumers.

## Color, iconography, and typography

**Palette** stays three hues, used semantically everywhere, in both light
and dark: emerald for primary/active/focus, slate for neutral text/borders/
surfaces, red for destructive actions and errors only. No new hue gets
introduced for a single feature.

**Iconography splits by audience, not by screen area:**

- Guardian chrome and navigation: monochrome line SVGs (the existing
  Heroicons-style set), inheriting `currentColor` so they theme
  automatically.
- Child-facing UI: emoji as the primary icon vocabulary (already used for
  medicine, celebration, pickup, milestones) -- legible to a pre-reader,
  needs no icon library, and reads as playful rather than clinical.
- Never mix the two for the same concept on one screen. A pickup shown as an
  SVG walking-icon to a guardian should still be an emoji to a child, not
  the same SVG recolored.

**Typography** uses the system font stack (no custom font loaded) -- keep
it; it's fast and legible cross-platform. Scale differs by audience:
guardian screens can run tighter (`text-sm`/`text-base`) to fit dense
tables; child screens should sit a step larger (`text-lg` minimum) and
favor semibold over regular weight, since the audience is reading faster
and with less precision.

**Touch targets:** guardian controls can follow standard web sizing
(~32-36px); child-facing controls should be at least 44x44px, reflecting
less precise motor control.

## Accessibility baseline

Carried forward from the current codebase's conventions, and binding for
every new component in this spec:

- Every icon-only control (SVG or emoji) gets an `aria-label`; decorative
  icons next to a text label get `aria-hidden="true"` instead so screen
  readers don't read them twice.
- Toggle switches use `role="switch"` with `aria-checked`, not a bare styled
  checkbox, as `shared/toggle` does.
- Stateful custom buttons (menus, segmented controls) carry
  `aria-pressed` / `aria-expanded` / `aria-haspopup` as appropriate,
  matching the pattern already used in `child-menu.ts`.
- Focus is always visible: `focus:ring-2` (or `ring-4` for larger touch
  targets) in emerald, never removed even on custom controls.
- Color never carries meaning alone -- every status badge, toggle, or error
  state pairs its color with a label, icon, or text, so the interface still
  works for color-blind users.
- Child-facing controls meet the 44x44px minimum touch target in addition
  to the above, since the accessibility need there is motor precision as
  much as vision.

## Gaps and recommendations

Most of the components this spec assumes now exist as shared components in
`src/app/shared`, following the pattern set by `shared/date-select` and
`shared/time-select`. That is what makes the rest of this document
enforceable rather than aspirational. As of October 2026:

| Component | State | Used by |
| --- | --- | --- |
| Toggle switch | Built: `shared/toggle`. No template uses a bare `<input type="checkbox">` any more | 10 templates, including the Sleep Diary's "seemed tired" flag |
| Segmented control | Built: `shared/segmented-control` | 8 templates, including `PickupAssigneeKind` in `pickup-cell` |
| Repeatable row group | Built: `shared/repeatable-row` | Sleep Diary wake-ups and naps, medicines, task subtasks, print templates |
| Time-range control | Built: `shared/time-range` | Sleep Diary bedtime-ritual window |
| Stepper | Built: `shared/stepper` | Task library, onboarding task step, agenda, print templates |
| Color swatch picker | Built: `shared/color-swatch-picker` | 8 templates (meals, medicines, tasks, work locations, agenda, print templates) |
| Styled dropdown | **Missing.** About 50 raw `<select>` elements in 20 templates, mostly for choosing a person, child, group or calendar from a list that isn't known in advance | Every closed set with more than four options |
| Modal / dialog | One so far: the confirmation in `admin/delete-account`. Editing still happens inline | Only introduce another if a flow can't be done inline. [house-rules.md](../../backend/analysis/house-rules.md#frontend) proposes a rule editor dialog |

The remaining free-typed number inputs (Sleep Diary wake-up minutes and
total-slept hours/minutes, progress goal thresholds) are wide-range values,
not the small bounded counts the stepper is for, so they stay as number
fields.

Also worth doing regardless of any single feature: promote the `emerald` /
`slate` / `red` convention from "used consistently by habit" to actual CSS
custom properties or a Tailwind theme block, so a future rebrand or a
fourth semantic color doesn't mean auditing every template by hand.

## Worked example: Sleep Diary

[Sleep Diary](../../backend/analysis/sleep-diary.md) (built: `/guardian/sleep-diary`) shows
why; its data model touches nearly every row in the map above on one
screen:

| Field | Data type | Guardian entry | Child view | Notes |
| --- | --- | --- | --- | --- |
| Seemed tired (yes/no) | Boolean | Toggle | Not shown as a field -- folded into the child's daily mood badge if surfaced at all | First real use case for the toggle component this spec calls for |
| Getting-ready time | Single time | Time field | Not shown | |
| Bedtime-ritual window | Time range | Time-range control (start -> end) | Collapsed to a single "bedtime" chip | First real use case for the time-range control |
| Put-down time | Single time | Time field | Not shown | |
| Fall-asleep time | Single time | Time field | Not shown | |
| Night wake-ups (variable count) | Repeatable time+duration entries | Stacked rows, "+ Add wake-up" | Tally badge, e.g. "1 wake-up" | First real use case for the repeatable-row component |
| Daytime naps (variable count) | Repeatable time+duration entries | Same repeatable-row component as wake-ups | Nap count or a simple nap icon per nap taken | Reuses the wake-up component -- same shape of data |
| Morning wake time | Single time | Time field | Rounded time chip | The one sleep-related time worth showing a child directly -- it frames their day |
| Total time slept | Guardian estimate, prefilled | Hours + minutes fields prefilled with a suggestion computed from the times above, plus "Use suggestion" once edited | Large number with a progress ring against an age-appropriate target | Built as an editable estimate, not a read-only pill: parents estimate overnight sleep, so the backend stores the guardian's number ([sleep-diary.md](../../backend/analysis/sleep-diary.md), Question 3) |
| Per-day remarks | Long free text | Auto-expanding textarea | Not shown | |
| Sleep hygiene note (diary-wide, not per day) | Long free text | Auto-expanding textarea, placed outside the daily entry -- visually separated so it reads as a setting, not a log row | Not shown | Keeping it out of the per-day table avoids implying it changes nightly |

Layout order should mirror the night itself, top to bottom: getting-ready ->
bedtime ritual -> put-down -> fall-asleep -> wake-ups/naps -> morning wake ->
total slept -> remarks. A guardian scanning the form should be able to
reconstruct the night in reading order without cross-referencing labels.

## Estimate

| | |
|---|---|
| Complexity | Medium -- almost every component in [Gaps and recommendations](#gaps-and-recommendations) is built, so what remains is broad rather than deep: a styled dropdown and the migration of about 50 raw `<select>` elements in 20 templates (any with four or fewer fixed options become segmented controls), semantic color tokens in place of several hundred hard-coded `emerald`/`slate`/`red` classes, a child-screen pass for `text-lg` and 44x44px targets, and regenerated screenshots |
| Single developer | 5-8 days |
| AI agent | 3-5 hours, plus 2-3 hours of human review, mostly checking the regenerated screenshots in light and dark |
