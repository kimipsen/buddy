import { expect, test } from './support/auth-fixture';
import { createCalendar, createGroup, uniqueName } from './support/guardian-data';

// Skipping one day of a repeating task (docs/backend/analysis/skip-occurrences.md) through the
// agenda, against the real occurrence expansion. A child can't log in through the UI here (see
// child-role-route-guard.spec.ts), so "the day disappears for everyone else" is checked on the
// guardian dashboard's tasks-today widget instead: like every screen except the agenda, it lists
// occurrences without includeSkipped. A daily task starting today shows on all seven days of the
// rolling Week view, so skipping today leaves six live rows plus one muted "Skipped" row.
//
// A disposable guardian rather than a seeded one: the dashboard fans out one occurrences request
// per calendar, and a seeded guardian shared with parallel workers (and earlier runs' calendars)
// runs into the per-user rate limit, which fails every widget at once.
test('guardian skips today of a daily task, it leaves the dashboard, and restoring brings it back', async ({
  page,
  loginAs,
  newGuardian,
}) => {
  await loginAs(await newGuardian());

  const groupName = await createGroup(page);
  const calendarName = await createCalendar(page, groupName);
  const title = uniqueName('E2E Skip');

  await page.goto('/guardian/calendar');
  await expect(page.getByRole('heading', { name: 'This week' })).toBeVisible();

  const calendarSelect = page.locator('#itemCalendar');
  await expect(calendarSelect).toContainText(calendarName);
  await calendarSelect.selectOption({ label: calendarName });

  await page.getByRole('radio', { name: 'Task' }).click();
  await page.getByLabel('Title').fill(title);
  await page.getByLabel('Repeat').selectOption({ label: 'Daily' });
  await page.getByRole('button', { name: 'Add to calendar' }).click();

  const rows = page.locator('li', { hasText: title });
  await expect(rows).toHaveCount(7);

  // Today is the first day of the rolling week, so the first row is today's.
  const todayRow = rows.first();
  await todayRow.getByRole('button', { name: 'Skip day' }).click();
  await expect(todayRow).toContainText('Skip just this day?');
  await todayRow.getByRole('button', { name: 'Skip', exact: true }).click();

  await expect(todayRow).toContainText('Skipped');
  await expect(todayRow.getByRole('button', { name: 'Restore' })).toBeVisible();
  await expect(rows).toHaveCount(7);

  await page.reload();
  await expect(page.getByRole('heading', { name: 'This week' })).toBeVisible();
  await expect(rows.first()).toContainText('Skipped');
  await expect(page.locator('li', { hasText: 'Skipped' }).filter({ hasText: title })).toHaveCount(
    1,
  );

  await page.goto('/guardian');
  await expect(page.getByRole('heading', { name: 'Guardian dashboard' })).toBeVisible();
  await expect(page.getByRole('switch', { name: title })).toHaveCount(0);

  await page.goto('/guardian/calendar');
  await expect(page.getByRole('heading', { name: 'This week' })).toBeVisible();
  await rows.first().getByRole('button', { name: 'Restore' }).click();
  await expect(page.locator('li', { hasText: 'Skipped' }).filter({ hasText: title })).toHaveCount(
    0,
  );

  await page.goto('/guardian');
  await expect(page.getByRole('heading', { name: 'Guardian dashboard' })).toBeVisible();
  await expect(page.getByRole('switch', { name: title })).toBeVisible();
});
