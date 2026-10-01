---
name: e2e-test
description: Write, run and debug the Buddy Playwright end-to-end specs in src/frontend/buddy/e2e (real browser against the real .NET API, Keycloak, Postgres and Mailpit, nothing mocked). Covers running the whole suite, one spec or one test, reading traces/screenshots/the HTML report, the loginAs direct-grant fixture vs the real login form, test-data isolation with unique names, role-based locator conventions, triaging flaky vs real failures, and a checklist for adding a new spec. Use for "write an e2e test for X", "add a Playwright spec", "run the e2e tests", "run login.spec", "the e2e suite is failing", "this Playwright test is flaky", "debug the e2e failure", "show the Playwright report".
---

# Buddy e2e tests (Playwright)

Everything runs from `src/frontend/buddy` unless it says otherwise. `$S` = your scratchpad directory.

## 1. Prerequisites

- Postgres, Keycloak and Mailpit must be up. Health-check them as in the `run-buddy` skill, step 1 (`pg_isready -h db`, `http://keycloak:8080/realms/buddy`, `http://mailpit:8025/api/v1/info`).
- Chromium, once per machine: `npx playwright install --with-deps chromium` (skip it if `~/.cache/ms-playwright/chromium-*` exists).
- You don't start the servers yourself. `playwright.config.ts` `webServer` starts `npm start` (waits for `http://localhost:4300`) and `dotnet run --project ../../backend/buddy` (waits for `https://localhost:7076/openapi/v1.json`, 120s timeout). Locally `reuseExistingServer` is on, so an API/frontend already listening is **reused as is**. After backend changes, stop the running API first (`fuser -k -TERM 5193/tcp 7076/tcp`) or the specs run against stale code.
- `globalSetup` temporarily rewrites `public/config/runtime-config.json` (`keycloak.authority` → `http://keycloak:8080`) and `globalTeardown` restores it. If a run is killed, the file stays modified next to a `runtime-config.json.e2e-backup`. Restore it (`mv public/config/runtime-config.json.e2e-backup public/config/runtime-config.json`) and never commit the swapped value.

## 2. Run

The config uses the `html` reporter, which by default **opens a blocking report server when a test fails**. In agent runs, always set `PLAYWRIGHT_HTML_OPEN=never` and add a console reporter:

```bash
export PLAYWRIGHT_HTML_OPEN=never
npx playwright test --reporter=line,html > $S/e2e.log 2>&1                                   # whole suite
npx playwright test e2e/task-complete.spec.ts --reporter=line,html                            # one spec
npx playwright test e2e/child-role-route-guard.spec.ts -g "redirected to /guardian" --reporter=line   # one test (title regex)
npx playwright test --last-failed --reporter=line                                             # only last run's failures
```

