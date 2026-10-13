import { type APIRequestContext, request } from '@playwright/test';

import { expect, test } from './support/auth-fixture';
import { createCalendar, createChild, createGroup, uniqueName } from './support/guardian-data';
import { getAccessToken } from './support/keycloak-client';
import { makeChildLoginUsable } from './support/keycloak-admin-client';
import { readRuntimeConfig } from './support/runtime-config';
import type { TestUser } from './support/seeded-users';

// The child's visual time-left timer end to end (docs/frontend/analysis/visual-time-left-timer.md),
// against the real API, Keycloak and Postgres: the disc on the current step of a routine under way,
// Start / Pause surviving a reload, ticking the step moving the timer on, the focus view, and a
// guardian turning timers off for the child on /guardian/admin.
//
// A disposable guardian because createChild is involved. The routine is set up through the API
// (template, two 30-minute subtasks, scheduled for today on the guardian's calendar): picking a
// template in the agenda is covered by task-library-template-crud.spec.ts, and this spec needs the
// routine to start a couple of minutes ago in the calendar's own time zone, so the real clock is
// inside its first step. No fake browser clock: one ahead of real time would expire the session.
// The child signs in by direct grant once makeChildLoginUsable has replaced its temporary password.
test('a child sees and drives the time-left timer, until a guardian turns it off', async ({
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

  // The child joins the group, so the group calendar's routine reaches their home page.
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

  const routineTitle = uniqueName('E2E Morning');
  const itemId = await scheduleRoutineStartingNow(guardian, calendarName, routineTitle);

  // The child's side: the routine under way, with a disc on its first step.
  const childLogin = await makeChildLoginUsable(child.username);
  await loginAs(childLogin);
  await expect(page).toHaveURL(/\/child$/);
  const routine = page.getByRole('listitem').filter({ hasText: routineTitle });
  const firstStep = routine.getByRole('listitem').filter({ hasText: 'Get dressed' });
  const secondStep = routine.getByRole('listitem').filter({ hasText: 'Eat breakfast' });
  await expect(
    routine.getByRole('timer', { name: new RegExp(`left of ${routineTitle}$`) }),
  ).toBeVisible();
  await expect(firstStep.getByRole('timer', { name: /^\d+ min left$/ })).toBeVisible();
  await expect(secondStep.getByRole('timer')).toHaveCount(0);

  // Start gives the step its whole 30 minutes; Pause holds it across a reload.
  await routine.getByRole('button', { name: 'Start' }).click();
  await expect(firstStep.getByRole('timer', { name: '30 min left' })).toBeVisible();
  await routine.getByRole('button', { name: 'Pause' }).click();
  await expect(firstStep.getByRole('timer', { name: 'Paused' })).toBeVisible();
  await page.reload();
  await expect(firstStep.getByRole('timer', { name: 'Paused' })).toBeVisible();
  await expect(routine.getByRole('button', { name: 'Keep going' })).toBeVisible();

  // Ticking the step moves the (child-started) timer on to the next one, with its own 30 minutes.
  await firstStep.getByRole('button', { name: 'Mark done' }).click();
  await expect(firstStep.getByRole('button', { name: 'Mark not done' })).toBeVisible();
  await expect(secondStep.getByRole('timer', { name: '30 min left' })).toBeVisible();
  await expect(firstStep.getByRole('timer')).toHaveCount(0);

  // The focus view shows the same step, large.
  await routine.getByRole('link', { name: 'Focus' }).click();
  await expect(page).toHaveURL(new RegExp(`/child/focus/${itemId}$`));
  await expect(page.getByRole('heading', { name: 'One step at a time' })).toBeVisible();
  await expect(page.getByRole('heading', { name: /Eat breakfast/ })).toBeVisible();
  await expect(page.getByRole('timer', { name: '30 min left' })).toBeVisible();

  // A guardian turns timers off for this child.
  await loginAs(guardian);
  await page.goto('/guardian/admin');
  const childRow = page
    .locator('app-manage-children')
    .getByRole('listitem')
    .filter({ hasText: childLabel })
    .first();
  const timerSwitch = childRow.getByRole('switch', { name: 'Show time-left timers' });
  await expect(timerSwitch).toHaveAttribute('aria-checked', 'true');
  await timerSwitch.click();
  await expect(timerSwitch).toHaveAttribute('aria-checked', 'false');
  await page.reload();
  await expect(timerSwitch).toHaveAttribute('aria-checked', 'false');

  // After signing in again the child sees the routine without any timer, and the focus view
  // sends them home.
  await loginAs(childLogin);
  await expect(page).toHaveURL(/\/child$/);
  await expect(secondStep).toBeVisible();
  await expect(page.getByRole('timer')).toHaveCount(0);
  await expect(routine.getByRole('button', { name: 'Start' })).toHaveCount(0);
  await page.goto(`/child/focus/${itemId}`);
  await expect(page).toHaveURL(/\/child$/);
});

// Creates a two-step routine template for the guardian's only child and schedules it for today on
// the named calendar, starting two minutes ago in the calendar's time zone. Returns the item id.
async function scheduleRoutineStartingNow(
  guardian: TestUser,
  calendarName: string,
  title: string,
): Promise<string> {
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

    const template = await json<{ id: string }>(
      api,
      'POST',
      `/task-templates/children/${child.id}`,
      {
        name: title,
        icon: '🌅',
        color: '#f97316',
      },
    );
    for (const [subtask, icon] of [
      ['Get dressed', '👕'],
      ['Eat breakfast', '🥣'],
    ]) {
      await json(api, 'POST', `/task-templates/${template.id}/subtasks`, {
        title: subtask,
        icon,
        duration: '00:30:00',
      });
    }

    // Never before midnight: a routine starting yesterday isn't in today's list.
    const now = wallClock(new Date(), timeZoneId);
    const twoMinutesAgo = wallClock(new Date(Date.now() - 2 * 60_000), timeZoneId);
    const start = twoMinutesAgo.date === now.date ? twoMinutesAgo : { ...now, time: '00:00:00' };
    const item = await json<{ id: string }>(
      api,
      'POST',
      `/calendars/${calendarId}/items/from-template`,
      {
        taskTemplateId: template.id,
        startDate: start.date,
        startTime: start.time,
        title,
        color: '#f97316',
        icon: '🌅',
        recurrence: null,
        assignedTo: child.id,
      },
    );
    return item.id;
  } finally {
    await api.dispose();
  }
}

async function json<T = unknown>(
  api: APIRequestContext,
  method: 'GET' | 'POST',
  path: string,
  data?: unknown,
): Promise<T> {
  const response = await api.fetch(path, { method, data });
  if (!response.ok()) {
    throw new Error(`${method} ${path} failed: ${response.status()} ${await response.text()}`);
  }
  return (await response.json()) as T;
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
