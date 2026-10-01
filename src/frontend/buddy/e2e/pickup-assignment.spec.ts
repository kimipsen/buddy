import { DISPOSABLE_GUARDIAN_GIVEN_NAME, expect, test } from './support/auth-fixture';
import { createChild } from './support/guardian-data';

test('guardian assigns a pickup person for a child/day cell, and it persists', async ({
  page,
  loginAs,
  newGuardian,
}) => {
  // A disposable guardian whose only child is this test's own (see newGuardian in auth-fixture.ts).
  await loginAs(await newGuardian());

  await createChild(page);

  await page.goto('/guardian/pickup');
  await expect(page.getByRole('heading', { name: 'Weekly schedule' })).toBeVisible();

  // The weekly grid's first row is always anchored on today (ManagePickups.buildWeek starts from
  // `new Date()`) and the Drop-off column is always first, so this is today's drop-off cell.
  const todayRow = page.locator('tbody tr').first();
  const dropOffCell = todayRow.locator('td').first();

  await dropOffCell.getByRole('button', { name: 'Not planned' }).click();

  // Kind defaults to "A guardian", which is what's being tested here -- the child's only
  // guardian is its creator (this test's disposable guardian), so they're the sole option.
  await page
    .getByLabel('Choose a guardian')
    .selectOption({ label: DISPOSABLE_GUARDIAN_GIVEN_NAME });
  await page.getByRole('button', { name: 'Save' }).click();

  // The assigned summary renders as a single button whose accessible name is the guardian's given
  // name (an aria-hidden icon precedes it in the DOM text, so a plain text match would see
  // "👤 <name>" instead -- see pickup-cell.html).
  await expect(
    dropOffCell.getByRole('button', { name: DISPOSABLE_GUARDIAN_GIVEN_NAME, exact: true }),
  ).toBeVisible();

  await page.reload();

  const dropOffCellAfterReload = page.locator('tbody tr').first().locator('td').first();
  await expect(
    dropOffCellAfterReload.getByRole('button', {
      name: DISPOSABLE_GUARDIAN_GIVEN_NAME,
      exact: true,
    }),
  ).toBeVisible();
});
