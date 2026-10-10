# Frontend documentation

This folder documents the Angular frontend for Buddy.

## Purpose

The frontend is a role-aware single-page app that supports two main user flows:

- guardian flows for creating and managing child accounts, shared calendars, and household coordination
- child flows for seeing a personalized day view and linked guardians

The app authenticates with Keycloak and then calls the Buddy API with the issued access token.

## App shell

The app is bootstrapped in [src/frontend/buddy/src/app/app.config.ts](../../src/frontend/buddy/src/app/app.config.ts). It wires together:

- router configuration
- HTTP client with auth interceptor
- runtime config loader, then the installation's feature flags (`FeaturesService`)
- browser app initialization

Each installation can turn optional features off ([feature flags](../backend/analysis/feature-flags.md)).
`FeaturesService.enabled(name)` reads the flags from `GET /features`. `featureGuard(name)` on every
route of an optional feature redirects to `/` while it is off, and the links, dashboard cards,
child-home sections, onboarding steps, help topics and print row kinds of a disabled feature are
hidden. Specs stub the flags with `provideFeatures(...)` / `disableFeatures(...)` from
`src/testing/features-fixture.ts`.

The top-level route setup is in [src/frontend/buddy/src/app/app.routes.ts](../../src/frontend/buddy/src/app/app.routes.ts). It divides the app into:

- `/login` for unauthenticated users
- `/invite/:token` for viewing and accepting group invitations, including the logged-out preview path
- `/shared/sleep-diary/:token` — the read-only, printable sleep diary a guardian shared with a doctor; no login, the token is the only credential
- `/verify-email/:token` for completing email verification, including the logged-out verification path
- `/guardian` for guardian dashboard screens
- `/child` for child-focused screens
- root redirect logic based on resolved role

## Authentication flow

Authentication is centered on the services in [src/frontend/buddy/src/app/core](../../src/frontend/buddy/src/app/core):

- `AuthService` handles the Keycloak authorization code exchange, token refresh, and logout
- `RuntimeConfigService` loads config from `/config/runtime-config.json` (Keycloak and API URLs,
  plus the optional `version` and `repositoryUrl` shown in the profile menu)
- `authGuard` blocks unauthenticated access
- `roleRedirectGuard` sends users to the correct route after the account role is resolved

The actual role is derived from guardian relationships, not from a stored role flag. See [src/frontend/buddy/src/app/core/account.service.ts](../../src/frontend/buddy/src/app/core/account.service.ts).

## Feature layout

The Angular app currently has guardian, child, login, invitation, and email-verification feature areas.

### Guardian feature

The guardian dashboard is implemented in [src/frontend/buddy/src/app/features/guardian](../../src/frontend/buddy/src/app/features/guardian).

Current responsibilities:

- display a dashboard shell
- list linked children
- create child accounts and capture the one-time temporary password, and reset a child's password
  to a new one-time password (shown once, like the first)
- show today's events, tasks, and medicine doses
- manage meals and assign them to shared meal-plan slots
- create, list, and revoke private iCal subscription links for the family's meal plan, with
  one-click `webcal://` and Google Calendar subscribe buttons (also offered for calendar iCal links
  in the admin area)
- start a chat-based AI assistant session to draft meal-plan assignments over a
  date range, optionally limited to rated meals and/or meals served in the last 30/60/90 days,
  then apply or discard the resulting draft
- manage pickup and drop-off assignments for linked children
- keep a sleep diary per child (one row per night, diary-wide sleep hygiene notes) and share it
  with a doctor through a revocable, optionally expiring link
- browse day, work-week, rolling-week, and month calendar views and create events/tasks across
  every calendar they can contribute to, personal or group-owned
- create, edit, and archive per-child task templates and their timed subtasks, and schedule them
  onto a calendar from the agenda's create-task form
- manage calendars, groups, children, and the current profile from the admin area
- manage the family's BYOK AI provider settings (add/remove keys, switch the
  active provider, test a connection) from the admin area
