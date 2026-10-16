import { expect, test } from './support/auth-fixture';
import { createChild } from './support/guardian-data';

// The doctor visit summary against the real API: a guardian logs a night in the sleep diary,
// prepares a summary of the last 4 weeks with a question for the doctor, sees the night in the
// report, and hands a link to a reader with no Buddy account, who sees the same report until the
// guardian deletes the summary. A disposable guardian, because both pages default to the guardian's
// first child (see newGuardian in auth-fixture.ts).
test('guardian prepares a visit summary, shares it with a no-login reader, then deletes it', async ({
  page,
  browser,
  loginAs,
  newGuardian,
}) => {
  await loginAs(await newGuardian());
  const child = await createChild(page);

  // One night, so the report has data: tonight, 20:45 -> 06:30, 9 h 45 min.
  await page.goto('/guardian/sleep-diary');
  const night = page.locator('app-sleep-entry-form');
  await expect(night.getByRole('heading', { name: 'Log a night' })).toBeVisible();
  await night.getByLabel('Lies down to sleep').fill('20:15');
  await night.getByLabel('Falls asleep').fill('20:45');
  await night.getByLabel('Wakes up in the morning').fill('06:30');
  await night.getByRole('button', { name: 'Save night' }).click();
  await expect(night.getByRole('status')).toHaveText('Saved.');

  await page.goto('/guardian/visit-summaries');
  await expect(page.getByRole('heading', { name: 'Prepare for a doctor’s visit.' })).toBeVisible();
  await expect(page.getByText('No visit summaries yet.')).toBeVisible();

  await page.getByRole('button', { name: 'New summary' }).click();
  const editor = page.locator('app-visit-summary-editor');
  await editor.getByLabel('Title').fill('Follow-up with Dr. Lund');
  await editor
    .getByLabel('Questions and notes for the doctor')
    .fill('Is the afternoon crash the medicine?');
  await editor.getByRole('button', { name: 'Save summary' }).click();

  // Saving opens the summary on its report.
  const report = page.locator('app-visit-summary-report');
  await expect(
    report.getByRole('heading', {
      name: `${child.givenName} Testson: Follow-up with Dr. Lund`,
    }),
  ).toBeVisible();
  await expect(report).toContainText('Is the afternoon crash the medicine?');
  await expect(report).toContainText('1 of 28 nights logged');
  await expect(report).toContainText('30 min');

  await page.reload();
  await expect(report).toContainText('1 of 28 nights logged');

  const share = page.locator('app-visit-summary-share-links');
  await share.getByRole('button', { name: 'Create link' }).click();
  // The plaintext link is only shown once, in the confirmation box's monospace span.
  const linkText = share.locator('.font-mono');
  await expect(linkText).toContainText('/shared/visit-summary/');
  const shareUrl = (await linkText.textContent())!.trim();

  // A brand-new browser context: no session storage, so no token -- the doctor's view.
  const readerContext = await browser.newContext();
  const reader = await readerContext.newPage();
  await reader.goto(shareUrl);
  await expect(
    reader.getByRole('heading', { name: `${child.givenName} Testson: Follow-up with Dr. Lund` }),
  ).toBeVisible();
  await expect(reader.locator('app-visit-summary-report')).toContainText('1 of 28 nights logged');

  await page.getByRole('button', { name: 'Delete summary' }).click();
  await page.getByRole('button', { name: 'Delete', exact: true }).click();
  await expect(page.getByText('No visit summaries yet.')).toBeVisible();

  await reader.reload();
  await expect(reader.getByText('This link doesn’t work any more')).toBeVisible();
  await readerContext.close();
});
