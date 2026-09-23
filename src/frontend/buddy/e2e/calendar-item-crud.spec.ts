import { SEEDED_USERS, expect, test } from './support/auth-fixture';
import { createCalendar, createGroup, uniqueName } from './support/guardian-data';

test('guardian creates, edits, and deletes a calendar event', async ({ page, loginAs }) => {
  await loginAs(SEEDED_USERS.carol);

  const groupName = await createGroup(page);
  const calendarName = await createCalendar(page, groupName);

  const originalTitle = uniqueName('E2E Event');
  const editedTitle = uniqueName('E2E Event Edited');

  await page.goto('/guardian/calendar');
  await expect(page.getByRole('heading', { name: 'This week' })).toBeVisible();

  const calendarSelect = page.locator('#itemCalendar');
  await expect(calendarSelect).toContainText(calendarName);
  await calendarSelect.selectOption({ label: calendarName });

  await page.getByRole('radio', { name: 'Event', exact: true }).click();
  await page.getByLabel('Title').fill(originalTitle);
  await page.getByRole('radio', { name: '#10b981' }).click();
  await page.getByLabel('Start time').fill('09:30');
  await page.getByLabel('End time').fill('10:30');
  await page.getByRole('button', { name: 'Add to calendar' }).click();

  await expect(page.getByText(originalTitle, { exact: true })).toBeVisible();

  // Switch to the month grid and confirm the new item shows up there as a chip too.
  await page.getByRole('button', { name: 'Month', exact: true }).click();
  const monthChip = page.locator('button', { hasText: originalTitle });
  await expect(monthChip).toBeVisible();

  // Drilling into the chip's day switches back to Day view, where the item is directly editable.
  await monthChip.click();

  const itemRow = page.locator('li', { hasText: originalTitle });
  await expect(itemRow).toBeVisible();
  await itemRow.getByRole('button', { name: 'Edit' }).click();

  const editForm = page.locator('form', { hasText: 'Save' });
  await editForm.getByLabel('Title').fill(editedTitle);
  await editForm.getByRole('button', { name: 'Save' }).click();

  await expect(page.getByText(editedTitle, { exact: true })).toBeVisible();
  await expect(page.getByText(originalTitle, { exact: true })).toHaveCount(0);

  const editedRow = page.locator('li', { hasText: editedTitle });
  await editedRow.getByRole('button', { name: 'Delete', exact: true }).click();
  await editedRow.getByRole('button', { name: 'Confirm' }).click();

  await expect(page.getByText(editedTitle, { exact: true })).toHaveCount(0);
});
