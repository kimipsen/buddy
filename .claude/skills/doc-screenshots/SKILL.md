---
name: doc-screenshots
description: Regenerate, extend or debug the documentation screenshots of every Buddy page in docs/screenshots (task docs:screenshots). Seeds a fresh demo family (guardian demo.sara, children demo.emil/demo.ida) through the real API, captures each page listed in src/frontend/buddy/screenshots/pages.ts with Playwright at desktop size and as an iPhone 15 (docs/screenshots/mobile), and regenerates docs/screenshots/README.md. Use for "update the screenshots", "regenerate the docs screenshots", "add a screenshot of the new page", "the screenshot of X is empty/wrong", "show what the app looks like in the docs", "the mobile screenshot of X is cut off", "add a tablet size". A one-off screenshot to check a change is run-buddy; Playwright test specs are e2e-test.
---

# Documentation screenshots

Paths are relative to `src/frontend/buddy` unless they say otherwise. `$S` = your scratchpad directory.

## Files

| File                                  | Role                                                                                                                                                                                                                                                                                                                                                                                                              |
| ------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `screenshots/pages.ts`                | The page list: name (PNG file name), title, description, who is signed in, `route` (the full route pattern), optional `path` (builds the URL from seeded ids when the route has parameters), optional `waitFor` text. Also `UNCAPTURED_ROUTES`, routes that can't be captured, each with a reason. Has no imports, because the unit spec below imports it.                                                        |
| `src/app/screenshot-coverage.spec.ts` | Vitest spec (part of `task test:frontend`). Walks the real route config, including lazy children, and fails when a page route is neither in `SCREENSHOT_PAGES` nor in `UNCAPTURED_ROUTES`, or when either one lists a route that no longer exists.                                                                                                                                                                |
| `screenshots/demo-family.ts`          | Resets and seeds the demo family through the API: children, group, calendars, events, task templates and scheduled tasks (with completed subtasks for stars), goal posts, meals and a three-week plan, medicine, work locations, pickups, a print template, and group + guardian invites (tokens read from Mailpit).                                                                                              |
| `screenshots/capture.spec.ts`         | One test per page and project: signs in by direct grant, waits, saves `docs/screenshots/<name>.png` (desktop) or `docs/screenshots/mobile/<name>.png` (mobile). Fails on any 5xx while loading.                                                                                                                                                                                                                   |
| `screenshots/global-teardown.ts`      | Writes `docs/screenshots/README.md` from `pages.ts`: per page the desktop image, then the mobile one at 320px wide (only images that exist). Never edit that README by hand.                                                                                                                                                                                                                                      |
| `playwright.screenshots.config.ts`    | Separate config (the e2e suite never runs these). One worker, light theme, `en-GB`, `Europe/Copenhagen`. Two projects: `desktop` (Desktop Chrome, 1280×800) and `mobile` (iPhone 15 profile, 393×659 viewport, touch, emulated in Chromium at 2× scale). Starts the API with `Demo__Now` (see the DEMO_NOW rule) and reuses a running one only with `BUDDY_SCREENSHOTS_REUSE_API=1`; reuses a running dev server. |

## Regenerate everything

From the repo root:

```bash
task docs:screenshots
```

Runs `task docs:screenshots:clock-check` first (see the DEMO_NOW rule), then `cd src/frontend/buddy && npm run screenshots`. Stop any dev API on 5193/7076 first: the task starts its own on the demo clock, and the seed fails fast on an API that isn't. Prerequisites: Postgres, Keycloak and Mailpit healthy (see the `run-buddy` skill, step 1). Chromium must be installed (`npx playwright install --with-deps chromium` once). It takes about 30 seconds once the servers are up.

To try changes without touching the committed images, write somewhere else:

```bash
SCREENSHOTS_DIR=$S/shots npx playwright test -c playwright.screenshots.config.ts
SCREENSHOTS_DIR=$S/shots npx playwright test -c playwright.screenshots.config.ts -g guardian-medicine   # one page, both sizes (the seed still runs)
SCREENSHOTS_DIR=$S/shots npx playwright test -c playwright.screenshots.config.ts --project mobile       # phone only
```

## Desktop and mobile

Every page is captured twice, once per Playwright project. `screenshotsDirFor(project)` in `screenshots/screenshots-dir.ts` maps `desktop` to `docs/screenshots/` (the paths existing docs link to) and any other project to a subfolder named after it, so `mobile` lands in `docs/screenshots/mobile/`. There's nothing per page to configure: a new `pages.ts` entry gets both images.

- The mobile project spreads `devices['iPhone 15']` (viewport, user agent, `isMobile`, `hasTouch`) but forces `defaultBrowserType: 'chromium'`, because only Chromium is installed in the devcontainer, and drops `deviceScaleFactor` to 2 so full-page PNGs stay a reasonable size.
- Full-page phone captures of long pages get tall. That's expected. Set `fullPage: false` only if a page is unreadable either way, and it applies to both sizes.
- Check the mobile PNGs as carefully as the desktop ones: look for horizontal overflow (content cut off on the right), the mobile navigation hiding content, and dialogs or popovers that don't fit. A layout bug you find there belongs in the frontend (`buddy-frontend` skill), not in the screenshot code.
- To add a further size (a tablet, say), add a project to `playwright.screenshots.config.ts` and an image line for it in `global-teardown.ts`.

Then **look at every PNG you changed** with the Read tool before committing. A passing run doesn't prove that a page shows data rather than an empty state.

## Add a page

