import { SEEDED_USERS, expect, test } from './support/auth-fixture';
import { createDisposableGuardian, deleteKeycloakUser } from './support/keycloak-admin-client';

// SAFETY: this spec must NEVER delete alice/bob/carol (SEEDED_USERS) -- every other e2e spec in
// this suite depends on all three continuing to exist and being able to log in. It provisions its
// own brand-new, disposable Keycloak guardian account (support/keycloak-admin-client.ts) and only
// ever acts on that account. The seeded users' known usernames/emails are hard-asserted against
// immediately before the destructive click, as a fail-safe independent of anything computed
// earlier in the test.
const SEEDED_USERNAMES = Object.values(SEEDED_USERS).map((user) => user.username);
const SEEDED_EMAILS = ['alice@buddy.test', 'bob@buddy.test', 'carol@buddy.test'];

test('a disposable guardian account deletes itself from the danger zone, and is signed out', async ({ page, loginAs }) => {
  const disposable = await createDisposableGuardian();

  // Fail-safe #1: assert the account we're about to drive is not a seeded one, before ever
  // touching the browser.
  expect(SEEDED_USERNAMES).not.toContain(disposable.username);
  expect(SEEDED_EMAILS).not.toContain(disposable.email);

  await loginAs(disposable);

  await page.goto('/guardian/admin');

  const profileSection = page.locator('app-my-profile');
  await expect(profileSection.getByRole('heading', { name: 'Your profile' })).toBeVisible();

  // Fail-safe #2: re-check against what the app itself rendered for the currently authenticated
  // account (not just what this test computed), immediately before opening the delete dialog.
  const emailInput = profileSection.getByLabel('Email');
  await expect(emailInput).toHaveValue(disposable.email);

  for (const seededEmail of SEEDED_EMAILS) {
    await expect(emailInput).not.toHaveValue(seededEmail);
  }

  const deleteSection = page.locator('app-delete-account');
  await expect(deleteSection.getByRole('heading', { name: 'Danger zone' })).toBeVisible();
  await deleteSection.getByRole('button', { name: 'Delete my account' }).click();

  await expect(page.getByRole('heading', { name: 'Delete your account?' })).toBeVisible();

  // Fail-safe #3: one last check, right at the point of no return, immediately before the
  // destructive click.
  expect(SEEDED_USERNAMES).not.toContain(disposable.username);
  expect(SEEDED_EMAILS).not.toContain(disposable.email);

  await page.getByRole('button', { name: 'Yes, delete my account' }).click();

  // DeleteAccount calls AuthService.logout(), which navigates through Keycloak's real end-session
  // endpoint and back to /login -- an actual cross-origin round trip, not just a local route change.
  await expect(page).toHaveURL(/\/login$/, { timeout: 15_000 });

  // The buddy backend only soft-deletes its own User aggregate (DeleteCurrentUser.Handler.cs never
  // calls Keycloak); reclaim the Keycloak identity itself so disposable accounts don't accumulate
  // in the realm across repeated runs. Best-effort -- see deleteKeycloakUser's own comment.
  await deleteKeycloakUser(disposable.username);
});
