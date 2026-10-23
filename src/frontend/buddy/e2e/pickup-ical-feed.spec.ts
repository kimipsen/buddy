import { DISPOSABLE_GUARDIAN_GIVEN_NAME, expect, test } from './support/auth-fixture';
import { createChild } from './support/guardian-data';

test('guardian subscribes to their own pickups, sees their slot in the feed, then revokes the link', async ({
  page,
  loginAs,
  newGuardian,
}) => {
  await loginAs(await newGuardian());
  const child = await createChild(page);

  // Assign today's drop-off to this guardian (see pickup-assignment.spec.ts for the grid layout).
  await page.goto('/guardian/pickup');
  await expect(page.getByRole('heading', { name: 'Weekly schedule' })).toBeVisible();
  const dropOffCell = page.locator('tbody tr').first().locator('td').first();
  await dropOffCell.getByRole('button', { name: 'Not planned' }).click();
  await page
    .getByLabel('Choose a guardian')
    .selectOption({ label: DISPOSABLE_GUARDIAN_GIVEN_NAME });
  await page.getByRole('button', { name: 'Save' }).click();
  await expect(
    dropOffCell.getByRole('button', { name: DISPOSABLE_GUARDIAN_GIVEN_NAME, exact: true }),
  ).toBeVisible();

  const card = page.locator('app-pickup-ical');
  await expect(card.getByRole('heading', { name: 'My pickups in my calendar' })).toBeVisible();
  await expect(card.getByText('No subscription links yet.', { exact: true })).toBeVisible();

  const [createResponse] = await Promise.all([
    page.waitForResponse(
      (res) => res.request().method() === 'POST' && res.url().endsWith('/pickups/me/ical-tokens'),
    ),
    card.getByRole('button', { name: 'Create subscription link' }).click(),
  ]);
  expect(createResponse.ok()).toBe(true);

  const feedUrl = (await card.locator('span.font-mono').textContent())?.trim();
  expect(feedUrl).toBeTruthy();
  await expect(card.getByRole('link', { name: 'Add to Google Calendar' })).toBeVisible();

  const feedResponse = await page.request.get(feedUrl!);
  expect(feedResponse.ok()).toBe(true);
  expect(feedResponse.headers()['content-type']).toContain('text/calendar');
  expect(await feedResponse.text()).toContain(`SUMMARY:Drop off ${child.givenName}`);

  const [revokeResponse] = await Promise.all([
    page.waitForResponse(
      (res) =>
        res.request().method() === 'DELETE' && res.url().includes('/pickups/me/ical-tokens/'),
    ),
    card.getByRole('button', { name: 'Revoke' }).click(),
  ]);
  expect(revokeResponse.ok()).toBe(true);
  await expect(card.getByText('No subscription links yet.', { exact: true })).toBeVisible();

  expect((await page.request.get(feedUrl!)).status()).toBe(404);
});
