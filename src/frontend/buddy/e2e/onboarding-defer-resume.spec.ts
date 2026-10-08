import { SEEDED_USERS, expect, test } from './support/auth-fixture';
import { trackCreatedChild } from './support/created-data-cleanup';
import { uniqueName } from './support/guardian-data';

// Leaving the guided setup early and coming back (docs/frontend/analysis/guardian-onboarding.md,
// "Entry and resume rules"): creating the group makes the guide active (the home redirects to it),
// "Finish later" defers it, the dashboard offers to resume it, and resuming lands on the first
// unfinished step without recreating what exists. Then an explicit skip of the optional
// invitations.
test('a guardian finishes setup later, resumes where they left off, and skips the invitations', async ({
  page,
  loginAs,
  newGuardian,
}) => {
  test.setTimeout(90_000);
  const guardian = await newGuardian({ inGuide: true });
  await loginAs(guardian);

  await expect(page.getByRole('heading', { name: 'Create your group' })).toBeVisible();
  const groupName = uniqueName('E2E Later ');
  await page.getByLabel('Group name').fill(groupName);
  await page.getByRole('button', { name: 'Create group' }).click();
  await expect(page.getByRole('heading', { name: 'Add your children' })).toBeVisible();

  // The guardian now has a group, but the started guide still takes the home.
  await page.goto('/guardian');
  await expect(page).toHaveURL(/\/guardian\/onboarding$/);
  await expect(page.getByRole('heading', { name: 'Add your children' })).toBeVisible();

  await page.getByRole('button', { name: 'Finish later' }).click();
  await expect(page.getByRole('heading', { name: 'Guardian dashboard' })).toBeVisible();

  // Deferred is remembered across a reload and a fresh login: no redirect, just the prompt.
  await loginAs(guardian);
  await expect(page.getByRole('heading', { name: 'Guardian dashboard' })).toBeVisible();
  await page.getByRole('link', { name: 'Resume setup' }).click();

  // The group step is done, so the guide resumes at the children, with the same group.
  await expect(page.getByRole('heading', { name: 'Add your children' })).toBeVisible();
  await expect(page.getByRole('navigation', { name: 'Setup progress' })).toContainText(
    /Group\s*\(done\)/,
  );

  const givenName = uniqueName('E2eIda');
  const username = uniqueName('e2echild').toLowerCase();
  await page.getByLabel('First name').fill(givenName);
  await page.getByLabel('Last name').fill('Testson');
  await page.getByLabel('Username').fill(username);
  await page.getByRole('button', { name: 'Add child' }).click();
  await expect(page.getByText(`Sign-in for ${givenName}`)).toBeVisible({ timeout: 15_000 });
  trackCreatedChild({ givenName, familyName: 'Testson', username });
  await page.getByRole('button', { name: 'Continue' }).click();

  await page.getByRole('button', { name: 'Skip for now' }).click();
  await expect(page.getByRole('heading', { name: 'Create a shared calendar' })).toBeVisible();

  // Reloading resumes at the same first unfinished step: the skip persisted. A resumed guide stays
  // deferred, so the home keeps offering it instead of redirecting.
  await page.reload();
  await expect(page.getByRole('heading', { name: 'Create a shared calendar' })).toBeVisible();
  await page.goto('/guardian');
  await expect(page.getByRole('link', { name: 'Resume setup' })).toBeVisible();

  // Still exactly one group of that name: resuming never created a second one.
  await page.goto('/guardian/admin');
  await expect(page.locator('app-manage-groups').getByText(groupName)).toHaveCount(1);
});

test('a guardian whose setup is out of the way goes straight to the dashboard', async ({
  page,
  loginAs,
}) => {
  await loginAs(SEEDED_USERS.alice);

  await expect(page).toHaveURL(/\/guardian$/);
  await expect(page.getByRole('heading', { name: 'Guardian dashboard' })).toBeVisible();
});