- Run the whole suite with `run_in_background: true` and wait for the notification. A single spec takes ~30s including server startup.
- The repo-root equivalents are `task test:e2e` / `task test:e2e:frontend` (= `npm run test:e2e`, no reporter override, so set `PLAYWRIGHT_HTML_OPEN=never` there too).
- `fullyParallel: true`, `retries: 0` locally (2 in CI), `expect` timeout 10s, `trace: 'on-first-retry'`. With zero local retries that means **no trace locally unless you ask for one** (`--trace on`, see §4).
- Locally the Node side talks to `keycloak:8080` / `mailpit:8025` by hostname. With `CI` set it uses `localhost:9080/9025/2025` and `ciBackendEnv` (that's what `.github/workflows/e2e-tests.yml` does). Don't set `CI=true` inside the devcontainer.

## 3. Results

- `test-results/` holds per-test failure artifacts (`test-failed-1.png`, `error-context.md`, `trace.zip` when traced). It's wiped at the start of every run, so copy what you need into `$S` first. `test-results/.last-run.json` lists the last failures.
- `playwright-report/` is the HTML report. `task test:e2e:frontend:report` (= `npx playwright show-report`) serves it on `http://localhost:9323` and blocks, so run it in the background and only when the user wants to browse it. Stop it afterwards.
- Trace: `npx playwright show-trace test-results/<dir>/trace.zip` is a GUI. In the agent, read `error-context.md` (an ARIA snapshot of the page at failure) and the PNG (Read tool) instead. Both are usually enough to see which locator didn't match and what was on screen.
- Both directories are gitignored.

## 4. Triage a failure: flaky or real?

Never "fix" a failure with `page.waitForTimeout`, bigger blanket timeouts, `test.slow()`, retries or `force: true`. Find the cause.

1. **Read the error first**: the assertion, the locator, the `error-context.md` snapshot, the screenshot. Typical causes: a renamed label/button (real), a strict-mode violation because the locator matches 2+ elements (real, often accumulated data, see 4), the wrong child selected (a seeded guardian's page defaulted to another test's child; use `newGuardian`), a 401/500 underneath.
2. **Re-run just that test, traced and repeated**:
   ```bash
   npx playwright test e2e/foo.spec.ts -g "title" --repeat-each=5 --trace on --reporter=line
   ```
   Fails every time = real (a regression in the app, or the spec is wrong about the UI). Fails intermittently = a race. Fix it by waiting on the actual signal: a web-first `expect(...)` on the state you need, `page.waitForResponse(...)` around the click that triggers the request (see `ai-provider-settings.spec.ts`), or the existing helpers' patterns. Also try `--workers=1`: passing serially but failing in parallel means a shared-data collision between specs. Extract the trace's network log (`unzip trace.zip`, then `jq` over `*.network` for `.snapshot.request.url`/`.response.status`) and compare ids/timestamps against the other specs' traces and the event store: e.g. a 404 on a child id another spec created and its cleanup unlinked a moment earlier.
3. **Check the backend**. If Playwright started the API, its output is prefixed `[WebServer]` in your log. Look for `fail:` / exceptions. Inspect what was written: `task db:marten:events SCHEMA=<feature>` (run-buddy step 5). Check mail with Mailpit `http://mailpit:8025/api/v1/search?query=to:...`.
4. **Environment, not code**. These show up in the API log or as many 500s:
   - `53300: sorry, too many clients already`: Postgres ran out of connections. The API uses one shared pool capped at 50 (`Common/Postgres/PostgresDataSource.cs`, `application_name` `buddy`), so this points at several API hosts at once, an explicit large `Maximum Pool Size`, or something else holding connections. Measure with `select count(*), application_name, state from pg_stat_activity group by 2,3`. Report it, don't patch around it in the spec.
   - `401 invalid_token "The signature key was not found"`: the token came from a different Keycloak (a nested `localhost:9080` stack, see run-buddy step 0).
   - Stale reused API after a backend change (§1).
5. Report the classification (real regression / spec bug / race / environment) with the evidence. Fix the app only if it's actually broken. Fix the spec if the spec is wrong.

## 5. Conventions in the existing specs

**Auth.** Import from the fixture, not `@playwright/test`:
```ts
import { SEEDED_USERS, expect, test } from './support/auth-fixture';
test('...', async ({ page, loginAs }) => { await loginAs(SEEDED_USERS.alice); ... });
```
`loginAs` mints a real token via Keycloak direct grant (`support/keycloak-client.ts`), seeds `sessionStorage['buddy_keycloak_tokens']` and navigates to `redirectPath` (`/dashboard`, which routes a guardian to `/guardian`). Calling it again switches identity mid-test (`guardian-invite-accept.spec.ts`). Only `login.spec.ts` drives the real Keycloak form (`Sign in with Keycloak` → `Username or email` / `Password` → `Sign In`) to cover the PKCE redirect. Don't add form logins elsewhere unless the redirect itself is what's under test.

**Users.** alice/bob/carol (`.devcontainer/keycloak/buddy-realm.json`, passwords in `SEEDED_USERS`) are all guardians, shared by every spec and every run against the same persistent DB. Never delete them or permanently change their email/password. If a spec changes profile data, restore it at the end (`profile-update.spec.ts`).

**Disposable guardians (`newGuardian` fixture).** `const g = await newGuardian(); await loginAs(g);` mints a fresh Keycloak guardian (verified email `<username>@buddy.test`, given name `DISPOSABLE_GUARDIAN_GIVEN_NAME` = `E2e`, no children, no family) and `cleanUpCreatedData` removes it after the test (`DELETE /users/me`, then the Keycloak user). Use it, not a seeded user, for **any spec that calls `createChild()`** or depends on family/account-wide state: per-child pages and the mealplan family scope default to the guardian's *first* child (`ListMyChildren`, no stable order), so as a seeded guardian a test lands on another parallel test's child, whose cleanup then revokes the link mid-flow (404s on `/task-templates/children/{id}`, `PUT /mealplans/children/{id}/plan`). Also for anything that changes the account (email change: `email-verification.spec.ts`) or links guardians to each other (`guardian-invite-accept.spec.ts` uses two). With one child there's no `#selectedChildId` select, so don't call `selectChildIfPresent` (it would wait 5s for nothing); wait for the page's data with a web-first assertion instead. Seeded users remain fine for specs that only create groups/calendars scoped by unique name, or that read the seeded account. `delete-account.spec.ts` uses `createDisposableGuardian()` / `deleteKeycloakUser()` directly (with fail-safe asserts that it isn't a seeded user). Child accounts can't be logged in through the UI here (temporary-password required action; see the comment in `child-role-route-guard.spec.ts`), so child-side flows are out of reach.