Every new route needs this. The coverage spec fails `task test` until it's done. A visible change to an existing page (a new dashboard widget, a new section) doesn't trip the spec, but it still needs steps 3–4 so the image stays current.

1. Add an entry to `SCREENSHOT_PAGES` in `screenshots/pages.ts`, in the order it should appear in the README. Set `route` to the full pattern exactly as the route config spells it out (`/guardian/print/templates/:templateId`). Use `as: 'guardian' | 'child' | 'anonymous'`. When the route has parameters, add a `path` function that builds the URL from seeded ids (see the print template entries). Only if the page truly can't be captured with demo data, add it to `UNCAPTURED_ROUTES` with the reason instead.
2. Set `waitFor` to text that only appears once the page's data has loaded, such as a seeded meal or medicine name. Visible text only: `<option>` text and input values don't count (`getByText` doesn't match them).
3. If the page would otherwise show an empty state, seed what it needs in `seedDemoFamily()` (`demo-family.ts`) through the API, as Sara (`api.post/put/patch`). Find request shapes in `/openapi/<feature>.json` on the running API, or in the typed models in `src/app/core/*.service.ts`. Enums go by member name (`"Dinner"`, `"Parent"`); only `kind` discriminators are numbers. The whole contract is `docs/backend/openapi/buddy.json`. If the page needs a new id, add it to `DemoFamily` and the return value.
4. Run it into `$S/shots`, look at the PNG, then run `task docs:screenshots` for real. Run `npx ng test --watch=false --include src/app/screenshot-coverage.spec.ts` to confirm coverage.

## Demo data rules

- **The DEMO_NOW rule.** The screenshots never depend on the day, time or time zone they're taken. `DEMO_NOW` in `demo-family.ts` (a fixed instant, `2026-11-11T10:30:00+01:00`) is the one "now":
  - Every seeded date derives from it: the plan covers the demo week's Monday plus 20 days, so pages that show the next seven days and the print sheet (which opens on next week) both have data. Don't hard-code other dates and don't read the real clock.
  - The browser clock is fixed per page with `page.clock.setFixedTime` at `DEMO_NOW`, or at the page's `clock` time ('HH:mm') on the demo day in `pages.ts`.
  - The server clock is the demo clock too: the config starts the API with `Demo__Now = DEMO_NOW` (a development-only override, `src/backend/buddy/Common/Time/DemoClock.cs`; startup fails if it is set outside Development). It starts at `DEMO_NOW` and advances in real time. Token validation stays on the real clock. That's how guardian-night-duty, which the server computes from "today", is identical on every run, with no exception.
  - Seeded share links have no expiry (`expiresAt: null`), and seeded moments stay at or before `DEMO_NOW`.
  - `task docs:screenshots:clock-check` fails on any `new Date()`, `Date.now(` or `Date()` in `screenshots/`. Exactly one line is allowed, the Keycloak token expiry, marked `clock-check: allow`.
  - To move the demo day, change `DEMO_NOW` only and regenerate. Keep it before the real date: the access token's real-clock expiry is checked against the browser's fixed clock, so a `DEMO_NOW` in the future makes every token look expired.
  - A value the app or a service makes random (the Mealie API token, say) also changes the images. Pin what the page shows in `capture.spec.ts` by rewriting the response, as it does for the Mealie `tokenLast4`.
- Each run calls `resetDemoFamily()` first, which deletes Sara's groups (and their calendars), unlinks the children, deletes her backend account, and deletes the `demo.*` Keycloak users. The demo accounts are fixed usernames, separate from alice/bob/carol, so the e2e specs are unaffected. Never seed screenshot data onto the seeded e2e users.
- The demo child gets a permanent password through the Keycloak admin API (a newly created child has a temporary one that direct grant can't use). Everything is local-dev-only: the Keycloak master admin is `admin/admin` and the demo password is `demo-screenshots-pw`.
- Optional extras (completed subtasks, a taken dose, invites) log `[demo seed] …` warnings instead of failing. Read those warnings when a page looks emptier than expected.
- After a run the demo family stays in place. To explore it by hand, sign in at <http://localhost:4300> as `demo.sara` / `demo-screenshots-pw` (or as `demo.emil`).

## Troubleshooting

- **`waitFor` times out**: open `test-results/screenshots/<test>/error-context.md`. Its ARIA snapshot shows what actually rendered. Usually the text is in an input or option, or the seed didn't create the data.
- **401s / login loops**: the browser must use `http://keycloak:8080`. `capture.spec.ts` rewrites `runtime-config.json` per request (the file on disk is never touched). Check `KEYCLOAK_URL` if you're running outside the devcontainer.
- **Seed fails with 4xx**: the error message includes the method, path and response body. Compare the request against the OpenAPI schema. A validation change in the backend usually means `demo-family.ts` needs the same change.
- **Every page times out on `networkidle`, or the run stalls with no output**: check `uptime`. When parallel .NET builds or test runs push the load average far past the core count, the API stops answering the browser in time. Wait for the load to drop and run again; the screenshot code isn't the cause.
- **A run changes images it shouldn't**: two runs must leave `git status docs/screenshots` clean. Look at the old and new PNG side by side. A date or time means something reads the real clock (run the clock-check, and check the API log for the `Demo clock override active` warning). Anything else is usually a random value from the seed; pin it as described under the DEMO_NOW rule.
- **`The API is not on the demo clock`**: a dev API started without `Demo__Now` was still running. Stop it (`fuser -k -TERM 5193/tcp 7076/tcp`) and run the task again.
