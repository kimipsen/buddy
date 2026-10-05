import { expect, test } from './support/auth-fixture';
import { createChild } from './support/guardian-data';

// The whole sleep diary journey against the real API: a guardian logs a night, sees it in the
// history (and after a reload), then hands a share link to someone with no Buddy account, who reads
// the diary with no login until the guardian revokes the link. A disposable guardian, because the
// page defaults to the guardian's first child (see newGuardian in auth-fixture.ts).
test('guardian logs a night, shares the diary with a no-login reader, then revokes the link', async ({
  page,
  browser,
  loginAs,
  newGuardian,
}) => {
  await loginAs(await newGuardian());
  const child = await createChild(page);

  await page.goto('/guardian/sleep-diary');
  const form = page.locator('app-sleep-entry-form');
  await expect(form.getByRole('heading', { name: 'Log a night' })).toBeVisible();

  // The form opens on tonight.
  await form.getByLabel('Lies down to sleep').fill('20:15');
  await form.getByLabel('Falls asleep').fill('20:45');
  await form.getByLabel('Wakes up in the morning').fill('06:30');
  await form.getByRole('button', { name: '+ Add wake-up' }).click();
  await form.getByLabel('Woke up at').fill('03:30');
  await form.getByLabel('Minutes', { exact: true }).fill('30');
  await form.getByRole('switch', { name: 'Seemed tired during the day' }).click();

  // The total is suggested from the times: 20:45 -> 06:30 is 9 h 45 min, minus the 30 min awake.
  await expect(form.getByLabel('Hours slept')).toHaveValue('9');
  await expect(form.getByLabel('Minutes slept')).toHaveValue('15');

  await form.getByRole('button', { name: 'Save night' }).click();
  await expect(form.getByRole('status')).toHaveText('Saved.');

  // History rows are newest first, so tonight is the first row (no accessible row name exists
  // beyond the date label, which is locale-formatted).
  const tonight = page.locator('app-sleep-history tbody tr').first();
  await expect(tonight).toContainText('9 h 15 min');

  await page.reload();
  await expect(page.locator('app-sleep-history tbody tr').first()).toContainText('9 h 15 min');

  const share = page.locator('app-sleep-share-links');
  await share.getByRole('button', { name: 'Create link' }).click();
  // The plaintext link is only shown once, in the confirmation box's monospace span.
  const linkText = share.locator('.font-mono');
  await expect(linkText).toContainText('/shared/sleep-diary/');
  const shareUrl = (await linkText.textContent())!.trim();

  // A brand-new browser context: no session storage, so no token -- a clinician's view.
  const readerContext = await browser.newContext();
  const reader = await readerContext.newPage();
  await reader.goto(shareUrl);
  await expect(
    reader.getByRole('heading', { name: `Sleep diary for ${child.givenName} Testson` }),
  ).toBeVisible();
  await expect(reader.locator('tbody')).toContainText('9 h 15 min');

  await share.getByRole('button', { name: 'Revoke' }).click();
  await expect(share.getByText('No active links.')).toBeVisible();

  await reader.reload();
  await expect(
    reader.getByText('This link is invalid, has been revoked or has expired.'),
  ).toBeVisible();
  await readerContext.close();
});
