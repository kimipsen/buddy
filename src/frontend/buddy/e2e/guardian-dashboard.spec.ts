import { SEEDED_USERS, expect, test } from './support/auth-fixture';

// Smoke test for the fast-login path (support/auth-fixture.ts): proves a token minted via
// Keycloak's direct-grant flow and seeded into sessionStorage is accepted by both the app's route
// guards and the real backend API, without going through Keycloak's hosted login form.
test('renders the guardian dashboard for a seeded user', async ({ page, loginAs }) => {
  await loginAs(SEEDED_USERS.bob);

  await expect(page).toHaveURL(/\/guardian$/);
  await expect(page.getByRole('heading', { name: 'Guardian dashboard' })).toBeVisible();
});
