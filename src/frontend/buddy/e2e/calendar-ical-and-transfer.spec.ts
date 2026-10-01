import { SEEDED_USERS, expect, test } from './support/auth-fixture';
import { createCalendar, createGroup } from './support/guardian-data';

// Covers two ManageCalendars flows beyond plain creation: generating/revoking an iCal
// subscription link (GetIcalFeedEndpoint, AllowAnonymous -- the token itself is the secret), and
// "moving" a calendar to a different group (CalendarsService.transferToGroup). That move *is* a
// real, separate UI action here ("Move to group"/ManageCalendars.confirmMove) distinct from
// creating a calendar under a group directly -- there's no separate "personal calendar" to
// transfer *from* (CreateCalendarRequest.groupId is always required), but transferring an
// *already-existing group-owned* calendar to a *different* group it's own real flow, so this spec
// exercises it as the closest equivalent to "transfer a calendar to a group".
test('guardian generates and revokes an iCal subscription link, then moves the calendar to another group', async ({
  page,
  loginAs,
}) => {
  await loginAs(SEEDED_USERS.alice);

  const groupAName = await createGroup(page, 'E2eGroupA');
  const calendarName = await createCalendar(page, groupAName);

  const section = page.locator('app-manage-calendars');
  const calendarRow = section.locator('li', { hasText: calendarName });

  await calendarRow.getByRole('button', { name: 'Subscribe' }).click();

  const [createResponse] = await Promise.all([
    page.waitForResponse(
      (res) => res.request().method() === 'POST' && res.url().includes('/ical-tokens'),
    ),
    calendarRow.getByRole('button', { name: 'Generate new link' }).click(),
  ]);
  expect(createResponse.ok()).toBe(true);

  const feedUrl = (await calendarRow.locator('span.font-mono').textContent())?.trim();
  expect(feedUrl).toBeTruthy();

  const feedResponse = await page.request.get(feedUrl!);
  expect(feedResponse.ok()).toBe(true);
  expect(feedResponse.headers()['content-type']).toContain('text/calendar');
  expect(await feedResponse.text()).toContain('BEGIN:VCALENDAR');

  // Revoke it, then confirm the same URL no longer resolves.
  const [revokeResponse] = await Promise.all([
    page.waitForResponse(
      (res) => res.request().method() === 'DELETE' && res.url().includes('/ical-tokens/'),
    ),
    calendarRow.getByRole('button', { name: 'Revoke' }).click(),
  ]);
  expect(revokeResponse.ok()).toBe(true);
  await expect(calendarRow.getByText('No subscription links yet.', { exact: true })).toBeVisible();

  const revokedFeedResponse = await page.request.get(feedUrl!);
  expect(revokedFeedResponse.status()).toBe(404);

  // Move ("transfer") the calendar to a second group.
  const groupBName = await createGroup(page, 'E2eGroupB');

  // ManageCalendars.loadManageableGroups only ever runs from ngOnInit (see manage-calendars.ts's
  // own comment on createCalendar/createGroup above) -- createGroup's own page.goto('/guardian/admin')
  // reloaded the page and re-fetched that dropdown *before* group B existed yet, so it still
  // wouldn't be offered as a move target without reloading again now that it does.
  await page.reload();

  const calendarRowAfterNav = section.locator('li', { hasText: calendarName });
  await calendarRowAfterNav.getByRole('button', { name: 'Move to group' }).click();
  await calendarRowAfterNav.getByLabel('Choose a group').selectOption({ label: groupBName });

  const [moveResponse] = await Promise.all([
    page.waitForResponse(
      (res) =>
        res.request().method() === 'PUT' && /\/calendars\/[^/]+\/group\/[^/]+$/.test(res.url()),
    ),
    calendarRowAfterNav.getByRole('button', { name: 'Move', exact: true }).click(),
  ]);
  expect(moveResponse.ok()).toBe(true);

  await page.reload();

  // ManageCalendars has no per-group calendar list to check "this calendar is now under group B"
  // against directly (listMyCalendars() is a flat, ungrouped list for the caller) -- the PUT
  // response above is the authoritative confirmation the transfer succeeded server-side. This
  // just confirms the calendar itself is still intact and alice (who manages both groups) still
  // owns it afterward, rather than the move having silently broken access to it.
  const calendarRowAfterMove = section.locator('li', { hasText: calendarName });
  await expect(calendarRowAfterMove).toBeVisible();
  await expect(calendarRowAfterMove.getByRole('button', { name: 'Move to group' })).toBeVisible();
});
