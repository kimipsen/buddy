# Sleep diary printing

Status: Implemented. The shared `app-sleep-diary-sheet`, the `SleepNightDuty` service, the print page at
`/guardian/sleep-diary/print/:childId`, the Print link on the sleep diary page, the help section, the
documentation screenshot and the Vitest and Playwright specs shipped together (GitHub issue #97).

A guardian prints a child's sleep diary for a period they choose, to bring to the sleep clinic. The
data model, the share link and the doctor's read-only page are designed in
[sleep-diary.md](../../backend/analysis/sleep-diary.md); this page covers the Angular side. No endpoint,
DTO, event or OpenAPI change was needed: every read already existed and is in range.

## Routes

| Route                                                         | Component             | Shell                       |
| ------------------------------------------------------------- | --------------------- | --------------------------- |
| `/guardian/sleep-diary`                                       | `SleepDiary`          | Inside `GuardianShell`      |
| `/guardian/sleep-diary/print/:childId?from=YYYY-MM-DD&to=...` | `SleepDiaryPrintPage` | **Outside** `GuardianShell` |

The print route is a sibling of the shell route in `GUARDIAN_ROUTES`, like the week plan and house rules
printouts, so no navigation or header ends up on paper. It sits behind `featureGuard('sleepDiary')`. The
period is kept in the URL, so a reload or a bookmark keeps it. A missing or invalid `to` falls back to
today, and a missing or invalid `from` to 13 days before `to`: the history's default 14-night window.

## Data loading and limits

The page uses the endpoints the sleep diary page already uses:

- `listMyChildren()` for the child's name.
- `listEntries(childId, from, to)` for the nights, in one read.
- `SleepNightDuty.load(childId, from, to)` for who was on duty. It reads in windows of at most 31 nights,
  because `ListNights` serves at most 31 days per read, and resolves to `null` on failure so a night duty
  problem never blocks the printout.

A printout covers at most **92 nights**, both ends included (the visit summary's period). The pure
`printPeriod(from, to)` returns `valid`, `missing`, `reversed` or `tooLong`. It counts calendar days with
`addDaysIso` and never with a `getTime()` difference, which is an hour off across a daylight saving
change. A period may end after today, so a guardian can print blank rows to fill in by hand.

## The sheet

`app-sleep-diary-sheet` (in `shared/`) holds the table and the sleep hygiene notes. The doctor's share
page uses it too and behaves as before.

- One row per night with the clinic form's columns in their order, then an optional **On duty** column
  right after Weekend. It is filled on logged nights and empty on unlogged ones.
- A4 **landscape** with 10 mm margins, set through `injectPrintPageStyle`. The 12 to 13 columns with
  no-wrap time cells need about 1,000 px, which portrait's 190 mm can't give.
- Print rules: the column headings repeat on every page, and no night is split across two pages.

## The entry point and the page

A **Print** link on the sleep diary page, next to Previous and Next, opens the print page with the window
on screen and the selected child. The print page has a toolbar that doesn't print: a back link, From and
To date fields, and a Print button that calls `window.print()`. An invalid period shows one message and
no sheet; loading and errors show no sheet and no Print button.

## Localization

The `sleepDiary.print.*` and `sleepDiary.shared.columns.nightDuty` strings, and the `printDiary` help
section, exist in English and Danish. Dates use `Intl.DateTimeFormat` with the guardian's language.

## Testing

- Vitest: `print-period`, `SleepNightDuty` (windowing), the sheet, the print page and the Print link, plus
  the feature route case in `feature-routes.spec.ts` and `help-coverage.spec.ts`.
- Playwright: one spec prints a chosen period and checks the sheet and the period rules.
- `task docs:screenshots` covers the page as `guardian-sleep-diary-print`.

## Accessibility

The From and To fields have the visible text as their accessible name. The Print link and button are
native, focusable and work with Enter. An invalid period message uses `role="alert"`.

## Decisions

- The sheet is landscape, not portrait, so every column stays readable.
- The night duty column follows Weekend and is filled on logged nights only, matching the history.
- The entry point is a link, since opening a page is navigation; the print page's Print is a button.
- The night duty loading moved into a root service, `SleepNightDuty`, so both pages share one copy.
- No backend change.

## Remaining open questions

- A generated PDF file, and the paper form's exact 14-day grid, stay open. See
  [sleep-diary.md](../../backend/analysis/sleep-diary.md#remaining-open-questions).

## Estimate

The complexity is low to medium. A single developer needs 2–3 days. An AI agent needs 2–3 hours, plus
about 30 minutes of human review: printing a 60-night period in two browsers.
