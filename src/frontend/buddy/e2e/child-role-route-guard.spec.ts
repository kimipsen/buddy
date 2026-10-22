import { SEEDED_USERS, expect, test } from './support/auth-fixture';
import { createChild } from './support/guardian-data';
import {
  createDisposableGuardian,
  deleteKeycloakUser,
  makeChildLoginUsable,
} from './support/keycloak-admin-client';

// The role trees are gated by Keycloak group (core/group.guard.ts): '/guardian' needs
// buddy-guardian, '/child' needs buddy-child. Anyone outside a tree's group is sent to the root,
// where roleRedirectGuard picks their own tree, or signs them out to /login?reason=no-access when
// they're in neither group.
test('a guardian landing on the root path is redirected to /guardian, not /child', async ({
  page,
  loginAs,
}) => {
  await loginAs(SEEDED_USERS.carol);

  await page.goto('/');

  await expect(page).toHaveURL(/\/guardian$/);
  await expect(page.getByRole('heading', { name: 'Guardian dashboard' })).toBeVisible();
});

test('a guardian who opens /child directly is sent back to /guardian', async ({
  page,
  loginAs,
}) => {
  await loginAs(SEEDED_USERS.carol);

  await page.goto('/child');

  await expect(page).toHaveURL(/\/guardian$/);
  await expect(page.getByRole('heading', { name: 'Guardian dashboard' })).toBeVisible();
});

// The child is created through the manage-children UI, so this also proves the app put it in the
// buddy-child group: without it, the child would be signed out with the no-access notice instead.
test('a child created in the app lands on /child and is sent back there from /guardian', async ({
  page,
  loginAs,
  newGuardian,
}) => {
  await loginAs(await newGuardian());
  const child = await createChild(page);
  const childLogin = await makeChildLoginUsable(child.username);

  await loginAs(childLogin);
  await expect(page).toHaveURL(/\/child$/);

  await page.goto('/guardian');

  await expect(page).toHaveURL(/\/child$/);
  await expect(page.getByRole('heading', { name: 'Today', level: 1 })).toBeVisible();
});

test('an account in neither Buddy group is signed out with a no-access notice', async ({
  page,
  loginAs,
}) => {
  const outsider = await createDisposableGuardian('e2enoaccess', []);

  try {
    await loginAs(outsider);

    await expect(page).toHaveURL(/\/login\?reason=no-access$/);
    await expect(page.getByRole('alert')).toContainText(
      'This account has not been given access to Buddy yet.',
    );
  } finally {
    await deleteKeycloakUser(outsider.username);
  }
});
