import { expect, test } from '@playwright/test';
import { mkdirSync } from 'node:fs';
import { join } from 'node:path';

import { KEYCLOAK_URL, browserTokenSet } from './demo-family';
import { type DemoFamily, SCREENSHOT_PAGES } from './pages';
import { SCREENSHOTS_DIR } from './screenshots-dir';

const demo = JSON.parse(process.env['BUDDY_DEMO_FAMILY'] ?? 'null') as DemoFamily | null;

test.beforeAll(() => {
  if (!demo) {
    throw new Error('BUDDY_DEMO_FAMILY is not set; run through playwright.screenshots.config.ts');
  }

  mkdirSync(SCREENSHOTS_DIR, { recursive: true });
});

test.beforeEach(async ({ context }) => {
  // The checked-in runtime-config.json points the browser at Keycloak on localhost:9080, which
  // inside the devcontainer isn't the Keycloak the API trusts. Rewrite it per request rather than
  // on disk, so a parallel e2e run or dev server is never affected.
  await context.route('**/config/runtime-config.json', async (route) => {
    const response = await route.fetch();
    const config = await response.json();
    config.keycloak.authority = KEYCLOAK_URL;
    await route.fulfill({ response, json: config });
  });
});

for (const entry of SCREENSHOT_PAGES) {
  test(entry.name, async ({ page }) => {
    const path = entry.path ? entry.path(demo!) : entry.route;
    test.skip(path === null, 'No data to show this page with (see the demo seed warnings)');

    const errors: string[] = [];
    page.on('response', (res) => {
      if (res.status() >= 500) {
        errors.push(`${res.status()} ${res.request().method()} ${res.url()}`);
      }
    });

    if (entry.as !== 'anonymous') {
      const user = entry.as === 'guardian' ? demo!.guardian : demo!.child;
      const tokens = await browserTokenSet(user.username, user.password);
      await page.addInitScript(
        (value) => window.sessionStorage.setItem('buddy_keycloak_tokens', value),
        JSON.stringify(tokens),
      );
    }

    await page.goto(path!);
    await page.waitForLoadState('networkidle');

    if (entry.waitFor) {
      await expect(page.getByText(entry.waitFor).first()).toBeVisible();
    }

    await expect(page.locator('app-loading-spinner')).toHaveCount(0);
    expect(errors, 'server errors while loading the page').toEqual([]);

    await page.screenshot({
      path: join(SCREENSHOTS_DIR, `${entry.name}.png`),
      fullPage: entry.fullPage ?? true,
      animations: 'disabled',
    });
  });
}
