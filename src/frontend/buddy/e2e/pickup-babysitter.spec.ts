import { expect, test } from './support/auth-fixture';
import { createChild, uniqueName } from './support/guardian-data';

// The full babysitter path: save a babysitter on /guardian/babysitters (BabysitterList, one stream
// per guardian), then pick them for a pickup slot (PickupAssignee kind 4), whose name the API
// resolves from that list on every read -- see docs/backend/analysis/babysitters.md.
test('guardian saves a babysitter and assigns them to a pickup slot, and it persists', async ({
  page,
  loginAs,
  newGuardian,
}) => {
  // A disposable guardian whose only child is this test's own (see newGuardian in auth-fixture.ts);
  // the babysitter list is per guardian, so it starts empty too.
  await loginAs(await newGuardian());
  await createChild(page);

  const babysitterName = uniqueName('Maja');

  await page.goto('/guardian/babysitters');
  await expect(page.getByRole('heading', { name: 'Your babysitters and nannies.' })).toBeVisible();
  await expect(page.getByText('No babysitters yet.', { exact: false })).toBeVisible();

  await page.getByLabel('Name', { exact: true }).fill(babysitterName);
  await page.getByLabel('Contact info (optional)').fill('+45 20 30 40 50');
  await page.getByRole('button', { name: 'Add babysitter' }).click();

  const entry = page.locator('li', { hasText: babysitterName });
  await expect(entry).toContainText('+45 20 30 40 50');

  await page.goto('/guardian/pickup');
  await expect(page.getByRole('heading', { name: 'Weekly schedule' })).toBeVisible();

  // Today's drop-off cell: the first row is always today and the Drop-off column comes first
  // (see pickup-assignment.spec.ts).
  const dropOffCell = page.locator('tbody tr').first().locator('td').first();
  await dropOffCell.getByRole('button', { name: 'Not planned' }).click();

  await dropOffCell.getByRole('radio', { name: 'Babysitter' }).click();
  await dropOffCell.getByLabel('Choose a babysitter').selectOption({ label: babysitterName });
  await dropOffCell.getByRole('button', { name: 'Save' }).click();

  // The summary button's accessible name is the resolved name; the 🧑‍🍼 icon is aria-hidden.
  await expect(
    dropOffCell.getByRole('button', { name: babysitterName, exact: true }),
  ).toBeVisible();

  await page.reload();

  await expect(
    page
      .locator('tbody tr')
      .first()
      .locator('td')
      .first()
      .getByRole('button', { name: babysitterName, exact: true }),
  ).toBeVisible();
});