- delete a child they alone guard (account and data), and delete their own account after a
  dialog that lists what goes with it: children with no other guardian, and owned groups that
  pass to another member or are deleted
- download everything Buddy holds about them and their children as a JSON file ("Your data" in
  the admin area)
- sign out of the current session

The route definition is in [src/frontend/buddy/src/app/features/guardian/guardian.routes.ts](../../src/frontend/buddy/src/app/features/guardian/guardian.routes.ts).

The guardian routes currently include:

- `/guardian` — dashboard and today's operational summary; a guardian with no groups and no
  children (or with a started guide) is sent to `/guardian/onboarding` instead, and a deferred
  guide shows a "Resume setup" card
- `/guardian/onboarding` — the guided first-login setup: group, children, optional invitations,
  shared calendar, a first routine, a meal plan, summary
  ([design](analysis/guardian-onboarding.md))
- `/guardian/mealplan` — meal library and meal-plan assignment
- `/guardian/mealplan/ai-assistant` — chat-based AI assistant for drafting
  meal-plan assignments; before the family's first session it shows what is
  shared with the provider (`AiDataSharingNotice`, also on the settings page)
  and asks a guardian to acknowledge it; the start form's meal filter (rated
  only, served in the last 30/60/90 days) is remembered per device
- `/guardian/mealplan/import` — import older meal plans from a pasted note or
  CSV: preview, per-meal review, import and undo
