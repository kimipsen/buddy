import { expect, test } from './support/auth-fixture';
import { createChild, createGroup, uniqueName } from './support/guardian-data';

test('guardian shares a medicine schedule with a group, then stops the schedule', async ({
  page,
  loginAs,
  newGuardian,
}) => {
  // A disposable guardian whose only child is this test's own (see newGuardian in auth-fixture.ts).
  await loginAs(await newGuardian());

  await createChild(page);
  const groupName = await createGroup(page);

  const medicineName = uniqueName('E2E Medicine');

  await page.goto('/guardian/medicine');
  await expect(
    page.getByRole('heading', { name: 'Medicine schedules', exact: true }),
  ).toBeVisible();

  await page.getByLabel('Medicine name').fill(medicineName);
  await page.getByLabel('Dosage (e.g. 5ml)').fill('5ml');
  await page.getByRole('button', { name: 'Add schedule' }).click();
  await expect(page.getByText(medicineName, { exact: false })).toBeVisible();

  // Share with the group.
  await page.getByLabel('Choose a group').selectOption({ label: groupName });

  const [shareResponse] = await Promise.all([
    page.waitForResponse(
      (res) => res.request().method() === 'PUT' && res.url().includes('/group-share/'),
    ),
    page.getByRole('button', { name: 'Share', exact: true }).click(),
  ]);
  expect(shareResponse.ok()).toBe(true);

  await expect(page.getByText('Shared with', { exact: false })).toBeVisible();
  await expect(page.getByText(groupName, { exact: true })).toBeVisible();

  // Sharing persists across a reload.
  await page.reload();
  await expect(page.getByText('Shared with', { exact: false })).toBeVisible();
  await expect(page.getByText(groupName, { exact: true })).toBeVisible();

  // Stop the schedule -- ManageMedicines.loadSchedules filters out stopped schedules entirely, so
  // a successful stop means the row disappears from the active list on its own (no separate
  // "inactive" badge to check for, unlike an archived task template).
  const scheduleRow = page.locator('li', { hasText: medicineName });
  await scheduleRow.getByRole('button', { name: 'Stop', exact: true }).click();

  const [stopResponse] = await Promise.all([
    page.waitForResponse(
      (res) => res.request().method() === 'DELETE' && res.url().includes('/schedules/'),
    ),
    scheduleRow.getByRole('button', { name: 'Confirm', exact: true }).click(),
  ]);
  expect(stopResponse.ok()).toBe(true);

  await expect(page.locator('li', { hasText: medicineName })).toHaveCount(0);

  // Persists across a reload too.
  await page.reload();
  await expect(page.locator('li', { hasText: medicineName })).toHaveCount(0);

  // A stopped schedule generates no further doses -- confirm it's absent from the guardian
  // dashboard's doses-today widget too (mirrors medicine-dose-status.spec.ts's own selector for
  // that widget).
  await page.goto('/guardian');
  await expect(page.getByRole('heading', { name: 'Guardian dashboard' })).toBeVisible();
  await expect(page.locator('li', { hasText: medicineName })).toHaveCount(0);
});
