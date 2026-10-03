import { defineConfig, devices } from '@playwright/test';

// Captures the documentation screenshots in docs/screenshots (`task docs:screenshots`). Separate
// from playwright.config.ts so the e2e suite never runs it. It seeds its own demo family
// (screenshots/demo-family.ts) and starts the API and dev server itself, reusing them when they're
// already running.
export default defineConfig({
  testDir: './screenshots',
  globalSetup: require.resolve('./screenshots/global-setup'),
  globalTeardown: require.resolve('./screenshots/global-teardown'),
  outputDir: './test-results/screenshots',
  // One worker, in order: the pages share one demo family and the run is short anyway.
  workers: 1,
  reporter: [['list']],
  timeout: 60_000,
  expect: { timeout: 15_000 },
  use: {
    ...devices['Desktop Chrome'],
    baseURL: 'http://localhost:4300',
    viewport: { width: 1280, height: 800 },
    colorScheme: 'light',
    locale: 'en-GB',
    timezoneId: 'Europe/Copenhagen',
    ignoreHTTPSErrors: true,
  },
  webServer: [
    {
      command: 'npm start',
      url: 'http://localhost:4300',
      reuseExistingServer: true,
      timeout: 180_000,
    },
    {
      command: 'dotnet run --project ../../backend/buddy',
      url: 'https://localhost:7076/openapi/v1.json',
      ignoreHTTPSErrors: true,
      reuseExistingServer: true,
      timeout: 180_000,
    },
  ],
});
