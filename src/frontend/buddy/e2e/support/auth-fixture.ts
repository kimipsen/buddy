import { test as base } from '@playwright/test';

import { cleanUpCreatedData, trackDisposableGuardian } from './created-data-cleanup';
import {
  DISPOSABLE_GUARDIAN_GIVEN_NAME,
  type DisposableGuardian,
  createDisposableGuardian,
} from './keycloak-admin-client';
import { getAccessToken } from './keycloak-client';
import { readRuntimeConfig } from './runtime-config';
import { SEEDED_USERS, type TestUser } from './seeded-users';

export { DISPOSABLE_GUARDIAN_GIVEN_NAME, SEEDED_USERS, type DisposableGuardian, type TestUser };

// Same storage key the app writes to (src/app/core/token-storage.ts).
const STORAGE_KEY = 'buddy_keycloak_tokens';

export const test = base.extend<{
  loginAs: (user: TestUser) => Promise<void>;
  newGuardian: () => Promise<DisposableGuardian>;
  cleanUpCreatedData: void;
}>({
  // Runs for every test (auto) and, after the test body, removes what guardian-data.ts helpers
  // created -- see created-data-cleanup.ts. Without it every run left its children linked to the
  // shared seeded guardians.
  cleanUpCreatedData: [
    async ({}, use) => {
      await use();
      await cleanUpCreatedData();
    },
    { auto: true },
  ],

  // A fresh, throwaway guardian (verified email, no children, no family) for specs that depend on
  // family-wide or account-wide state, which a shared seeded guardian can't give a parallel test in
  // isolation. Removed again (backend user + Keycloak identity) by cleanUpCreatedData.
  newGuardian: async ({}, use) => {
    await use(async () => {
      const guardian = await createDisposableGuardian('e2eguardian');
      trackDisposableGuardian(guardian);
      return guardian;
    });
  },

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
      // The app redirects from redirectPath to the role's home route. Wait for that redirect so a
      // caller's next page.goto doesn't race it ("navigation interrupted by another navigation").
      await page.waitForURL((url) => url.pathname !== keycloak.redirectPath);
    });
  },
});

export { expect } from '@playwright/test';
