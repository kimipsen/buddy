import { DISPOSABLE_GUARDIAN_GIVEN_NAME, expect, test } from './support/auth-fixture';
import { createChild } from './support/guardian-data';

// The night duty iCal feed end to end against the real API
// (docs/backend/analysis/night-duty-ical-feed.md): plan a screen night, create a subscription
// link for the child on the night duty page, read the anonymous feed, then revoke the link.
//
// A disposable guardian whose only child is this test's own, so the card shows that child and its
// links without a child picker.

const WEEKDAYS = ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday'];

test('guardian subscribes to a child’s nights, sees the planned night in the feed, then revokes the link', async ({
  page,
  loginAs,
  newGuardian,
}) => {
  await loginAs(await newGuardian());
  const child = await createChild(page);

  await page.goto('/guardian/night-duty');
  await expect(page.getByRole('heading', { name: "Who's on duty at night." })).toBeVisible();

  // Tomorrow is the guardian's screen night in a one-week schedule that starts today.
  const pattern = page.locator('app-night-pattern-editor');
  const tomorrow = WEEKDAYS[(new Date().getDay() + 1) % 7]!;
  await pattern
    .getByLabel(`Week A, ${tomorrow}: who`)
    .selectOption({ label: `👤 ${DISPOSABLE_GUARDIAN_GIVEN_NAME}` });
  await pattern.getByLabel(`Week A, ${tomorrow}: note`).fill('Screen night');
  await pattern.getByRole('button', { name: 'Save schedule' }).click();
  await expect(pattern.getByText('Schedule saved.')).toBeVisible();

  const card = page.locator('app-night-ical');
  await expect(
    card.getByRole('heading', { name: `${child.givenName}'s nights in my calendar` }),
  ).toBeVisible();
  await expect(card.getByText('No subscription links yet.', { exact: true })).toBeVisible();

  const [createResponse] = await Promise.all([
    page.waitForResponse(
      (res) => res.request().method() === 'POST' && res.url().endsWith('/ical-tokens'),
    ),
    card.getByRole('button', { name: 'Create subscription link' }).click(),
  ]);
  expect(createResponse.ok()).toBe(true);

  const feedUrl = (await card.locator('span.font-mono').textContent())?.trim();
  expect(feedUrl).toBeTruthy();
  await expect(card.getByRole('link', { name: 'Add to Google Calendar' })).toBeVisible();
  await expect(card.getByRole('button', { name: 'Revoke' })).toHaveCount(1);

  const feedResponse = await page.request.get(feedUrl!);
  expect(feedResponse.ok()).toBe(true);
  expect(feedResponse.headers()['content-type']).toContain('text/calendar');
  const ics = await feedResponse.text();
  expect(ics).toContain(`X-WR-CALNAME:Night duty: ${child.givenName}`);
  expect(ics).toContain(`SUMMARY:Night: ${DISPOSABLE_GUARDIAN_GIVEN_NAME} · Screen night`);

  const [revokeResponse] = await Promise.all([
    page.waitForResponse(
      (res) => res.request().method() === 'DELETE' && res.url().includes('/ical-tokens/'),
    ),
    card.getByRole('button', { name: 'Revoke' }).click(),
  ]);
  expect(revokeResponse.ok()).toBe(true);
  await expect(card.getByText('No subscription links yet.', { exact: true })).toBeVisible();

  expect((await page.request.get(feedUrl!)).status()).toBe(404);
});
