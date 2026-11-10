import { DISPOSABLE_GUARDIAN_GIVEN_NAME, expect, test } from './support/auth-fixture';
import { createChild } from './support/guardian-data';

// The sleep diary shows who was on duty each logged night, read from night duty (GitHub issue
// #68). A disposable guardian whose only child is this test's own: tonight is planned in night
// duty (the guardian, with a note), then logged in the sleep diary.
test('a logged night shows who was on duty and the night’s note', async ({
  page,
  loginAs,
  newGuardian,
}) => {
  await loginAs(await newGuardian());
  await createChild(page);

  // The night-duty week starts tonight, so its first tile is tonight.
  await page.goto('/guardian/night-duty');
  const tonightTile = page.locator('app-night-week [data-night]').first();
  await tonightTile.getByRole('button', { name: /^Change/ }).click();
  const dialog = page.getByRole('dialog');
  await dialog
    .getByLabel('Who is on duty')
    .selectOption({ label: `👤 ${DISPOSABLE_GUARDIAN_GIVEN_NAME}` });
  await dialog.getByLabel('Note (optional)').fill('Screen night');
  await dialog.getByRole('button', { name: 'Save night' }).click();
  await expect(dialog).toBeHidden();
  await expect(tonightTile).toContainText('Screen night');

  await page.goto('/guardian/sleep-diary');
  const history = page.locator('app-sleep-history');
  await expect(history.getByRole('columnheader', { name: 'On duty' })).toBeVisible();

  // Not logged yet: nothing extra on the row.
  const tonightRow = history.locator('tbody tr').first();
  await expect(tonightRow).toContainText('Not logged');
  await expect(tonightRow).not.toContainText('Screen night');

  const form = page.locator('app-sleep-entry-form');
  await form.getByLabel('Lies down to sleep').fill('20:15');
  await form.getByRole('button', { name: 'Save night' }).click();
  await expect(form.getByRole('status')).toHaveText('Saved.');

  const cell = tonightRow.getByTestId('night-duty-cell');
  await expect(cell).toContainText(DISPOSABLE_GUARDIAN_GIVEN_NAME);
  await expect(cell).toContainText('Screen night');
});
