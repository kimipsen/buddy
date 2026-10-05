import { defineConfig, devices } from '@playwright/test';

// Captures the documentation screenshots in docs/screenshots (`task docs:screenshots`). Separate
// from playwright.config.ts so the e2e suite never runs it. It seeds its own demo family
// (screenshots/demo-family.ts) and starts the API and dev server itself, reusing them when they're
// already running.
//
// Two projects capture every page twice: `desktop` into docs/screenshots/ and `mobile` (an
// iPhone 15 profile) into docs/screenshots/mobile/. Both share the one seeded demo family.
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
    baseURL: 'http://localhost:4300',
    colorScheme: 'light',
    locale: 'en-GB',
    timezoneId: 'Europe/Copenhagen',
    ignoreHTTPSErrors: true,
  },
  projects: [
    {
      name: 'desktop',
      use: { ...devices['Desktop Chrome'], viewport: { width: 1280, height: 800 } },
    },
    {
      name: 'mobile',
      use: {
        ...devices['iPhone 15'],
        // Emulated in Chromium (the only browser the devcontainer installs) rather than WebKit.
        // 2x instead of the device's 3x keeps full-page PNGs a reasonable size in the repo.
        defaultBrowserType: 'chromium',
        deviceScaleFactor: 2,
      },
    },
  ],
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
