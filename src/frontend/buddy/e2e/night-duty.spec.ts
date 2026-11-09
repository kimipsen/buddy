import { DISPOSABLE_GUARDIAN_GIVEN_NAME, expect, test } from './support/auth-fixture';
import { createChild } from './support/guardian-data';

// Night duty end to end against the real API (docs/backend/analysis/night-duty.md): save a fixed
// schedule with a screen night, change a single night, switch two nights (each keeps its note),
// undo a change, and check it all survives a reload.
//
// A disposable guardian whose only child is this test's own, so the schedule never leaks into
// other runs. The week view starts tonight, so its first two tiles are tonight and tomorrow.

const WEEKDAYS = ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday'];

test('guardian plans the nights, changes and switches them, and it persists', async ({
  page,
  loginAs,
  newGuardian,
}) => {
  await loginAs(await newGuardian());
  await createChild(page);

  await page.goto('/guardian/night-duty');
  await expect(page.getByRole('heading', { name: "Who's on duty at night." })).toBeVisible();

  // Every night of the one-week schedule is the guardian's; tomorrow is screen night.
  const pattern = page.locator('app-night-pattern-editor');
  const guardianOption = { label: `👤 ${DISPOSABLE_GUARDIAN_GIVEN_NAME}` };
  for (const day of WEEKDAYS) {
    await pattern.getByLabel(`Week A, ${day}: who`).selectOption(guardianOption);
  }
  const tomorrow = WEEKDAYS[(new Date().getDay() + 1) % 7]!;
  await pattern.getByLabel(`Week A, ${tomorrow}: note`).fill('Screen night');
  await pattern.getByRole('button', { name: 'Save schedule' }).click();
  await expect(pattern.getByText('Schedule saved.')).toBeVisible();
  await expect(pattern.getByText(/\(current\)/)).toBeVisible();

  const week = page.locator('app-night-week');
  const tiles = week.locator('[data-night]');
  const tonight = tiles.nth(0);
  const tomorrowTile = tiles.nth(1);
  const dayAfter = tiles.nth(2);

  await expect(tonight).toContainText(DISPOSABLE_GUARDIAN_GIVEN_NAME);
  await expect(tonight).toContainText('From schedule');
  await expect(tomorrowTile).toContainText('Screen night');

  // Change one night: nothing planned the day after tomorrow.
  await dayAfter.getByRole('button', { name: /^Change/ }).click();
  const dialog = page.getByRole('dialog');
  await dialog.getByLabel('Who is on duty').selectOption({ label: 'Nothing planned' });
  await dialog.getByRole('button', { name: 'Save night' }).click();
  await expect(dialog).toBeHidden();
  await expect(dayAfter).toContainText('Changed');
  await expect(dayAfter).toContainText('Nothing planned');

  // Switch tonight and the day after: the guardian moves, each night keeps its own note.
  await week.getByRole('button', { name: 'Switch two nights' }).click();
  await tonight.getByRole('button').click();
  await dayAfter.getByRole('button').click();
  await week.getByRole('button', { name: /^Switch .* and / }).click();
  await expect(tonight).toContainText('Changed');
  await expect(tonight).toContainText('Nothing planned');
  await expect(dayAfter).toContainText(DISPOSABLE_GUARDIAN_GIVEN_NAME);

  // Undo tonight: back to the schedule.
  await tonight.getByRole('button', { name: /^Undo/ }).click();
  await expect(tonight).toContainText('From schedule');
  await expect(tonight).toContainText(DISPOSABLE_GUARDIAN_GIVEN_NAME);

  await page.reload();
  await expect(page.getByRole('heading', { name: "Who's on duty at night." })).toBeVisible();
  const reloaded = page.locator('app-night-week [data-night]');
  await expect(reloaded.nth(0)).toContainText('From schedule');
  await expect(reloaded.nth(1)).toContainText('Screen night');
  await expect(reloaded.nth(2)).toContainText('Changed');
  await expect(reloaded.nth(2)).toContainText(DISPOSABLE_GUARDIAN_GIVEN_NAME);
});
