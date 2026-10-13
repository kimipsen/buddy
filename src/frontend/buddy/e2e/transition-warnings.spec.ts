import { type APIRequestContext, request } from '@playwright/test';

import { expect, test } from './support/auth-fixture';
import { createCalendar, createChild, createGroup, uniqueName } from './support/guardian-data';
import { getAccessToken } from './support/keycloak-client';
import { makeChildLoginUsable } from './support/keycloak-admin-client';
import { readRuntimeConfig } from './support/runtime-config';
import type { TestUser } from './support/seeded-users';

// Transition warnings end to end (docs/backend/analysis/transition-warnings.md), against the real
// API, Keycloak and Postgres: a child with no settings sees no heads-up; a guardian turns them on
// at /guardian/transition-warnings with 10 minutes' notice and a "no heads-up" exception for one
// item; the child then sees the countdown on the home page and the calendar page, and "Got it"
// hides it.
//
// A disposable guardian because createChild is involved. The two events are created through the
// API, 8 and 9 minutes from now on the calendar's wall clock, and the child's profile time zone is
// set to the calendar's, so the child's "today" (ChildClockService) is the events' day. No fake
// browser clock: one ahead of real time would expire the session. The countdown's own minutes come
// from the real clock, so the assertions match "<n> minutes until ...". The child signs in by
// direct grant once makeChildLoginUsable has replaced its temporary password.
test('a guardian turns on heads-ups and the child sees a countdown before the next event', async ({
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

  // The child joins the group, so the group calendar's events reach their pages.
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

  const football = uniqueName('E2E Football');
  const swimming = uniqueName('E2E Swimming');
  const created = await createEventsSoon(guardian, calendarName, [
    [football, 8],
    [swimming, 9],
  ]);
  test.skip(!created, 'The events would start after midnight in the calendar’s time zone.');

  // Off by default: the child sees the events but no heads-up.
  const childLogin = await makeChildLoginUsable(child.username);
  await loginAs(childLogin);
  await expect(page).toHaveURL(/\/child$/);
  await expect(page.getByText(football).first()).toBeVisible();
  await expect(page.getByTestId('transition-warning')).toHaveCount(0);

  // The guardian finds the setting from the child's line on the admin page.
  await loginAs(guardian);
  await page.goto('/guardian/admin');
  const childRow = page
    .locator('app-manage-children')
    .getByRole('listitem')
    .filter({ hasText: childLabel })
    .first();
  await expect(childRow.getByTestId('warning-status')).toContainText(
    'Heads-up before changes: Off',
  );
  await childRow.getByTestId('warning-status').getByRole('link', { name: 'Off' }).click();
  await expect(page).toHaveURL(/\/guardian\/transition-warnings\?child=/);

  const warnSwitch = page.getByRole('switch', { name: `Warn ${child.givenName} before changes` });
  await expect(warnSwitch).toHaveAttribute('aria-checked', 'false');
  await warnSwitch.click();
  // 15, 5 and 1 are suggested; swap 15 for 10.
  const leadTimes = page.locator('app-page app-lead-chips').first();
  await expect(leadTimes.getByRole('button', { name: '15 min' })).toHaveAttribute(
    'aria-pressed',
    'true',
  );
  await leadTimes.getByRole('button', { name: '15 min' }).click();
  await leadTimes.getByRole('button', { name: '10 min' }).click();
  await page.getByRole('button', { name: 'Save', exact: true }).click();
  await expect(page.getByText('Saved.')).toBeVisible();

  // No heads-up at all for swimming.
  const exceptions = page.locator('app-warning-exceptions');
  await exceptions.getByRole('button', { name: 'Add exception' }).click();
  await exceptions.getByLabel('Item', { exact: true }).selectOption({ label: `📅 ${swimming}` });
  await exceptions.getByRole('switch', { name: 'No heads-up for this item' }).click();
  await exceptions.getByRole('button', { name: 'Save exception' }).click();
  const swimmingRow = exceptions.getByRole('listitem').filter({ hasText: swimming });
  await expect(swimmingRow).toContainText('No heads-up');

  // Both persist.
  await page.reload();
  await expect(warnSwitch).toHaveAttribute('aria-checked', 'true');
  await expect(leadTimes.getByRole('button', { name: '10 min' })).toHaveAttribute(
    'aria-pressed',
    'true',
  );
  await expect(swimmingRow).toContainText('No heads-up');

  // The child: a countdown to football on the home page and on the calendar, none for swimming.
  await loginAs(childLogin);
  await expect(page).toHaveURL(/\/child$/);
  const banner = page.getByTestId('transition-warning');
  await expect(banner).toContainText(new RegExp(`\\d+ minutes? until ${football}`));
  await expect(banner).not.toContainText(swimming);

  await page.goto('/child/calendar');
  await expect(banner).toContainText(new RegExp(`\\d+ minutes? until ${football}`));

  await banner.getByRole('button', { name: 'Got it' }).click();
  await expect(banner).toHaveCount(0);
});

// Creates one-hour events on the named calendar, each starting the given number of minutes from
// now on the calendar's wall clock, and sets the guardian's only child to the calendar's time zone.
// False (nothing created) when one would start on the next day there.
async function createEventsSoon(
  guardian: TestUser,
  calendarName: string,
  events: [title: string, minutesFromNow: number][],
): Promise<boolean> {
  const { accessToken } = await getAccessToken(guardian.username, guardian.password);
  const api = await request.newContext({
    baseURL: readRuntimeConfig().apiBaseUrl,
    ignoreHTTPSErrors: true,
    extraHTTPHeaders: { Authorization: `Bearer ${accessToken}` },
  });

  try {
    const [child] = await json<{ id: string }[]>(api, 'GET', '/users/me/children');
    const calendars = await json<{ id: string; name: string }[]>(api, 'GET', '/calendars');
    const calendarId = calendars.find((calendar) => calendar.name === calendarName)?.id;
    if (!child || !calendarId) {
      throw new Error(`child or calendar '${calendarName}' not found`);
    }
    const { timeZoneId } = await json<{ timeZoneId: string }>(
      api,
      'GET',
      `/calendars/${calendarId}`,
    );
    await json(api, 'PATCH', `/users/me/children/${child.id}/timezone`, { timeZoneId });

    const today = wallClock(new Date(), timeZoneId).date;
    const windows = events.map(([title, minutes]) => ({
      title,
      start: wallClock(new Date(Date.now() + minutes * 60_000), timeZoneId),
      end: wallClock(new Date(Date.now() + (minutes + 60) * 60_000), timeZoneId),
    }));
    if (windows.some((window) => window.start.date !== today)) {
      return false;
    }

    for (const { title, start, end } of windows) {
      await json(api, 'POST', `/calendars/${calendarId}/items`, {
        title,
        icon: '📅',
        color: '#10b981',
        schedule: {
          kind: 0,
          startsAt: start,
          endsAt: end,
          isAllDay: false,
        },
      });
    }
    return true;
  } finally {
    await api.dispose();
  }
}

async function json<T = unknown>(
  api: APIRequestContext,
  method: 'GET' | 'POST' | 'PATCH',
  path: string,
  data?: unknown,
): Promise<T> {
  const response = await api.fetch(path, { method, data });
  if (!response.ok()) {
    throw new Error(`${method} ${path} failed: ${response.status()} ${await response.text()}`);
  }
  const text = await response.text();
  return (text ? JSON.parse(text) : undefined) as T;
}

// The date (yyyy-MM-dd) and time (HH:mm:00) an instant shows on a wall clock in `timeZone`.
function wallClock(instant: Date, timeZone: string): { date: string; time: string } {
  const parts = Object.fromEntries(
    new Intl.DateTimeFormat('en-GB', {
      timeZone,
      year: 'numeric',
      month: '2-digit',
      day: '2-digit',
      hour: '2-digit',
      minute: '2-digit',
      hourCycle: 'h23',
    })
      .formatToParts(instant)
      .map((part) => [part.type, part.value]),
  );
  return {
    date: `${parts['year']}-${parts['month']}-${parts['day']}`,
    time: `${parts['hour']}:${parts['minute']}:00`,
  };
}