**Data isolation.** Nothing is pre-seeded and every spec shares the same persistent DB and seeded guardians across runs and parallel workers:
- Create prerequisites through the real UI with `support/guardian-data.ts`: `createChild(page)`, `createGroup(page)`, `createCalendar(page, groupName)`. Name everything with `uniqueName('E2E Foo')`.
- **Created data is cleaned up automatically.** `createChild()`, `createGroup()` and `createCalendar()` register what they make, and the `cleanUpCreatedData` auto fixture in `support/auth-fixture.ts` removes it after every test (`support/created-data-cleanup.ts`), checking as each seeded guardian and each disposable guardian the test made. Groups and calendars are deleted (`DELETE /groups/{id}` cascades to the group's calendars). Children can't be deleted (no backend use case), so every seeded guardian's link is revoked (`DELETE /users/me/children/{id}/guardian-link`) and the child's `e2echild*` Keycloak user is deleted. It's best-effort: failures are `console.warn`ed, never thrown. It only covers specs that import `test` from `./support/auth-fixture` and data made through those three helpers. Medicines, meals, task templates etc. made inline in a spec are still left behind. If a new helper creates shared-user data, track it the same way.
- Leftovers from before the cleanup existed: `node e2e/scripts/cleanup-leftover-e2e-data.mjs` (dry run) / `--apply`, with the API running. It only matches the exact names the helpers generate.
- Still assume a seeded user already has other children (from parallel workers or a crashed run), groups and items from earlier runs. Scope every locator to *your* unique name (`page.locator('li', { hasText: name })`). Assertions on "first item" or exact counts are wrong. Never create a child as a seeded user (use `newGuardian`, above); `selectChildIfPresent(page, givenName)` only narrows the race, it can't stop another test's cleanup from revoking the child a page defaulted to.
- Family-wide state (mealplan library, AI provider keys, the family scope) is per guardian's children; get a clean family from `newGuardian` instead of resetting a seeded one.
- Emails: `waitForMessageTextContaining(address, uniqueFragment)` + `extractInviteToken(text, 'invite' | 'guardian-invite')` from `support/mailpit-client.ts`. Never use "latest message".
- Stub only what would leave the machine: `page.route()` for endpoints that call real AI providers (`ai-provider-settings.spec.ts`, `mealplan-ai-assistant.spec.ts`). Everything else hits the real backend.

**Locators.** Role- and label-based, scoped to the component's tag when labels repeat on the page:
- `page.locator('app-manage-children').getByLabel('Given name')`, `section.getByRole('button', { name: 'Add child' })`, `getByRole('heading', { name: 'Guardian dashboard' })`. Use `exact: true` when names overlap (`'Delete'` vs `'Delete my account'`).
- Toggles are `app-toggle`, a `<button role="switch" aria-checked>` that replaced every native checkbox (commit b1eb72a). Use `getByRole('switch', { name })` and `toHaveAttribute('aria-checked', 'true')`, never `getByRole('checkbox')` / `toBeChecked()` / `input[type=checkbox]`.
- Falling back to `#id` or tag locators is only OK where no accessible name exists (`#selectedChildId`, `#itemCalendar`, `tbody tr`), with a comment explaining why.
- Use web-first assertions (`await expect(locator).toBeVisible()`), not `isVisible()` checks, except for genuinely conditional setup branches. Prove persistence with `page.reload()` + re-assert.

## 6. New spec checklist

1. File `e2e/<feature>-<behaviour>.spec.ts`, kebab-case, one user-visible flow. Most files hold a single `test(...)` with a sentence title ("guardian marks a dose taken ..., and it persists"). No `describe` / `beforeEach` boilerplate.
2. Top-of-file comment: what real path this exercises and why (which backend/UI constraints forced the setup), like the existing specs. Explain each non-obvious wait or scoping choice inline.
3. Imports: `SEEDED_USERS, expect, test` from `./support/auth-fixture`, plus helpers from `./support/*`. Add a new shared helper to `support/guardian-data.ts` (UI-driven, unique names, comment) only when a second spec needs it.
4. Body: `loginAs(SEEDED_USERS.x)` → create prerequisites via helpers → `page.goto(route)` → assert the page heading is visible (proves the page loaded) → act → assert the outcome → `page.reload()` → assert it persisted.
5. Pick the user deliberately: `newGuardian` if the spec creates a child or touches family/account state, otherwise a seeded user (check which ones the existing specs lean on and whether you mutate shared state).
6. Stub only outbound third-party calls. Use no `waitForTimeout`. `waitForLoadState('networkidle')` only for the documented child-select race.
7. Read the actual component template (`src/app/features/**/*.html`) and i18n strings for labels and button names. Don't guess.
8. Run it: `npx playwright test e2e/<new>.spec.ts --repeat-each=3 --reporter=line`, then the full suite once with `--reporter=line,html`. All must pass, and pre-existing failures should be reported, not hidden.
9. Check that `git status` shows only your spec/helper changes (in particular no `public/config/runtime-config.json` change), and that no servers you started are left running. Don't commit unless asked.
