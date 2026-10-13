import { type APIRequestContext, request } from '@playwright/test';

import { expect, test } from './support/auth-fixture';
import { createCalendar, createChild, createGroup, uniqueName } from './support/guardian-data';
import { getAccessToken } from './support/keycloak-client';
import { makeChildLoginUsable } from './support/keycloak-admin-client';
import { readRuntimeConfig } from './support/runtime-config';
import type { TestUser } from './support/seeded-users';

// A child's packing-list suggestion end to end (docs/backend/analysis/packing-checklists.md,
// Question 8), against the real API, Keycloak and Postgres: the child suggests "Recorder" for
// Friday under today's list, the guardian notices the "Packing suggestions: 1" pill on the
// dashboard, approves it from the task library (changing it to every day), and the child sees
// "Added to your list" and the new item on today's list.
//
// A disposable guardian because createChild is involved. The packing list is built and scheduled
// for today through the API: building one in the editor and scheduling it from the agenda are
// covered by packing-checklist-pack.spec.ts. The child joins the group so the list reaches their
// home page, and signs in by direct grant once makeChildLoginUsable has replaced its temporary
// password.
test('a guardian approves the item a child suggested for their packing list', async ({
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

  const listName = uniqueName('E2E School bag');
  await scheduleChecklistForToday(guardian, calendarName, listName);

  // The child suggests an item under today's list.
  const childLogin = await makeChildLoginUsable(child.username);
  await loginAs(childLogin);
  await expect(page).toHaveURL(/\/child$/);
  const list = page.getByRole('listitem').filter({ hasText: listName }).first();
  await expect(list.getByText('0 of 1 packed')).toBeVisible();
  await list.getByRole('button', { name: 'Suggest something' }).click();
  await list.getByLabel('What do you need to bring?').fill('Recorder');
  await list.getByRole('button', { name: 'Friday' }).click();
  await list.getByRole('button', { name: 'Send to a grown-up' }).click();
  await expect(list.getByText('Recorder · Waiting for a grown-up')).toBeVisible();

  // The guardian notices it on the dashboard and approves it for every day.
  await loginAs(guardian);
  await page.goto('/guardian');
  await page.getByRole('link', { name: 'Packing suggestions: 1' }).click();
  await expect(page).toHaveURL(/\/guardian\/task-library\?child=/);
  const listRow = page.locator('app-manage-tasks li', { hasText: listName }).first();
  // Arriving from the pill opens the list with the waiting suggestion.
  const panel = page.getByTestId('suggestion-panel');
  await expect(panel).toContainText(`Suggested by ${child.givenName}`);
  await expect(panel).toContainText('Recorder');
  await panel.getByRole('button', { name: 'Approve' }).click();
  // Pre-filled from the child's suggestion; the guardian takes Friday off, so it's every day.
  await expect(panel.getByRole('button', { name: 'Friday' })).toHaveAttribute(
    'aria-pressed',
    'true',
  );
  await panel.getByRole('button', { name: 'Friday' }).click();
  await panel.getByRole('button', { name: 'Add to list' }).click();
  await expect(panel).toHaveCount(0);
  await expect(listRow.getByRole('listitem').filter({ hasText: 'Recorder' })).toContainText(
    'Every day',
  );
  await page.reload();
  await expect(page.getByText(/2 items/).first()).toBeVisible();

  // The child sees the outcome and the new item on today's list.
  await loginAs(childLogin);
  await expect(page).toHaveURL(/\/child$/);
  await expect(list.getByText('0 of 2 packed')).toBeVisible();
  await expect(list.getByText('Recorder · Added to your list')).toBeVisible();
});

// A packing list with one every-day item for the guardian's only child, scheduled for today at
// 23:59 on the named calendar and assigned to the child.
async function scheduleChecklistForToday(
  guardian: TestUser,
  calendarName: string,
  name: string,
): Promise<void> {
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
        name,
        icon: '🎒',
        color: '#0ea5e9',
        kind: 'Checklist',
      },
    );
    await json(api, 'POST', `/task-templates/${template.id}/subtasks`, { title: 'Lunchbox' });

    await json(api, 'POST', `/calendars/${calendarId}/items/from-template`, {
      taskTemplateId: template.id,
      startDate: dateIn(timeZoneId),
      startTime: '23:59:00',
      title: name,
      color: '#0ea5e9',
      icon: '🎒',
      recurrence: null,
      assignedTo: child.id,
    });
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

// Today's date (yyyy-MM-dd) on a wall clock in `timeZone`.
function dateIn(timeZone: string): string {
  return new Intl.DateTimeFormat('en-CA', { timeZone }).format(new Date());
}
