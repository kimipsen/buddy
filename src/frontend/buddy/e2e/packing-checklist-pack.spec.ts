import { expect, test } from './support/auth-fixture';
import { createCalendar, createChild, createGroup, uniqueName } from './support/guardian-data';
import { makeChildLoginUsable } from './support/keycloak-admin-client';

// A packing list end to end (docs/backend/analysis/packing-checklists.md), against the real API,
// Keycloak and Postgres: a guardian builds a "School bag" packing list with an every-day item and
// one bound to a weekday that isn't today, schedules it for today ("Leave by"), and the child sees
// only today's item, ticks it, and earns exactly one star for the packed list.
//
// A disposable guardian because createChild is involved (the task library defaults to the
// guardian's first child). The child joins the group, so the group calendar's list reaches their
// home page, and signs in by direct grant once makeChildLoginUsable has replaced its temporary
// password (see reward-redemption.spec.ts). The "evening before" view depends on the time of day,
// so it is covered by the unit specs (home.spec.ts, packing-tomorrow.spec.ts), not here.
const WEEKDAYS = ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday'];

test('a child packs today’s list from a weekday-aware packing list and earns one star', async ({
  page,
  loginAs,
  newGuardian,
}) => {
  const guardian = await newGuardian();
  await loginAs(guardian);
  const child = await createChild(page);
  const groupName = await createGroup(page);
  const calendarName = await createCalendar(page, groupName);
  const childLabel = `${child.givenName} ${child.familyName}`;

  const groupRow = page.locator('app-manage-groups').locator('li', { hasText: groupName });
  await groupRow.getByRole('button', { name: 'Add a child' }).click();
  await groupRow.getByLabel('Choose a child').selectOption({ label: childLabel });
  const [addChildResponse] = await Promise.all([
    page.waitForResponse(
      (res) =>
        res.request().method() === 'PUT' && /\/groups\/[^/]+\/children\/[^/]+$/.test(res.url()),
    ),
    groupRow.getByRole('button', { name: 'Add to group' }).click(),
  ]);
  expect(addChildResponse.ok()).toBe(true);

  // The packing list: an every-day item, and one for a weekday other than today (the browser and
  // the calendar share the devcontainer's clock, so "today" agrees on both sides).
  await page.goto('/guardian/task-library');
  await expect(page.getByRole('heading', { name: 'Task templates', exact: true })).toBeVisible();
  const listName = uniqueName('E2E School bag');
  await page.getByRole('radio', { name: 'Packing list' }).click();
  await page.getByLabel('Template name').fill(listName);
  await page.getByRole('button', { name: 'Add template' }).click();
  const listRow = page.locator('app-manage-tasks li', { hasText: listName }).first();
  await expect(listRow.getByText('Packing list', { exact: true })).toBeVisible();
  // A new packing list starts with the evening view on, from 17:00.
  await expect(
    listRow.getByRole('switch', { name: 'Show the evening before from' }),
  ).toHaveAttribute('aria-checked', 'true');

  await page.getByLabel('Item', { exact: true }).fill('Lunchbox');
  await page.getByRole('button', { name: 'Add item' }).click();
  await expect(listRow.getByRole('listitem').filter({ hasText: 'Lunchbox' })).toContainText(
    'Every day',
  );

  const otherDay = WEEKDAYS[(new Date().getDay() + 2) % 7]!;
  await page.getByLabel('Item', { exact: true }).fill('Swimming kit');
  await page.locator('app-manage-tasks form').getByRole('button', { name: otherDay }).click();
  await page.getByRole('button', { name: 'Add item' }).click();
  await expect(listRow.getByText('Swimming kit')).toBeVisible();
  await expect(listRow.getByText('(2 items)')).toBeVisible();

  // Scheduled for today from the calendar; a packing list's time reads "Leave by".
  await page.goto('/guardian/calendar');
  await expect(page.getByRole('heading', { name: 'This week' })).toBeVisible();
  const calendarSelect = page.locator('#itemCalendar');
  await expect(calendarSelect).toContainText(calendarName);
  await calendarSelect.selectOption({ label: calendarName });
  await page.getByRole('radio', { name: 'Task', exact: true }).click();
  await page.getByLabel('Assign to').selectOption({ label: childLabel });
  await page.getByRole('button', { name: 'From template' }).click();
  const picker = page.getByLabel('No template');
  await picker.click();
  await picker.fill(listName);
  await page.getByRole('button', { name: new RegExp(`${listName}.*2 items`) }).click();
  await expect(page.getByText('Leave by', { exact: true })).toBeVisible();
  await page.getByRole('button', { name: 'Add to calendar' }).click();
  await expect(page.getByText(listName).first()).toBeVisible();

  // The child sees only today's item, packs it, and the packed list earns one star.
  const childLogin = await makeChildLoginUsable(child.username);
  await loginAs(childLogin);
  await expect(page).toHaveURL(/\/child$/);
  const list = page.getByRole('listitem').filter({ hasText: listName }).first();
  await expect(list.getByText('0 of 1 packed')).toBeVisible();
  await expect(list.getByText('Swimming kit')).toHaveCount(0);
  await list.getByRole('button', { name: 'Mark done' }).click();
  await expect(list.getByText('1 of 1 packed')).toBeVisible();

  await page.reload();
  await expect(list.getByText('1 of 1 packed')).toBeVisible();
  await page.goto('/child/rewards');
  await expect(page.getByTestId('reward-balance')).toHaveText(/\b1 stars to spend/);
});
