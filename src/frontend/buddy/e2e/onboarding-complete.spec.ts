import { expect, test } from './support/auth-fixture';
import { trackCreatedChild } from './support/created-data-cleanup';
import { uniqueName } from './support/guardian-data';

// The guided first-login setup (docs/frontend/analysis/guardian-onboarding.md) end to end: a brand
// new guardian (newGuardian with inGuide, so nothing is set up and the guide isn't deferred) is
// sent into the guide on login and creates real data through every step -- a group, two children,
// invitations, a group calendar, a two-step routine scheduled once, and a planned meal -- then
// finishes and lands on the dashboard for good. Everything is checked through the app afterwards.
test('a new guardian is guided through setting up the family, and finishes on the dashboard', async ({
  page,
  loginAs,
  newGuardian,
}) => {
  test.setTimeout(120_000);
  const guardian = await newGuardian({ inGuide: true });
  await loginAs(guardian);

  await expect(page).toHaveURL(/\/guardian\/onboarding$/);
  await expect(page.getByRole('heading', { name: 'Create your group' })).toBeVisible();

  const groupName = uniqueName('E2E Family ');
  await page.getByLabel('Group name').fill(groupName);
  await page.getByRole('button', { name: 'Create group' }).click();

  await expect(page.getByRole('heading', { name: 'Add your children' })).toBeVisible();
  const childNames = [uniqueName('E2eAda'), uniqueName('E2eEmil')];
  for (const givenName of childNames) {
    const username = uniqueName('e2echild').toLowerCase();
    await page.getByLabel('First name').fill(givenName);
    await page.getByLabel('Last name').fill('Testson');
    await page.getByLabel('Username').fill(username);
    await page.getByRole('button', { name: 'Add child' }).click();
    // Creating a child provisions a Keycloak user first, so give it the same room createChild does.
    await expect(page.getByText(`Sign-in for ${givenName}`)).toBeVisible({ timeout: 15_000 });
    await expect(page.getByRole('listitem').filter({ hasText: givenName })).toContainText(
      'In the group',
    );
    // Children made here bypass createChild(), so register their Keycloak users for cleanup.
    trackCreatedChild({ givenName, familyName: 'Testson', username });
  }
  await page.getByRole('button', { name: 'Continue' }).click();

  await expect(page.getByRole('heading', { name: /Invite other adults/ })).toBeVisible();
  await page.getByLabel('Email').fill(`${uniqueName('e2e-aunt-')}@buddy.test`);
  await page.getByRole('radio', { name: 'Member: can see the calendar' }).click();
  await page.getByRole('button', { name: 'Send invitations' }).click();
  // One guardian invitation per child plus the group invitation, each reported on its own.
  await expect(page.getByRole('status').getByText('Sent', { exact: true })).toHaveCount(3);
  await page.getByRole('button', { name: 'Continue' }).click();

  await expect(page.getByRole('heading', { name: 'Create a shared calendar' })).toBeVisible();
  await expect(page.getByText('Children and other members: can see it')).toBeVisible();
  const calendarName = uniqueName('E2E Calendar ');
  await page.getByLabel('Calendar name').fill(calendarName);
  await page.getByRole('button', { name: 'Create calendar' }).click();
  await expect(page.getByRole('listitem').filter({ hasText: calendarName })).toBeVisible();
  await page.getByRole('button', { name: 'Continue' }).click();

  await expect(page.getByRole('heading', { name: 'Schedule a first routine' })).toBeVisible();
  await page.getByLabel('Routine name').fill('Morning routine');
  // exact: "Step 1" is also a substring of the stepper's "Minutes for step 1".
  await page.getByLabel('Step 1', { exact: true }).fill('Brush teeth');
  await page.getByLabel('Step 2', { exact: true }).fill('Get dressed');
  await page.getByRole('button', { name: 'Save routine' }).click();
  await page.getByRole('button', { name: 'Add to calendar' }).click();
  await expect(page.getByText('The routine is on the calendar.')).toBeVisible();
  await page.getByRole('button', { name: 'Continue' }).click();

  await expect(page.getByRole('heading', { name: 'Plan a first meal' })).toBeVisible();
  await page.getByLabel('Meal name').fill('Spaghetti');
  await page.getByRole('button', { name: 'Add to meal plan' }).click();
  await expect(page.getByText(/Planned: Spaghetti/)).toBeVisible();
  await page.getByRole('button', { name: 'Continue' }).click();

  await expect(page.getByRole('heading', { name: 'All set?' })).toBeVisible();
  for (const text of [groupName, ...childNames, calendarName, 'Scheduled', 'Planned']) {
    await expect(page.getByRole('definition').filter({ hasText: text })).toBeVisible();
  }
  await page.getByRole('button', { name: 'Finish setup' }).click();

  await expect(page.getByRole('heading', { name: 'Guardian dashboard' })).toBeVisible();

  // Completed for good: the home no longer redirects, and there's no resume prompt.
  await page.reload();
  await expect(page.getByRole('heading', { name: 'Guardian dashboard' })).toBeVisible();
  await expect(page.getByRole('link', { name: 'Resume setup' })).toHaveCount(0);

  // The data is real: the group with both children is on the admin page.
  await page.goto('/guardian/admin');
  const children = page.locator('app-manage-children');
  for (const givenName of childNames) {
    await expect(children.getByText(givenName)).toBeVisible();
  }
  await expect(page.locator('app-manage-groups').getByText(groupName)).toBeVisible();
});
