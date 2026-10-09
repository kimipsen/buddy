import { type Page, expect, test } from '@playwright/test';
import { mkdirSync } from 'node:fs';
import { join } from 'node:path';

import { KEYCLOAK_URL, browserTokenSet } from './demo-family';
import { type DemoFamily, SCREENSHOT_PAGES } from './pages';
import { screenshotsDirFor } from './screenshots-dir';

const demo = JSON.parse(process.env['BUDDY_DEMO_FAMILY'] ?? 'null') as DemoFamily | null;

test.beforeAll(({}, testInfo) => {
  if (!demo) {
    throw new Error('BUDDY_DEMO_FAMILY is not set; run through playwright.screenshots.config.ts');
  }

  mkdirSync(screenshotsDirFor(testInfo.project.name), { recursive: true });
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
  test(entry.name, async ({ page }, testInfo) => {
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
      path: join(screenshotsDirFor(testInfo.project.name), `${entry.name}.png`),
      fullPage: entry.fullPage ?? true,
      animations: 'disabled',
    });

    // Checked after the screenshot, so a failing page still leaves its PNG to look at.
    expect(
      await horizontalOverflow(page),
      `${entry.name} is wider than the ${testInfo.project.name} screen`,
    ).toBeNull();
  });
}

// How far the page is wider than the screen, with the elements sticking out furthest, or null
// when it fits. Compared with the root's clientWidth (the layout viewport without a scrollbar),
// because a mobile browser may zoom out to fit wide content and so change innerWidth.
async function horizontalOverflow(page: Page): Promise<string | null> {
  return page.evaluate(() => {
    const root = document.documentElement;
    const extra = root.scrollWidth - root.clientWidth;
    if (extra <= 0) {
      return null;
    }

    // "app-doses-today > button.rounded-lg.bg-emerald-600": the nearest component and the element.
    const describe = (el: Element) => {
      const component = findComponent(el);
      const classes = [...el.classList]
        .slice(0, 3)
        .map((c) => `.${c}`)
        .join('');
      return `${component ? `${component} > ` : ''}${el.tagName.toLowerCase()}${classes}`;
    };
    const findComponent = (el: Element) => {
      for (let a = el.parentElement; a; a = a.parentElement) {
        if (a.tagName.includes('-')) {
          return a.tagName.toLowerCase();
        }
      }
      return null;
    };

    // Content inside an overflow-x-auto box scrolls there and doesn't widen the page, unless it is
    // absolutely positioned against something outside that box (an sr-only span in a scrolling
    // table with no positioned ancestor in between does exactly that).
    const isClipped = (el: Element) => {
      const position = getComputedStyle(el).position;
      if (position === 'fixed') {
        return false;
      }
      // An absolute element skips static ancestors until it reaches its containing block.
      let escaping = position === 'absolute';
      for (let a = el.parentElement; a; a = a.parentElement) {
        const style = getComputedStyle(a);
        if (escaping) {
          if (style.position === 'static') {
            continue;
          }
          escaping = false;
        }
        if (style.overflowX !== 'visible') {
          return true;
        }
      }
      return false;
    };
    const sticksOut = (el: Element) =>
      el.getBoundingClientRect().right > root.clientWidth + 1 && !isClipped(el);

    const widest = [...document.body.querySelectorAll('*')]
      .filter(sticksOut)
      // Keep the outermost offenders: an element whose parent also sticks out adds nothing.
      .filter((el) => !(el.parentElement && sticksOut(el.parentElement)))
      .map((el) => ({ el, right: el.getBoundingClientRect().right }))
      .sort((a, b) => b.right - a.right)
      .slice(0, 3)
      .map(({ el, right }) => `${describe(el)} (right edge ${Math.round(right)}px)`);

    return `${extra}px wider than the ${root.clientWidth}px screen; ${widest.join('; ')}`;
  });
}
