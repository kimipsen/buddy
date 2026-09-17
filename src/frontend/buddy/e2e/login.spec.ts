import { expect, test } from '@playwright/test';

import { SEEDED_USERS } from './support/auth-fixture';

// The one spec that drives the real Keycloak redirect (PKCE authorization-code flow) end to end,
// including Keycloak's own hosted login form. Every other spec uses the loginAs fixture's
// direct-grant fast path instead -- see support/auth-fixture.ts.
test('signs in through Keycloak and lands on the guardian dashboard', async ({ page }) => {
  await page.goto('/login');
  await page.getByRole('button', { name: 'Sign in with Keycloak' }).click();

  await page.getByLabel('Username or email').fill(SEEDED_USERS.alice.username);
  await page.getByLabel('Password', { exact: true }).fill(SEEDED_USERS.alice.password);
  await page.getByRole('button', { name: 'Sign In' }).click();

  await expect(page).toHaveURL(/\/guardian$/);
  await expect(page.getByRole('heading', { name: 'Guardian dashboard' })).toBeVisible();
});
