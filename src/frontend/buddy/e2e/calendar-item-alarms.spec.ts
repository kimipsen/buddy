import { expect, test } from './support/auth-fixture';
import { createCalendar, createGroup, uniqueName } from './support/guardian-data';

// An alarm set on one calendar item reaches the subscribed iCal feed as a VALARM
// (docs/backend/analysis/calendar-item-alarms.md): the guardian picks "2 hours before" in the
// agenda's create form, sees the bell on the row, finds the chip still selected when reopening the
// item, and the feed fetched through a fresh subscription link carries TRIGGER:-PT2H. Buddy never
// fires the alarm itself, so the feed text is the observable outcome.
//
// A disposable guardian, so the new calendar is the only one the agenda and admin page list.
test('guardian sets a 2-hours-before alarm on an event, sees it on reopening, and the feed carries it', async ({
  page,
  loginAs,
  newGuardian,
}) => {
  await loginAs(await newGuardian());

  const groupName = await createGroup(page);
  const calendarName = await createCalendar(page, groupName);
  const title = uniqueName('E2E Alarm');

  await page.goto('/guardian/calendar');
  await expect(page.getByRole('heading', { name: 'This week' })).toBeVisible();

  // #itemCalendar has no accessible name of its own beyond the shared "Calendar" label text.
  const calendarSelect = page.locator('#itemCalendar');
  await expect(calendarSelect).toContainText(calendarName);
  await calendarSelect.selectOption({ label: calendarName });

  await page.getByLabel('Title').fill(title);
  const createChip = page
    .locator('app-alarm-picker')
    .getByRole('button', { name: '2 hours before' });
  await createChip.click();
  await expect(createChip).toHaveAttribute('aria-pressed', 'true');
  await page.getByRole('button', { name: 'Add to calendar' }).click();

  const row = page.locator('li', { hasText: title });
  await expect(row).toHaveCount(1);
  await expect(row.getByRole('img', { name: 'Has alarms' })).toBeVisible();

  await page.reload();
  await expect(page.getByRole('heading', { name: 'This week' })).toBeVisible();
  await row.getByRole('button', { name: 'Edit' }).click();
  // The open edit form holds the title in an input, so the row no longer matches by text.
  const editForm = page.locator('form', { has: page.locator('input[name="editTitle"]') });
  await expect(editForm.locator('input[name="editTitle"]')).toHaveValue(title);
  await expect(
    editForm.locator('app-alarm-picker').getByRole('button', { name: '2 hours before' }),
  ).toHaveAttribute('aria-pressed', 'true');
  await expect(
    editForm.locator('app-alarm-picker').getByRole('button', { name: '1 day before' }),
  ).toHaveAttribute('aria-pressed', 'false');

  // A subscription link from the admin page, as a calendar app would get it.
  await page.goto('/guardian/admin');
  const calendarRow = page.locator('app-manage-calendars').locator('li', { hasText: calendarName });
  await calendarRow.getByRole('button', { name: 'Subscribe' }).click();
  await calendarRow.getByRole('button', { name: 'Generate new link' }).click();
  await expect(calendarRow.locator('span.font-mono')).toBeVisible();
  const feedUrl = (await calendarRow.locator('span.font-mono').textContent())?.trim();
  expect(feedUrl).toBeTruthy();

  const feed = await (await page.request.get(feedUrl!)).text();
  expect(feed).toContain(`SUMMARY:${title}`);
  expect(feed).toContain('BEGIN:VALARM');
  expect(feed).toContain('ACTION:DISPLAY');
  expect(feed).toContain(`DESCRIPTION:${title}`);
  expect(feed).toContain('TRIGGER:-PT2H');
});
