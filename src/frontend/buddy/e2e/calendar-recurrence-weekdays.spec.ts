import { SEEDED_USERS, expect, test } from './support/auth-fixture';
import { createCalendar, createGroup, uniqueName } from './support/guardian-data';

// Weekday recurrence rules, through the agenda create form and the real occurrence expansion.
// The default Week view is a rolling seven days starting today, so a daily event starting today
// shows up seven times. Keeping only tomorrow's weekday must leave exactly one occurrence, which
// also proves the start date (today) is skipped when its weekday is off.
test('guardian limits a daily event to one weekday and it occurs once a week', async ({
  page,
  loginAs,
}) => {
  await loginAs(SEEDED_USERS.carol);

  const groupName = await createGroup(page);
  const calendarName = await createCalendar(page, groupName);
  const title = uniqueName('E2E Weekday');

  const tomorrow = new Date();
  tomorrow.setDate(tomorrow.getDate() + 1);
  const tomorrowName = tomorrow.toLocaleDateString('en-US', { weekday: 'long' });

  await page.goto('/guardian/calendar');
  await expect(page.getByRole('heading', { name: 'This week' })).toBeVisible();

  const calendarSelect = page.locator('#itemCalendar');
  await expect(calendarSelect).toContainText(calendarName);
  await calendarSelect.selectOption({ label: calendarName });

  await page.getByRole('radio', { name: 'Event', exact: true }).click();
  await page.getByLabel('Title').fill(title);
  await page.getByLabel('Repeat').selectOption({ label: 'Daily' });

  const weekdays = page.getByRole('group', { name: 'On these days' });
  for (const day of [
    'Monday',
    'Tuesday',
    'Wednesday',
    'Thursday',
    'Friday',
    'Saturday',
    'Sunday',
  ]) {
    if (day !== tomorrowName) {
      await weekdays.getByRole('button', { name: day }).click();
    }
  }
  await expect(weekdays.getByRole('button', { name: tomorrowName })).toHaveAttribute(
    'aria-pressed',
    'true',
  );

  await page.getByRole('button', { name: 'Add to calendar' }).click();

  await expect(page.getByText(title, { exact: true })).toHaveCount(1);

  await page.reload();
  await expect(page.getByRole('heading', { name: 'This week' })).toBeVisible();
  await expect(page.getByText(title, { exact: true })).toHaveCount(1);
});

// A weekly rule on several days: today (the start date's weekday, switched on by default) plus
// tomorrow's weekday. The rolling seven-day Week view holds each weekday once, so the event shows
// up exactly twice.
test('guardian repeats a weekly event on two weekdays and it occurs on both', async ({
  page,
  loginAs,
}) => {
  await loginAs(SEEDED_USERS.carol);

  const groupName = await createGroup(page);
  const calendarName = await createCalendar(page, groupName);
  const title = uniqueName('E2E Weekly days');

  const tomorrow = new Date();
  tomorrow.setDate(tomorrow.getDate() + 1);
  const todayName = new Date().toLocaleDateString('en-US', { weekday: 'long' });
  const tomorrowName = tomorrow.toLocaleDateString('en-US', { weekday: 'long' });

  await page.goto('/guardian/calendar');
  await expect(page.getByRole('heading', { name: 'This week' })).toBeVisible();

  const calendarSelect = page.locator('#itemCalendar');
  await expect(calendarSelect).toContainText(calendarName);
  await calendarSelect.selectOption({ label: calendarName });

  await page.getByRole('radio', { name: 'Event', exact: true }).click();
  await page.getByLabel('Title').fill(title);
  await page.getByLabel('Repeat').selectOption({ label: 'Weekly' });

  const weekdays = page.getByRole('group', { name: 'On these days' });
  await expect(weekdays.getByRole('button', { name: todayName })).toHaveAttribute(
    'aria-pressed',
    'true',
  );
  await weekdays.getByRole('button', { name: tomorrowName }).click();
  await expect(weekdays.getByRole('button', { name: tomorrowName })).toHaveAttribute(
    'aria-pressed',
    'true',
  );

  await page.getByRole('button', { name: 'Add to calendar' }).click();

  await expect(page.getByText(title, { exact: true })).toHaveCount(2);

  await page.reload();
  await expect(page.getByRole('heading', { name: 'This week' })).toBeVisible();
  await expect(page.getByText(title, { exact: true })).toHaveCount(2);
});
