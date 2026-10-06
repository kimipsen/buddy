import { expect, test } from './support/auth-fixture';
import { endKeycloakSessions } from './support/keycloak-admin-client';

// A Keycloak SSO session that idles out (30 min by default) leaves the app holding a refresh token
// Keycloak rejects. This spec reproduces that without waiting: it ends the user's Keycloak sessions
// through the Admin API (the same server-side state an idle timeout leaves), then moves the
// browser clock past the 5-minute access-token lifetime so the app's next token lookup makes a
// real refresh request and gets a real rejection.
//
// The tokens live in AuthService's memory, so editing sessionStorage mid-page would change
// nothing; the fake clock is what makes the in-memory token look expired. A disposable guardian
// is used because ending a seeded user's sessions would break parallel specs signed in as them.
//
// Navigating inside the guardian tree must then land on /login with the "session expired" notice
// (authGuard runs as canActivateChild), and signing in again through the real Keycloak form must
// return to the interrupted page (pending-return-url.ts), not the dashboard.
test('an expired session sends in-app navigation to /login, and signing in returns to that page', async ({
  page,
  loginAs,
  newGuardian,
}) => {
  const guardian = await newGuardian();
  await page.clock.install();
  await loginAs(guardian);
  await expect(page.getByRole('heading', { name: 'Guardian dashboard' })).toBeVisible();

  await endKeycloakSessions(guardian);
  await page.clock.fastForward('06:00');

  // An in-app (router) navigation, not a page load.
  await page.getByRole('button', { name: 'Open account menu' }).click();
  await page.getByRole('link', { name: 'Calendar' }).click();

  await expect(page).toHaveURL(/\/login\?reason=session-expired$/);
  await expect(page.getByText('Your session expired. Sign in again to continue.')).toBeVisible();

  await page.getByRole('button', { name: 'Sign in with Keycloak' }).click();
  await page.getByLabel('Username or email').fill(guardian.username);
  await page.getByLabel('Password', { exact: true }).fill(guardian.password);
  await page.getByRole('button', { name: 'Sign In' }).click();

  await expect(page).toHaveURL(/\/guardian\/calendar$/);
});
