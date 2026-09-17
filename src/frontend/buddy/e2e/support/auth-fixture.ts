import { test as base } from '@playwright/test';

import { getAccessToken } from './keycloak-client';
import { readRuntimeConfig } from './runtime-config';

// Same storage key the app writes to (src/app/core/token-storage.ts).
const STORAGE_KEY = 'buddy_keycloak_tokens';

export interface TestUser {
  username: string;
  password: string;
}

// Seeded by .devcontainer/keycloak/buddy-realm.json -- see also TestRealm.json on the backend,
// which uses the same usernames/passwords/emails convention for its own isolated realm.
export const SEEDED_USERS = {
  alice: { username: 'alice', password: 'alice-test-pw' },
  bob: { username: 'bob', password: 'bob-test-pw' },
  carol: { username: 'carol', password: 'carol-test-pw' },
} as const satisfies Record<string, TestUser>;

export const test = base.extend<{ loginAs: (user: TestUser) => Promise<void> }>({
  // Fast path used by most specs: mints a real token via Keycloak's direct-grant flow (see
  // keycloak-client.ts) and seeds it into sessionStorage before navigating, skipping the hosted
  // login form. login.spec.ts is the one spec that drives the real redirect instead, to prove
  // the PKCE/redirect wiring itself still works.
  loginAs: async ({ page }, use) => {
    await use(async (user) => {
      const tokens = await getAccessToken(user.username, user.password);

      await page.addInitScript(({ key, value }) => window.sessionStorage.setItem(key, value), {
        key: STORAGE_KEY,
        value: JSON.stringify(tokens),
      });

      const { keycloak } = readRuntimeConfig();
      await page.goto(keycloak.redirectPath);
    });
  },
});

export { expect } from '@playwright/test';
