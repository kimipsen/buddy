import { defineConfig, devices } from '@playwright/test';

const isCI = !!process.env['CI'];

// Wherever this config's own process (and everything it spawns) runs -- inside the devcontainer's
// app service by default, or directly on a bare CI runner (.github/workflows/e2e-tests.yml) -- db,
// keycloak and mailpit are sibling containers reachable only by hostname from inside the
// devcontainer, and only via their published localhost ports from a bare runner. See
// e2e/support/global-setup.ts for the matching runtime-config.json swap the browser needs (this
// only covers the two Node-side processes below: the .NET API and mailpit-client.ts).
process.env['MAILPIT_BASE_URL'] ??= isCI ? 'http://localhost:9025' : 'http://mailpit:8025';

const ciBackendEnv = {
  ASPNETCORE_ENVIRONMENT: 'Development',
  ConnectionStrings__Postgres:
    'Host=localhost;Port=5432;Database=postgres;Username=postgres;Password=postgres',
  Authentication__Keycloak__Authority: 'http://localhost:9080/realms/buddy',
  Authentication__Keycloak__ValidIssuer: 'http://localhost:9080/realms/buddy',
  // buddy-admin-cli lives in the buddy realm of the fresh import (.devcontainer/keycloak/buddy-realm.json),
  // and CI has no git-ignored appsettings.Development.json, so the secret comes from that export too.
  Authentication__KeycloakAdmin__TokenEndpoint:
    'http://localhost:9080/realms/buddy/protocol/openid-connect/token',
  Authentication__KeycloakAdmin__AdminBaseUrl: 'http://localhost:9080/admin/realms/buddy',
  Authentication__KeycloakAdmin__ClientSecret: 'buddy-admin-cli-dev-secret',
  Mail__Host: 'localhost',
  Mail__Port: '2025',
};

// Every logged-out request in the suite comes from the same localhost IP, so it all shares the API's
// anonymous rate-limit bucket (60 requests, then 1 per second; RateLimitingOptions.Anonymous). The
// parallel suite loads the app dozens of times (each load fetches GET /features before signing in)
// and ran it dry, so a public page such as the shared sleep diary got a 429 and showed its generic
// error. Production keeps its limits; only the API this config starts gets a larger anonymous
// budget. A dev API that is already running (reuseExistingServer) keeps whatever limits it has.
const e2eBackendEnv = {
  RateLimiting__Anonymous__TokenLimit: '1000',
  RateLimiting__Anonymous__TokensPerPeriod: '100',
};

export default defineConfig({
  testDir: './e2e',
  globalSetup: require.resolve('./e2e/support/global-setup'),
  globalTeardown: require.resolve('./e2e/support/global-teardown'),
  fullyParallel: true,
  forbidOnly: isCI,
  retries: isCI ? 2 : 0,
  reporter: 'html',
  // The default 5s is tight for this suite's assertions (each one typically follows a real
  // network round trip -- create/save/etc. -- not just a local state change), and this devcontainer
  // runs the whole stack (Angular dev server, .NET backend, Postgres, Keycloak, Mailpit, plus
  // whatever IDE tooling is active) alongside the browser under test, so latency varies more than
  // on a dedicated CI runner. 10s gives real assertions room without masking a genuinely broken one
  // for long.
  expect: { timeout: 10_000 },
  use: {
    baseURL: 'http://localhost:4300',
    // The backend's dev HTTPS endpoint uses a self-signed cert that's never trusted in CI, and
    // dotnet dev-certs' "trust" step isn't supported on Linux -- ignoring cert errors here (both
    // for the browser and the webServer readiness checks below) is simpler than working around
    // that everywhere.
    ignoreHTTPSErrors: true,
    trace: 'on-first-retry',
  },
  projects: [
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'] },
    },
  ],
  webServer: [
    {
      command: 'npm start',
      url: 'http://localhost:4300',
      reuseExistingServer: !isCI,
    },
    {
      command: 'dotnet run --project ../../backend/buddy',
      // The API has no root route (GET / is a 404), and Playwright's webServer readiness check
      // only accepts 2xx/3xx/400-403 -- openapi/v1.json is always served in Development and is a
      // plain 200, unlike an authenticated route that would 401.
      url: 'https://localhost:7076/openapi/v1.json',
      ignoreHTTPSErrors: true,
      reuseExistingServer: !isCI,
      // Only the backend needs an explicit override: inside the devcontainer it already targets
      // db/keycloak/mailpit by hostname via the checked-in appsettings.Development.json, so
      // nothing to change there -- only a bare CI runner needs redirecting to localhost. The
      // rate-limit budget above applies everywhere.
      env: { ...(isCI ? ciBackendEnv : {}), ...e2eBackendEnv },
      // The API logs to stdout, which Playwright drops by default; on CI that log is the only
      // record of why a request failed (e.g. a 500 from GET /users/me in global setup).
      stdout: isCI ? 'pipe' : 'ignore',
      timeout: 120_000,
    },
  ],
});