- `/guardian/medicine` — medicine schedule management
- `/guardian/pickup` — rolling seven-day pickup and drop-off assignment planner
- `/guardian/sleep-diary` — log a night, review 14 nights at a time, hygiene notes and share links
- `/guardian/house-rules` — household and personal house rules in markdown: pick a household or a child, see who has read each rule, add/edit (with a "small fix" that keeps the children's ticks)/move/remove, and read a rule with a child who can't read yet
- `/guardian/house-rules/print/children/:childId`, `/guardian/house-rules/print/groups/:groupId` — A4 portrait printouts for the fridge, outside the shell like the week-plan sheet
- `/guardian/babysitters` — the guardian's saved babysitters and nannies, which any of a child's
  guardians can pick in the pickup planner
- `/guardian/work-locations` — the guardian's own work locations, alternating weekly pattern, and
  per-day exceptions (used by the printable week plan; never shown to children)
- `/guardian/print` — quick print: pick a template and a start date, preview, print; create templates
- `/guardian/print/templates/:templateId` — the print template editor with a live preview
- `/guardian/print/sheet/:templateId?start=` — the printable sheet, deliberately outside the
  guardian shell so no navigation ends up on paper
- `/guardian/calendar` — day, work-week, rolling-week, and month views across every accessible
  calendar, plus event/task creation
- `/guardian/task-library` — per-child task template and subtask management
- `/guardian/admin` — profile, child, calendar, group, AI provider, data export, and account administration
- `/guardian/help` — every in-app help topic on one page; `?topic=<id>` scrolls to one. Every other
  guardian page (except onboarding) names its topic in its route's `data.helpTopic`, and the shell
  header's "?" button expands that topic inline above the page
  ([design](analysis/in-app-help.md))

### Child feature

The child-facing area is implemented in [src/frontend/buddy/src/app/features/child](../../src/frontend/buddy/src/app/features/child).

Current responsibilities:

- display the child home screen
- show the child's day view, including meals and ratings, medicine doses,
  pickup/drop-off assignments, and tasks when available
- let the child browse current and previous meal-plan weeks and rate meals
- show guardian links when available
- provide the child-specific entry route
- offer a menu (top-right of the child shell) for theme selection and sign out

The route definition is in [src/frontend/buddy/src/app/features/child/child.routes.ts](../../src/frontend/buddy/src/app/features/child/child.routes.ts).

The child routes currently include:

- `/child` — today's operational view and relationship summaries
- `/child/mealplan` — current/historical meal plans and the child's own ratings
- `/child/calendar` — read-only seven-day agenda across accessible calendars
- `/child/rules` — the child's own and each household's rules, with "New"/"Changed" chips and an "I've read this" button; the child home links to it and shows a "N rules to read" card

### Login feature

The login screen is in [src/frontend/buddy/src/app/features/login](../../src/frontend/buddy/src/app/features/login). It starts the Keycloak redirect flow and keeps the sign-in UX cleanly separated from the rest of the app.

When a session ends mid-use (Keycloak rejects the refresh token, or the API answers 401), the auth interceptor and `authGuard` (which runs as both `canActivate` and `canActivateChild` on `/guardian` and `/child`) send the user to `/login?reason=session-expired`, which shows a notice. After signing in, `roleRedirectGuard` returns them to the interrupted page if it's inside their own role tree (`core/pending-return-url.ts`). See [Expired sessions during in-app navigation](analysis/expired-session-handling.md).

### Invitation and email verification features

The invitation flow is in [src/frontend/buddy/src/app/features/invite](../../src/frontend/buddy/src/app/features/invite). It supports a public invitation preview and returns the user to the invitation after login so the invitation can be accepted in an authenticated session. Accepting a group or guardian invite requires a verified email address that matches the invited one; otherwise the API answers `403`. For `403 email_not_verified` the page asks the user to verify their email and come back to the link (`features/invite/email-not-verified.ts`) and offers a button that sends the verification email again; any other `403` shows the wrong-account error.

The email verification flow is in [src/frontend/buddy/src/app/features/verify-email](../../src/frontend/buddy/src/app/features/verify-email). It uses the same public-route pattern: a user can open a verification link while logged out, sign in if needed, and return to the pending token.

`shared/resend-verification` is the "Send verification email again" button (`POST /users/me/email/verify/resend`). It appears on the invite pages after a `403 email_not_verified`, and on the profile while the saved email is unverified. A `409 resend_cooldown` shows a "try again shortly" message.

### Start/end ranges

In every form with a start and an end (calendar create/edit, the sleep-diary bedtime ritual, work-day override ranges, the AI meal-plan period, medicine schedules), changing the start moves the end by the same amount, so the length is kept and the start can't end up after the end. Changing the end alone only changes the end. The pure helpers `shiftRangeEnd`, `shiftRangeEndDate` and `shiftRangeEndTime` in `core/date-utils.ts` do the arithmetic; `shared/time-range` uses the time variant, which may cross midnight.

## Shared services

The shared domain services live under [src/frontend/buddy/src/app/core](../../src/frontend/buddy/src/app/core). Their request
and response types are aliases of the API contract's schemas: `core/api/buddy-api.ts` is generated
from [docs/backend/openapi/buddy.json](../backend/openapi/buddy.json) with openapi-typescript
(`npm run api:types`, or `task docs:openapi` to regenerate the contract too), so a backend DTO
change shows up as a type error. Never edit the generated file; frontend CI fails if it is stale.
Enums arrive as member names (`'Owner'`, `'Dinner'`); only `kind` discriminators are numbers.

- `AccountService` resolves whether the user is a guardian or child
- `FeaturesService` loads the installation's feature flags (`GET /features`, anonymous, with
  `fetch` in an app initializer; every feature on if it fails) and answers `enabled(name)`
- `AiAssistantService` calls the AI provider-settings and AI mealplan-session
  endpoints (list/set/remove provider keys, test a connection, acknowledge data
  sharing, start a session, send a chat message, apply or discard the draft)
- `CalendarsService` lists accessible calendars and occurrences and manages
  calendar items and task completion
- `GroupsService` manages group membership, invitations, sharing policies, and
  group deletion
- `GuardiansService` calls the backend guardian endpoints, including deleting a child
- `MealplansService` calls meal-library, meal-plan, rating, group-sharing, iCal
  subscription-token and import (preview, commit, list, undo) endpoints
