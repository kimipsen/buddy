import { expect, test } from './support/auth-fixture';

// Exercises the guardian's own work locations end to end against the real API: add a location,
// save an alternating two-week pattern, then mark one of its days off as an exception, and check
// all of it survives a reload.
//
// A disposable guardian, not a seeded one: work locations are per-guardian state that would
// otherwise accumulate across runs (and the backend caps a guardian at 12 active locations). No
// child is needed -- any non-child account can keep work locations.

// The overrides grid labels each day's picker "Location on <weekday, month day>" using the app
// language (English for a fresh account), formatted the same way WorkDayOverrides.dayLabel does.
function dayPickerLabel(date: Date): string {
  const formatted = date.toLocaleDateString('en', {
    weekday: 'short',
    day: 'numeric',
    month: 'short',
  });
  return `Location on ${formatted}`;
}

test('guardian sets an alternating work pattern and marks a day off, and it persists', async ({
  page,
  loginAs,
  newGuardian,
}) => {
  await loginAs(await newGuardian());

  await page.goto('/guardian/work-locations');
  await expect(page.getByRole('heading', { name: 'Where you work, day by day.' })).toBeVisible();

  const locations = page.locator('app-manage-work-locations');
  await locations.getByLabel('Name').fill('Stil');
  await locations.getByRole('button', { name: 'Add location' }).click();
  await expect(locations.getByText('Stil', { exact: true })).toBeVisible();

  // A fresh pattern is anchored on this week, so "This week is" defaults to Week A.
  const pattern = page.locator('app-work-pattern-editor');
  await pattern.getByRole('radio', { name: '2 weeks' }).click();
  await expect(pattern.getByRole('radio', { name: 'Week A' })).toHaveAttribute(
    'aria-checked',
    'true',
  );
  await pattern.getByLabel('Week A, Tue').selectOption({ label: '🏢 Stil' });
  await pattern.getByLabel('Week B, Thu').selectOption({ label: '🏢 Stil' });
  await pattern.getByRole('button', { name: 'Save pattern' }).click();
  await expect(pattern.getByText('Pattern saved.')).toBeVisible();

  // This week's Tuesday now resolves to Stil from the pattern; next week's Tuesday (week B) doesn't.
  const now = new Date();
  const monday = new Date(
    now.getFullYear(),
    now.getMonth(),
    now.getDate() - ((now.getDay() + 6) % 7),
  );
  const tuesday = new Date(monday.getFullYear(), monday.getMonth(), monday.getDate() + 1);
  const nextTuesday = new Date(tuesday.getFullYear(), tuesday.getMonth(), tuesday.getDate() + 7);

  const overrides = page.locator('app-work-day-overrides');
  const tuesdayCell = overrides.locator('li', { has: page.getByLabel(dayPickerLabel(tuesday)) });
  const nextTuesdayCell = overrides.locator('li', {
    has: page.getByLabel(dayPickerLabel(nextTuesday)),
  });

  // Assert on the cell's <p> lines, not the whole <li>: the <li> text also includes the picker's
  // <option>s, which always list every location.
  await expect(tuesdayCell.locator('p', { hasText: 'Stil' })).toBeVisible();
  await expect(tuesdayCell).toContainText('From pattern');
  await expect(nextTuesdayCell.locator('p', { hasText: 'Stil' })).toHaveCount(0);
  await expect(nextTuesdayCell).not.toContainText('From pattern');

  await tuesdayCell.getByRole('combobox').selectOption({ label: 'Off' });
  await expect(tuesdayCell).toContainText('Exception');
  await expect(tuesdayCell.locator('p', { hasText: 'Stil' })).toHaveCount(0);

  await page.reload();
  await expect(page.getByRole('heading', { name: 'Where you work, day by day.' })).toBeVisible();

  const patternAfterReload = page.locator('app-work-pattern-editor');
  await expect(patternAfterReload.getByRole('radio', { name: '2 weeks' })).toHaveAttribute(
    'aria-checked',
    'true',
  );
  await expect(patternAfterReload.getByLabel('Week B, Thu')).toHaveValue(/.+/);

  const tuesdayAfterReload = page
    .locator('app-work-day-overrides')
    .locator('li', { has: page.getByLabel(dayPickerLabel(tuesday)) });
  await expect(tuesdayAfterReload).toContainText('Exception');
  await expect(tuesdayAfterReload.getByRole('combobox')).toHaveValue('off');
});
