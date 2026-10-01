import { expect, test } from './support/auth-fixture';
import { createChild, uniqueName } from './support/guardian-data';

test('guardian marks a dose taken from the dashboard doses-today widget, and it persists', async ({ page, loginAs, newGuardian }) => {
  // A disposable guardian whose only child is this test's own (see newGuardian in auth-fixture.ts).
  await loginAs(await newGuardian());

  await createChild(page);

  const medicineName = uniqueName('E2E Medicine');

  await page.goto('/guardian/medicine');
  await expect(page.getByRole('heading', { name: 'Medicine schedules', exact: true })).toBeVisible();

  await page.getByLabel('Medicine name').fill(medicineName);
  await page.getByLabel('Dosage (e.g. 5ml)').fill('5ml');
  // Dose times default to a single "08:00" entry and the start date defaults to today -- both are
  // exactly what's needed for a dose to show up on today's dashboard, so nothing else to fill in.
  await page.getByRole('button', { name: 'Add schedule' }).click();

  await expect(page.getByText(medicineName, { exact: false })).toBeVisible();

  await page.goto('/guardian');
  await expect(page.getByRole('heading', { name: 'Guardian dashboard' })).toBeVisible();

  const doseRow = page.locator('li', { hasText: medicineName });
  await expect(doseRow).toBeVisible();
  await doseRow.getByRole('button', { name: 'Mark taken' }).click();

  await expect(doseRow.getByText('Taken', { exact: true })).toBeVisible();
  await expect(doseRow.getByRole('button', { name: 'Undo' })).toBeVisible();

  await page.reload();

  const doseRowAfterReload = page.locator('li', { hasText: medicineName });
  await expect(doseRowAfterReload.getByText('Taken', { exact: true })).toBeVisible();
});
