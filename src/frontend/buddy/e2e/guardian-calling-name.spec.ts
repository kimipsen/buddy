import { DISPOSABLE_GUARDIAN_GIVEN_NAME, expect, test } from './support/auth-fixture';
import { createChild } from './support/guardian-data';
import { makeChildLoginUsable } from './support/keycloak-admin-client';

// A guardian sets what their children call them on My profile, assigns themself to today's
// drop-off, and the child's home page shows the calling name instead of the given name
// (docs/backend/analysis/guardian-calling-names.md). A disposable guardian (newGuardian) so the
// calling name never leaks into the seeded users other specs assert on.
test('a child sees the calling name their guardian set on today’s pickup', async ({
  page,
  loginAs,
  newGuardian,
}) => {
  const guardian = await newGuardian();
  await loginAs(guardian);
  const child = await createChild(page);

  // createChild leaves the page on /guardian/admin, where My profile also lives.
  const profile = page.locator('app-my-profile');
  await profile.getByLabel('What your children call you').fill('Mor');
  await profile.getByRole('button', { name: 'Save calling name' }).click();
  await expect(profile.getByText('Calling name updated.')).toBeVisible();

  await page.reload();
  await expect(profile.getByLabel('What your children call you')).toHaveValue('Mor');

  // Guardian screens keep the real name: the pickup picker still offers the given name.
  await page.goto('/guardian/pickup');
  const dropOffCell = page.locator('tbody tr').first().locator('td').first();
  await dropOffCell.getByRole('button', { name: 'Not planned' }).click();
  await page
    .getByLabel('Choose a guardian')
    .selectOption({ label: DISPOSABLE_GUARDIAN_GIVEN_NAME });
  await page.getByRole('button', { name: 'Save' }).click();
  await expect(
    dropOffCell.getByRole('button', { name: DISPOSABLE_GUARDIAN_GIVEN_NAME, exact: true }),
  ).toBeVisible();

  // The child's side.
  await loginAs(await makeChildLoginUsable(child.username));
  await expect(page).toHaveURL(/\/child$/);
  await expect(page.getByRole('heading', { name: 'Today’s pickup & drop-off' })).toBeVisible();
  const dropOff = page.getByRole('listitem').filter({ hasText: 'Drop-off' });
  // The name sits next to an aria-hidden 👤 icon, so match the row's text rather than exact text.
  await expect(dropOff).toContainText('Mor');
  await expect(dropOff).not.toContainText(DISPOSABLE_GUARDIAN_GIVEN_NAME);
});
