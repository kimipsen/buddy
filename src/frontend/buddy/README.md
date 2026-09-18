# Buddy frontend

The Buddy frontend is an Angular 22 single-page application for guardian and
child workflows. It uses standalone components, signals, Tailwind CSS, typed
English/Danish translation dictionaries, and runtime configuration for the API
and Keycloak.

For feature architecture and route descriptions, see the
[frontend documentation](../../../docs/frontend/README.md).

## Prerequisites

The supported environment is the repository's VS Code development container;
see the [development container guide](../../../.devcontainer/README.md). It
provides the Node and Angular toolchain and starts the API's PostgreSQL,
Keycloak, and Mailpit services.

For development outside the container, use Node 22 and npm 11. The exact npm
version is declared by `packageManager` in `package.json`. The API and a
configured `buddy` Keycloak realm must be running before authenticated flows
can work.

## Install and run

From this directory:

```bash
npm install
npm start
```

The Angular development server listens on `http://localhost:4300` and reloads
when source files change.

Runtime endpoints are read from
[`public/config/runtime-config.json`](public/config/runtime-config.json). The
checked-in development values use:

- Keycloak at `http://localhost:9080`, realm `buddy`, client
  `buddy-frontend`;
- Buddy API at `https://localhost:7076`.

If local ports or authorities change, update the runtime config rather than
hardcoding endpoints in services. Values shipped to the browser are public
configuration and must never contain secrets.

## Build

Create the production build with:

```bash
npm run build
```

`angular.json` uses the production configuration by default. Browser artifacts
are written under `dist/buddy/browser/` and are served by Caddy in the
production image.

For a continuous development build without the dev server:

```bash
npm run watch
```

## Tests

Run the Vitest-backed Angular unit suite once:

```bash
npm test -- --watch=false
```

Run mutation testing with StrykerJS:

```bash
npm run test:mutation
```

The mutation report is written to `reports/mutation/index.html`.

Run the Playwright end-to-end suite (needs Postgres, Keycloak and Mailpit
running -- see the [development container guide](../../../.devcontainer/README.md)):

```bash
npx playwright install --with-deps chromium   # once per machine
npm run test:e2e
```

`playwright.config.ts` starts the Angular dev server and the backend API for
you; inside the devcontainer both come up talking to the already-running
Postgres/Keycloak/Mailpit services. Tests live under `e2e/`; `e2e/support/`
has the Keycloak direct-grant login fixture and Mailpit client that specs use
instead of driving the hosted login form or Testcontainers themselves -- see
`e2e/login.spec.ts` for the one spec that does drive the real login form.

See the repository [testing guide](../../../docs/testing.md) for backend and CI
commands.

## Localization

Static UI text is stored by feature under
`src/app/core/i18n/translations/{en,da}/`. Add the same key to both languages;
the Danish dictionary is type-checked against the English dictionary so drift
fails the TypeScript build. Components resolve keys through `TranslatePipe`
rather than embedding user-facing strings.

## Project layout

- `src/app/core/` — authentication, runtime configuration, domain HTTP services,
  dates, time display, and localization.
- `src/app/features/guardian/` — guardian dashboard, mealplan, medicine,
  pickup, calendar, and administration routes.
- `src/app/features/child/` — child day view and mealplan/rating route.
- `src/app/shared/` — reusable date and time controls.
- `public/config/` — browser-visible runtime configuration.

Generate new Angular artifacts only when they fit this feature-first layout;
generic CLI scaffolding paths often need moving and cleanup afterward.
