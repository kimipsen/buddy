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
- runtime config loader
- browser app initialization

The top-level route setup is in [src/frontend/buddy/src/app/app.routes.ts](../../src/frontend/buddy/src/app/app.routes.ts). It divides the app into:

- `/login` for unauthenticated users
- `/invite/:token` for viewing and accepting group invitations, including the logged-out preview path
- `/verify-email/:token` for completing email verification, including the logged-out verification path
- `/guardian` for guardian dashboard screens
- `/child` for child-focused screens
- root redirect logic based on resolved role

## Authentication flow

Authentication is centered on the services in [src/frontend/buddy/src/app/core](../../src/frontend/buddy/src/app/core):

- `AuthService` handles the Keycloak authorization code exchange, token refresh, and logout
- `RuntimeConfigService` loads config from `/config/runtime-config.json`
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
- create child accounts and capture the one-time temporary password
- show today's events, tasks, and medicine doses
- manage meals and assign them to shared meal-plan slots
- browse a weekly agenda and create events/tasks across every calendar they can contribute to,
  personal or group-owned
- manage calendars, groups, children, and the current profile from the admin area
- sign out of the current session

The route definition is in [src/frontend/buddy/src/app/features/guardian/guardian.routes.ts](../../src/frontend/buddy/src/app/features/guardian/guardian.routes.ts).

The guardian routes currently include:

- `/guardian` — dashboard and today's operational summary
- `/guardian/mealplan` — meal library and meal-plan assignment
- `/guardian/medicine` — medicine schedule management
- `/guardian/calendar` — weekly agenda across every accessible calendar, plus event/task creation
- `/guardian/admin` — profile, child, calendar, group, event-history, and account administration

### Child feature

The child-facing area is implemented in [src/frontend/buddy/src/app/features/child](../../src/frontend/buddy/src/app/features/child).

Current responsibilities:

- display the child home screen
- show the child's day view, including events, tasks, and doses when available
- show guardian links when available
- provide the child-specific entry route

The route definition is in [src/frontend/buddy/src/app/features/child/child.routes.ts](../../src/frontend/buddy/src/app/features/child/child.routes.ts).

### Login feature

The login screen is in [src/frontend/buddy/src/app/features/login](../../src/frontend/buddy/src/app/features/login). It starts the Keycloak redirect flow and keeps the sign-in UX cleanly separated from the rest of the app.

### Invitation and email verification features

The invitation flow is in [src/frontend/buddy/src/app/features/invite](../../src/frontend/buddy/src/app/features/invite). It supports a public invitation preview and returns the user to the invitation after login so the invitation can be accepted in an authenticated session.

The email verification flow is in [src/frontend/buddy/src/app/features/verify-email](../../src/frontend/buddy/src/app/features/verify-email). It uses the same public-route pattern: a user can open a verification link while logged out, sign in if needed, and return to the pending token.

## Shared services

The shared domain services live under [src/frontend/buddy/src/app/core](../../src/frontend/buddy/src/app/core):

- `AccountService` resolves whether the user is a guardian or child
- `GuardiansService` calls the backend guardian endpoints
- `MealplansService` calls meal-library, meal-plan, rating, and group-sharing endpoints
- `UsersService` loads the current profile, language, and email-verification state
- `AuthInterceptor` attaches the access token to outgoing requests
- `UserEventsService` is available for future user event stream integration
- `TranslationService` resolves the UI's current language (see Localization below)

## Localization (i18n)

The app supports English and Danish. Static UI text is a translation key resolved through
[`TranslationService`](../../src/frontend/buddy/src/app/core/i18n/translation.service.ts) and the
`translate` pipe, e.g. `{{ 'profile.title' | translate }}`, rather than a hardcoded string.
Dictionaries live under `src/frontend/buddy/src/app/core/i18n/translations/{en,da}/`, one file per
feature area, merged in that language's `index.ts`; `translations/index.ts` types `da` against
`typeof en` so a missing or extra key in either language fails the build instead of silently
falling back to the raw key at runtime.

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

## Current status

The frontend is an actively developed product shell with working domain workflows. The core pieces already in place are:

- Keycloak authentication and token lifecycle
- route guards and role-based redirecting
- guardian-child provisioning flow
- child/guardian role separation
- email verification and invitation return flows
- English and Danish localization
- guardian meal planning, meal ratings, and group-shared meal-plan access
- medicine schedule management and today's dose views
- a guardian-facing weekly calendar agenda and event/task creation across personal and
  group-owned calendars
- profile, calendar, group, child, and account administration

The main remaining product work is to deepen the child-facing experience — including whether
children get their own calendar agenda/creation UI, an open question noted in
[Creating events and seeing them across every accessible calendar](analysis/calendar-agenda-and-event-creation.md)
— and connect the existing routine data into a richer personalized daily view.

## Design analysis

- [Installing Buddy on a kid's iPad](analysis/ipad-installation.md) — PWA vs. native install
  options, push notification support, and pricing
- [A single-day dashboard for the child home screen](analysis/child-day-dashboard.md) — layout
  options for today's meal plan, medicine, and tasks on the child home screen
- [Historical meal plans and children's ratings](analysis/mealplan-history-and-ratings.md) — what's
  needed to browse past meal-plan weeks and surface children's ratings, given the backend already
  supports both
- [Creating events and seeing them across every accessible calendar](analysis/calendar-agenda-and-event-creation.md) —
  what's needed to create events and browse an agenda merged across personal and group calendars,
  given the backend already supports both

## Local development

From the frontend app folder:

```bash
cd src/frontend/buddy
npm install
npm start
```

The app runs by default on the Angular dev server at http://localhost:4200/.

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
internal Vitest wiring (jsdom, zone.js, Angular template compilation) in a separate config file.
The TypeScript checker (`checkers: ["typescript"]`) still runs ahead of the test command to skip
mutants that don't compile, without needing the Vitest API integration.

`concurrency` is capped at `4` in the checked-in config — each mutant reruns a full `ng test`
build, and higher concurrency was observed to cause build contention and false timeouts in this
devcontainer. Raise it if your machine handles more parallel builds comfortably.

```bash
cd src/frontend/buddy
npm run test:mutation
```

The html report is written to `reports/mutation/index.html` (git-ignored). As of this setup only
`app.spec.ts` exists, so most mutants will report as uncovered rather than killed — that's an
honest signal of how much of the app still needs unit tests, not a tooling problem.
