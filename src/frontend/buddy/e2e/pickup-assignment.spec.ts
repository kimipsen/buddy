import { SEEDED_USERS, expect, test } from './support/auth-fixture';
import { createChild, selectChildIfPresent } from './support/guardian-data';

test('guardian assigns a pickup person for a child/day cell, and it persists', async ({ page, loginAs }) => {
  await loginAs(SEEDED_USERS.carol);

  const child = await createChild(page);

  await page.goto('/guardian/pickup');
  await expect(page.getByRole('heading', { name: 'Weekly schedule' })).toBeVisible();
  await selectChildIfPresent(page, child.givenName);

  // The weekly grid's first row is always anchored on today (ManagePickups.buildWeek starts from
  // `new Date()`) and the Drop-off column is always first, so this is today's drop-off cell.
  const todayRow = page.locator('tbody tr').first();
  const dropOffCell = todayRow.locator('td').first();

  await dropOffCell.getByRole('button', { name: 'Not planned' }).click();

  // Kind defaults to "A guardian", which is what's being tested here -- the seeded child's only
  // guardian at this point is the creator (Carol), so she's the sole option in the dropdown.
  await page.getByLabel('Choose a guardian').selectOption({ label: 'Carol' });
  await page.getByRole('button', { name: 'Save' }).click();

  // The assigned summary renders as a single button whose accessible name is the guardian's given
  // name (an aria-hidden icon precedes it in the DOM text, so a plain text match would see
  // "👤 Carol" instead -- see pickup-cell.html).
  await expect(dropOffCell.getByRole('button', { name: 'Carol', exact: true })).toBeVisible();

  await page.reload();

  // A reload re-initializes ManagePickups from scratch, which -- like every other page in this app
  // that has a "family scope" -- defaults to whichever child comes first from ListMyChildren, not
  // necessarily this test's own child (see ManagePickups.loadChildren). Carol is a long-lived shared
  // fixture with other children created by other specs, so this child must be re-selected after
  // reload too, exactly as it was before.
  await selectChildIfPresent(page, child.givenName);

  const dropOffCellAfterReload = page.locator('tbody tr').first().locator('td').first();
  await expect(dropOffCellAfterReload.getByRole('button', { name: 'Carol', exact: true })).toBeVisible();
});