- `MedicinesService` manages medicine schedules, dose status, and group sharing
- `OnboardingService` reads and version-checks the guided setup's progress
  (`/users/me/onboarding`), decides whether a guardian enters the guide (`onboardingEntryGuard`
  on the guardian home), and derives each step's completion from the existing domain services
- `PickupsService` lists, assigns, and clears pickup/drop-off occurrences
- `HouseRulesService` lists, adds (`postIdempotent`), edits, removes and reorders the rules of a child or group book, acknowledges a rule (for a guardian, on a named child's behalf), and loads everything one child is asked to keep
- `SleepDiaryService` logs, clears, and lists sleep diary nights, saves the hygiene notes, manages
  share links, and reads a shared diary anonymously by token
- `BabysittersService` manages the guardian's own babysitters and lists the babysitters a child's
  guardians can pick for that child
- `PrintTemplatesService` manages print templates (layout, rows, guardian name colors); the
  `WeekPlanLoader` in `features/guardian/print` composes a week from the existing services
- `WorkLocationsService` manages the guardian's work locations, pattern, and exceptions, and lists
  resolved work days for the guardian or a co-guardian
- `TaskLibraryService` manages per-child task templates and subtasks, and backs the
  template picker on the calendar agenda's create-task form
- `ProgressService` loads a child's star count, unlocked milestones, and resolved goal-post
  info for the progress badge, and lets a guardian configure a child's goal posts
- `UsersService` loads the current profile, language, and email-verification state, resends the
  verification email, loads the
  account-deletion preview, downloads the personal-data export, and deletes the account
- `postIdempotent` (`http-idempotency.ts`) wraps `HttpClient.post` for create-style calls
  (calendars, groups, children, meals, medicine schedules, task templates, invites, email
  verification, ...) with a fresh `Idempotency-Key` header per call and retries a transient
  failure (network error or `5xx`) with that same key, so a dropped response can't create a
  duplicate resource -- see [Idempotency-Key (POST)](../backend/http-status-codes.md#idempotency-key-post)
  for the backend side of this contract
- `AuthInterceptor` attaches the access token to outgoing requests
- `TranslationService` resolves the UI's current language (see Localization below)
- `ThemeService` resolves the active light/dark theme (see Theming below)

## Localization (i18n)

The app supports English and Danish. Static UI text is a translation key resolved through
[`TranslationService`](../../src/frontend/buddy/src/app/core/i18n/translation.service.ts) and the
`translate` pipe, e.g. `{{ 'profile.title' | translate }}`, rather than a hardcoded string.
Dictionaries live under `src/frontend/buddy/src/app/core/i18n/translations/{en,da}/`, one file per
feature area, merged in that language's `index.ts`. `translations/index.ts` compares the full set of
dotted key paths of `en` and `da` at the type level, so a key missing from or extra in either
language (at any nesting depth) fails `tsc`/`ng build` with an error naming the offending keys,
instead of silently falling back to the raw key at runtime. CI additionally runs
`.claude/skills/i18n/check-parity.mjs` (`.github/workflows/frontend-tests.yml`), which also fails
on `{placeholder}` mismatches between the two languages and warns about identical en/da text.

Only English ships in the initial bundle (the default, and the fallback). Danish is a lazy chunk
that `loadDictionary` fetches the first time it's needed; `TranslationService.setLanguage` switches
only once it has loaded, so the screen never mixes languages. The app initializer awaits the
browser language's dictionary, so a Danish browser's first screen is already Danish. The public
pages opened from emails and share links (invites, email verification, the shared sleep diary)
are lazy chunks too; only the login page is in the initial bundle.

The current language is a signal seeded from the browser's own language (`detectBrowserLanguage`
in `core/i18n/language.ts`) so the pre-auth login screen renders sensibly before any user is
known. Once `UsersService.ensureCurrentUser()` resolves, it's replaced with the signed-in user's
saved `language` from `GET /users/me` — itself defaulted from the browser's `Accept-Language`
header the first time that user's backend account was created (see
[docs/backend/users/flow.md](../backend/users/flow.md)), and changeable afterward from the
profile page (`PATCH /users/me/language`). A dynamic status/error message (e.g. "Name updated.")
is stored as a translation key on its signal rather than as literal text, and interpolated through
the pipe in the template using Angular's `as` narrowing, e.g. `@if (error(); as message) { {{
message | translate }} }` — a raw backend validation message (not a static UI string) is passed
through untranslated instead.

## Layout and breakpoints

Layout uses Tailwind's default breakpoints, mobile first (`sm` 640px, `md` 768px, `lg` 1024px,
`xl` 1280px), with no custom breakpoints and no `BreakpointObserver`. Every page must fit a 393px
phone; the documentation screenshot run fails on horizontal overflow. See
[responsive-layout.md](analysis/responsive-layout.md) for the plan and its decisions.

Two shared components hold the repeated spacing, so it is set in one place:

- **`app-page`** ([`shared/page`](../../src/frontend/buddy/src/app/shared/page/page.ts)) is the
  guardian page container: `mx-auto max-w-7xl`, side padding `px-4 sm:px-6 lg:px-8` and `py-8`.
  `width="3xl" | "4xl" | "5xl"` narrows the cap for form-like pages, and `backLink` plus a
  translated `backLabel` add the "Back to …" link above the content. The eyebrow, title and page
  actions differ between pages, so each page keeps them as its own content.
- **`app-card`** ([`shared/card`](../../src/frontend/buddy/src/app/shared/card/card.ts)) is the
  white guardian card: border, `shadow-sm` and padding `p-4 sm:p-6`. Its classes sit on the host,
  so a caller adds margins or a flex layout with its own `class`. The host is a plain element like
  the unnamed `<section>` it replaced; a card that should be a landmark adds `role="region"` and
  `aria-labelledby`.

On phones the guardian shell adds a bottom tab bar
([`shell/tab-bar`](../../src/frontend/buddy/src/app/features/guardian/shell/tab-bar/tab-bar.ts))
with Today, Calendar, Meals and Medicine (the last two follow their feature flags). It is hidden
from `sm` up, when printing and on onboarding (`data: { hideTabBar: true }`); the account menu in
the sticky header keeps every link on all sizes.

On wide screens the dashboard packs its cards into CSS columns (two from `lg`, three from `2xl`)
so short cards leave no holes; nothing is wider than `max-w-7xl`, so pages line up with the shell
header. The help page caps its running text at `max-w-prose`.

Child pages share **`app-child-page`**
([`features/child/child-page`](../../src/frontend/buddy/src/app/features/child/child-page/child-page.ts)):
the warm background, a header with the page title (a back link to `/child`, or the logo on the
home page) and controls projected with the `childPageActions` attribute, and a `max-w-3xl` content
column, padded `px-4 sm:px-6 lg:px-8`. Child cards (`rounded-3xl border-4`) are padded
`p-5 sm:p-8`.

Touch targets: guardian pages keep their mouse density and grow only on touch screens, through
Tailwind's `pointer-coarse:` variant. Small inline text actions (Edit, Delete, Remove, Revoke,
Undo) get a 44px tap area from a transparent `::after` that doesn't move the layout
(`pointer-coarse:relative`, `pointer-coarse:after:absolute`, `pointer-coarse:after:-inset-x-2`,
`pointer-coarse:after:-inset-y-3.5`). Keep at least `gap-2` between such actions (`gap-y-4` on touch
where they wrap). Child-facing buttons are at least 44px (`size-11`) on every device.

## Theming

The app supports light, dark, and system (OS-following) themes, chosen from the theme switcher in
the profile menu (top-right of the guardian shell) or, for a child, the equivalent menu
(top-right of the child shell). The selection is a `light` | `dark` | `system`
mode stored under `buddy_theme_mode` in `localStorage`
([`core/theme-storage.ts`](../../src/frontend/buddy/src/app/core/theme-storage.ts)) and resolved by
[`ThemeService`](../../src/frontend/buddy/src/app/core/theme.service.ts): `system` tracks
`prefers-color-scheme` live via a `matchMedia` listener, while `light`/`dark` override the OS
setting outright.

`App` applies the resolved theme by toggling a `dark` class on `<html>`, which Tailwind's
`dark:` variant is keyed off (`@custom-variant dark (&:where(.dark, .dark *));` in
[`styles.css`](../../src/frontend/buddy/src/styles.css)) instead of relying solely on
`prefers-color-scheme` media queries. `styles.css` also sets the `color-scheme` CSS property so
native widgets (checkboxes, date/color pickers, scrollbars) follow the active theme.
[`index.html`](../../src/frontend/buddy/src/index.html) re-applies the same class from the stored
preference in an inline script before Angular boots, to avoid a flash of the wrong theme on load.

## Current status

The frontend is an actively developed product shell with working domain workflows. See the
[Features](../../README.md#features) list in the root README for the up-to-date checklist of
implemented and planned features.

Children have a read-only calendar agenda; event and task creation remains a guardian workflow.
The child home keeps routines in separate, scannable sections rather than merging them into a
full calendar timeline. Theme selection (light/dark/system) is persisted per browser.

## Design analysis

- [Guardian onboarding](analysis/guardian-onboarding.md) -- implemented first-login
  setup guide for guardians with no groups or children, with resumable progress
- [Visual specification](analysis/visual-specification.md) — proposed data-type-to-component
  map and guardian/child visual language, applied to the Sleep Diary as a worked example
- [Responsive layout](analysis/responsive-layout.md) — implemented phone, tablet and wide-desktop
  layouts: no page wider than a phone (checked by the screenshot run on all three sizes), shared
  page and card components, touch targets, day lists for the week tables, a phone tab bar, and a
  gap-free dashboard
- [Installing Buddy on a kid's iPad](analysis/ipad-installation.md) — PWA vs. native install
  options, push notification support, and pricing
- [A single-day dashboard for the child home screen](analysis/child-day-dashboard.md) — layout
  options for today's meal plan, medicine, and tasks on the child home screen
- [Historical meal plans and children's ratings](analysis/mealplan-history-and-ratings.md) — what's
  implemented for browsing past meal-plan weeks and surfacing children's ratings
- [Creating events and seeing them across every accessible calendar](analysis/calendar-agenda-and-event-creation.md) —
  implemented guardian agenda/event workflow and child read-only access
- [Task library](analysis/task-library.md) — implemented per-child task template
  management and template-based task scheduling
- [Child calendar agenda](analysis/child-calendar-agenda-plan.md) — implemented read-only
  seven-day child calendar
- [Guardian calendar views](analysis/guardian-full-calendar-views.md) — implemented day,
  work-week, rolling-week, and month navigation
- [Child progress and rewards](analysis/child-progress-and-rewards.md) — implemented progress
  summary, guardian-configurable goal posts, and reward sketch
- [Pickup planning and daily views](analysis/pickup-planning-and-daily-views.md) — implemented
  guardian planner, guardian dashboard summary, and child read-only view
- [Week plan printing](analysis/week-plan-printing.md) — implemented A3/A4 landscape print
  sheet from saved templates, quick print flow, and template editor
- [Sleep diary](../backend/analysis/sleep-diary.md) — implemented guardian page and the public
  share view; the [visual specification](analysis/visual-specification.md)'s Sleep Diary example
  drove its controls (toggle, repeatable rows, the new `shared/time-range`)
- [Expired sessions during in-app navigation](analysis/expired-session-handling.md) — implemented
  redirect to `/login` with a "session expired" notice when the Keycloak session ends mid-use, and
  return to the interrupted page after signing in
- [In-app help for guardians](analysis/in-app-help.md) — implemented per-page inline help panel
  toggled from the shell header, a `/guardian/help` index, typed en/da help dictionaries and a
  coverage spec that requires help for every guardian page
- [House rules](../backend/analysis/house-rules.md) — implemented guardian page, child page with
  a "rules to read" card on the child home, A4 printouts, and the shared markdown renderer and
  editor (see "Rendering markdown" below)

## Rendering markdown

`shared/markdown-view` (`<app-markdown-view [markdown]="rule.body" />`) is the one place the app
renders user-written markdown (house rules' bodies). `shared/markdown-editor` is the matching
input: a textarea with a Write/Preview switch (the preview is `markdown-view`), a toolbar that
inserts syntax (pure edits in `markdown-edits.ts`) and a character counter. `shared/markdown-view/markdown.ts` runs
`marked`'s lexer (pinned in `package.json`; its HTML renderer is never called) and maps the token
tree onto an allow-listed node model: headings (`#`/`##` drop to level 3), paragraphs, line breaks,
bold, italic, strikethrough, inline and block code, nested and numbered lists, read-only task lists,
GFM tables with alignment, block quotes, rules, and `http:`/`https:`/`mailto:` links (new tab,
`rel="noopener noreferrer"`). Anything else (raw HTML, images, other link schemes) is shown as its
source text. The template uses only interpolation and attribute bindings, never `innerHTML`, so
there is nothing to sanitize; a spec checks that Angular's sanitizer is never asked for HTML.

