#!/usr/bin/env node
// Screenshot a Buddy page as a seeded user. Run from src/frontend/buddy (resolves Playwright from
// its node_modules):
//
//   node ../../../.agents/skills/run-buddy/screenshot.mjs --path /guardian --user alice --out /tmp/x.png
//
// Options:
//   --path <route>     app route to open after login (default /guardian)
//   --user <name>      alice | bob | carol (seeded in .devcontainer/keycloak/buddy-realm.json; default alice)
//   --out <file>       PNG output path (default ./buddy-screenshot.png)
//   --form             log in through the real Keycloak hosted form instead of the direct-grant fast path
//   --anon             don't log in at all
//   --full-page        capture the full scrollable page
//   --wait-for <text>  wait until this text is visible before capturing
//
// Env: BUDDY_URL (default http://localhost:4300), KEYCLOAK_URL (default http://keycloak:8080).
import { createRequire } from 'node:module';

const require = createRequire(`${process.cwd()}/`);
const { chromium } = require('@playwright/test');

// Passwords match SEEDED_USERS in src/frontend/buddy/e2e/support/auth-fixture.ts.
const USERS = {
  alice: 'alice-test-pw',
  bob: 'bob-test-pw',
  carol: 'carol-test-pw',
};

const args = process.argv.slice(2);
const opt = (name, fallback) => {
  const i = args.indexOf(`--${name}`);
  return i >= 0 ? args[i + 1] : fallback;
};
const flag = (name) => args.includes(`--${name}`);

const baseUrl = process.env.BUDDY_URL ?? 'http://localhost:4300';
const keycloakUrl = process.env.KEYCLOAK_URL ?? 'http://keycloak:8080';
const route = opt('path', '/guardian');
const user = opt('user', 'alice');
const out = opt('out', 'buddy-screenshot.png');
const waitFor = opt('wait-for');

if (!USERS[user]) {
  console.error(`Unknown user '${user}' (expected one of ${Object.keys(USERS).join(', ')})`);
  process.exit(2);
}

async function directGrantTokens() {
  const response = await fetch(`${keycloakUrl}/realms/buddy/protocol/openid-connect/token`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
    body: new URLSearchParams({
      grant_type: 'password',
      client_id: 'buddy-frontend',
      username: user,
      password: USERS[user],
      scope: 'openid profile email',
    }),
  });
  if (!response.ok) throw new Error(`Token request failed: ${response.status} ${await response.text()}`);
  const body = await response.json();
  // Same TokenSet shape/key the app uses (src/app/core/token-storage.ts).
  return {
    accessToken: body.access_token,
    refreshToken: body.refresh_token ?? null,
    idToken: body.id_token ?? null,
    expiresAt: Date.now() + body.expires_in * 1000,
  };
}

const browser = await chromium.launch();
try {
  const context = await browser.newContext({ ignoreHTTPSErrors: true, viewport: { width: 1280, height: 900 } });
  // Same idea as e2e/support/global-setup.ts, without touching the checked-in file: point the
  // app's Keycloak authority at KEYCLOAK_URL. Inside the devcontainer localhost:9080 is not the
  // Keycloak the API trusts (see SKILL.md), so the checked-in value would yield 401s.
  await context.route('**/config/runtime-config.json', async (route) => {
    const response = await route.fetch();
    const config = await response.json();
    config.keycloak.authority = keycloakUrl;
    await route.fulfill({ response, json: config });
  });
  const page = await context.newPage();
  page.on('console', (msg) => msg.type() === 'error' && console.error(`[browser] ${msg.text()}`));
  page.on('response', (res) => res.status() >= 400 && console.error(`[http ${res.status()}] ${res.request().method()} ${res.url()}`));

  if (flag('form')) {
    await page.goto(`${baseUrl}/login`);
    await page.getByRole('button', { name: 'Sign in with Keycloak' }).click();
    await page.getByLabel('Username or email').fill(user);
    await page.getByLabel('Password', { exact: true }).fill(USERS[user]);
    await page.getByRole('button', { name: 'Sign In' }).click();
    // Keycloak redirects back to redirectPath (/dashboard?code=...); the app exchanges the code and
    // then routes on to /guardian or /child. Wait for that final hop -- navigating earlier aborts
    // the code exchange and leaves you logged out.
    await page.waitForURL(/\/(guardian|child)(\/|$)/, { timeout: 30_000 });
  } else if (!flag('anon')) {
    const tokens = await directGrantTokens();
    await page.addInitScript(
      ({ key, value }) => window.sessionStorage.setItem(key, value),
      { key: 'buddy_keycloak_tokens', value: JSON.stringify(tokens) },
    );
  }

  await page.goto(`${baseUrl}${route}`);
  await page.waitForLoadState('networkidle');
  if (waitFor) await page.getByText(waitFor).first().waitFor({ state: 'visible', timeout: 15_000 });

  await page.screenshot({ path: out, fullPage: flag('full-page') });
  console.log(`Saved ${out} (url: ${page.url()})`);
} finally {
  await browser.close();
}
