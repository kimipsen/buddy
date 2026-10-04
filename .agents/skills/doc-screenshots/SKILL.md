---
name: doc-screenshots
description: Regenerate, extend or debug the documentation screenshots of every Buddy page in docs/screenshots (task docs:screenshots). Seeds a fresh demo family (guardian demo.sara, children demo.emil/demo.ida) through the real API, captures each page listed in src/frontend/buddy/screenshots/pages.ts with Playwright, and regenerates docs/screenshots/README.md. Use for "update the screenshots", "regenerate the docs screenshots", "add a screenshot of the new page", "the screenshot of X is empty/wrong", "show what the app looks like in the docs". A one-off screenshot to check a change is run-buddy; Playwright test specs are e2e-test.
---

# Documentation screenshots

Paths are relative to `src/frontend/buddy` unless they say otherwise. `$S` = your scratchpad directory.

## Files

| File                                  | Role                                                                                                                                                                                                                                                                                                                                                       |
| ------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `screenshots/pages.ts`                | The page list: name (PNG file name), title, description, who is signed in, `route` (the full route pattern), optional `path` (builds the URL from seeded ids when the route has parameters), optional `waitFor` text. Also `UNCAPTURED_ROUTES`, routes that can't be captured, each with a reason. Has no imports, because the unit spec below imports it. |
| `src/app/screenshot-coverage.spec.ts` | Vitest spec (part of `task test:frontend`). Walks the real route config, including lazy children, and fails when a page route is neither in `SCREENSHOT_PAGES` nor in `UNCAPTURED_ROUTES`, or when either one lists a route that no longer exists.                                                                                                         |
| `screenshots/demo-family.ts`          | Resets and seeds the demo family through the API: children, group, calendars, events, task templates and scheduled tasks (with completed subtasks for stars), goal posts, meals and a three-week plan, medicine, work locations, pickups, a print template, and group + guardian invites (tokens read from Mailpit).                                       |
| `screenshots/capture.spec.ts`         | One test per page: signs in by direct grant, waits, saves `docs/screenshots/<name>.png`. Fails on any 5xx while loading.                                                                                                                                                                                                                                   |
| `screenshots/global-teardown.ts`      | Writes `docs/screenshots/README.md` from `pages.ts` (only pages that have a PNG). Never edit that README by hand.                                                                                                                                                                                                                                          |
| `playwright.screenshots.config.ts`    | Separate config (the e2e suite never runs these). One worker, 1280×800, light theme, `en-GB`, `Europe/Copenhagen`. Starts or reuses the API and dev server.                                                                                                                                                                                                |

## Regenerate everything

From the repo root:

```bash
task docs:screenshots
```

Same as `cd src/frontend/buddy && npm run screenshots`. Prerequisites: Postgres, Keycloak and Mailpit healthy (see the `run-buddy` skill, step 1). Chromium must be installed (`npx playwright install --with-deps chromium` once). It takes about 30 seconds once the servers are up.

To try changes without touching the committed images, write somewhere else:

```bash
SCREENSHOTS_DIR=$S/shots npx playwright test -c playwright.screenshots.config.ts
SCREENSHOTS_DIR=$S/shots npx playwright test -c playwright.screenshots.config.ts -g guardian-medicine   # one page (the seed still runs)
```

Then **look at every PNG you changed** with the Read tool before committing. A passing run doesn't prove that a page shows data rather than an empty state.

## Add a page

Every new route needs this. The coverage spec fails `task test` until it's done. A visible change to an existing page (a new dashboard widget, a new section) doesn't trip the spec, but it still needs steps 3–4 so the image stays current.

1. Add an entry to `SCREENSHOT_PAGES` in `screenshots/pages.ts`, in the order it should appear in the README. Set `route` to the full pattern exactly as the route config spells it out (`/guardian/print/templates/:templateId`). Use `as: 'guardian' | 'child' | 'anonymous'`. When the route has parameters, add a `path` function that builds the URL from seeded ids (see the print template entries). Only if the page truly can't be captured with demo data, add it to `UNCAPTURED_ROUTES` with the reason instead.
2. Set `waitFor` to text that only appears once the page's data has loaded, such as a seeded meal or medicine name. Visible text only: `<option>` text and input values don't count (`getByText` doesn't match them).
3. If the page would otherwise show an empty state, seed what it needs in `seedDemoFamily()` (`demo-family.ts`) through the API, as Sara (`api.post/put/patch`). Find request shapes in `/openapi/<feature>.json` on the running API, or in the typed models in `src/app/core/*.service.ts`. Enums are integer ordinals. If the page needs a new id, add it to `DemoFamily` and the return value.
4. Run it into `$S/shots`, look at the PNG, then run `task docs:screenshots` for real. Run `npx ng test --watch=false --include src/app/screenshot-coverage.spec.ts` to confirm coverage.

## Demo data rules

- Dates are relative to the day of the run: the plan covers this week's Monday plus 20 days, so pages that show the next seven days and the print sheet (which opens on next week) both have data. Don't hard-code dates.
- Each run calls `resetDemoFamily()` first, which deletes Sara's groups (and their calendars), unlinks the children, deletes her backend account, and deletes the `demo.*` Keycloak users. The demo accounts are fixed usernames, separate from alice/bob/carol, so the e2e specs are unaffected. Never seed screenshot data onto the seeded e2e users.
- The demo child gets a permanent password through the Keycloak admin API (a newly created child has a temporary one that direct grant can't use). Everything is local-dev-only: the Keycloak master admin is `admin/admin` and the demo password is `demo-screenshots-pw`.
- Optional extras (completed subtasks, a taken dose, invites) log `[demo seed] …` warnings instead of failing. Read those warnings when a page looks emptier than expected.
- After a run the demo family stays in place. To explore it by hand, sign in at http://localhost:4300 as `demo.sara` / `demo-screenshots-pw` (or as `demo.emil`).

## Troubleshooting

- **`waitFor` times out**: open `test-results/screenshots/<test>/error-context.md`. Its ARIA snapshot shows what actually rendered. Usually the text is in an input or option, or the seed didn't create the data.
- **401s / login loops**: the browser must use `http://keycloak:8080`. `capture.spec.ts` rewrites `runtime-config.json` per request (the file on disk is never touched). Check `KEYCLOAK_URL` if you're running outside the devcontainer.
- **Seed fails with 4xx**: the error message includes the method, path and response body. Compare the request against the OpenAPI schema. A validation change in the backend usually means `demo-family.ts` needs the same change.
- **Timing-dependent content**: events earlier today show as past (struck through), and weekend runs have no pickups "today". This is expected. The images reflect the moment they were taken.