The app's Content-Security-Policy is enforced (`Caddyfile`). Keep the app free of inline scripts
and inline event handlers: the pre-boot theme script lives in `public/theme-init.js`, and
`inlineCritical` is off in `angular.json` because critical-CSS inlining adds an `onload` handler.

## Local development

Use the [development container guide](../../.devcontainer/README.md) for local
PostgreSQL, Keycloak, API, and certificate setup. From the frontend app folder:

```bash
cd src/frontend/buddy
npm install
npm start
```

The app runs by default on the Angular dev server at http://localhost:4300/.

## Testing

Unit tests run through Angular's `@angular/build:unit-test` builder (Vitest under the hood):

```bash
cd src/frontend/buddy
npm test
```

### Mutation testing

StrykerJS is wired up (`stryker.conf.json`) to check that the unit test suite actually catches
regressions, not just that it runs. Angular 22's new test builder wraps Vitest internally rather
than exposing a standalone `vitest.config.ts`, so Stryker drives it through its built-in command
test runner (`npm test -- --watch=false`) instead of the `@stryker-mutator/vitest-runner` plugin —
treating `ng test` as a black-box pass/fail command avoids having to reimplement the builder's
internal Vitest wiring (jsdom, TestBed initialisation, Angular template compilation) in a
separate config file.
The TypeScript checker (`checkers: ["typescript"]`) still runs ahead of the test command to skip
mutants that don't compile, without needing the Vitest API integration.
The command runs with `NG_BUILD_TYPE_CHECK=0` so the Angular build skips type checking: Stryker's
mutant switches widen inferred types in mutated files, which would otherwise break `strictTemplates`
checking of their templates and fail the initial test run.

`concurrency` is capped at `4` in the checked-in config — each mutant reruns a full `ng test`
build, and higher concurrency was observed to cause build contention and false timeouts in this
devcontainer. Raise it if your machine handles more parallel builds comfortably.

```bash
cd src/frontend/buddy
npm run test:mutation
```

The HTML report is written to `reports/mutation/index.html` (git-ignored).
Coverage is still intentionally small: the current suite contains the app
shell spec and a profile-management spec, so many mutants can report as
uncovered rather than killed. That is a test-coverage signal, not a tooling
failure.
