import { expect, test } from './support/auth-fixture';
import { createChild } from './support/guardian-data';
import { makeChildLoginUsable } from './support/keycloak-admin-client';

// The homework splitter end to end (docs/frontend/analysis/homework-splitter.md), against the real
// API, Keycloak and Postgres: a guardian sets a child's focus and break lengths on /guardian/admin,
// and the child plans two pieces of homework, starts, finishes a part early, takes and ends a
// break, survives a reload, and reaches "All done". The session itself lives in the browser.
//
// A disposable guardian because createChild is involved. The child signs in by direct grant once
// makeChildLoginUsable has replaced its temporary password.
test('a child splits homework into the chunks a guardian chose and works through it', async ({
  page,
  loginAs,
  newGuardian,
}) => {
  const guardian = await newGuardian();
  await loginAs(guardian);
  const child = await createChild(page);
  const childLabel = `${child.givenName} ${child.familyName}`;

  // The guardian picks 10-minute chunks with 2-minute breaks.
  await page.goto('/guardian/admin');
  const lengths = page
    .locator('app-manage-children')
    .getByRole('listitem')
    .filter({ hasText: childLabel })
    .getByTestId('homework-lengths');
  for (const [label, value] of [
    ['Homework focus', '10 min'],
    ['Break', '2 min'],
  ]) {
    const [response] = await Promise.all([
      page.waitForResponse(
        (res) => res.request().method() === 'PATCH' && res.url().endsWith('/homework-chunks'),
      ),
      lengths.getByLabel(label).selectOption({ label: value }),
    ]);
    expect(response.ok()).toBe(true);
  }

  // The child plans Maths (20 min) and Reading (10 min): 3 chunks and 2 breaks.
  await loginAs(await makeChildLoginUsable(child.username));
  await expect(page).toHaveURL(/\/child$/);
  await page.getByRole('link', { name: 'Homework' }).click();
  await expect(page).toHaveURL(/\/child\/homework$/);
  await expect(page.getByText('Focus 10 min · Break 2 min')).toBeVisible();

  await page.getByLabel('What is it?').fill('Maths');
  await page.getByLabel('About how long?').selectOption({ label: '20 min' });
  await page.getByRole('button', { name: 'Add more homework' }).click();
  await page.getByLabel('What is it?').nth(1).fill('Reading');
  await page.getByLabel('About how long?').nth(1).selectOption({ label: '10 min' });
  await expect(page.getByText('Focus chunks: 3 · Breaks: 2 · About 34 min')).toBeVisible();
  await page.getByRole('button', { name: 'Start', exact: true }).click();

  await expect(page.getByRole('heading', { name: 'Maths: 1 of 2' })).toBeVisible();
  await expect(page.getByRole('timer', { name: '10 min left' })).toBeVisible();

  // The session survives a reload.
  await page.reload();
  await expect(page.getByRole('heading', { name: 'Maths: 1 of 2' })).toBeVisible();

  // Maths is done early: its second chunk goes, and a break comes next.
  await page.getByRole('button', { name: 'Done with Maths' }).click();
  await expect(page.getByRole('heading', { name: 'Break' })).toBeVisible();
  // Regex text matching sees the template's surrounding whitespace, so no ^ anchor. The capital
  // after the colon proves the idea was translated, not shown as its homework.breakIdeas.* key.
  await expect(page.getByTestId('break-idea')).toHaveText(/Break idea: [A-Z]/);
  await expect(page.getByText('Next: Reading: 1 of 1')).toBeVisible();

  await page.getByRole('button', { name: 'Back to work' }).click();
  await expect(page.getByRole('heading', { name: 'Reading: 1 of 1' })).toBeVisible();
  await expect(page.getByText('This is the last one!')).toBeVisible();

  await page.getByRole('button', { name: "I'm done" }).click();
  await expect(page.getByText('All done! Great work.')).toBeVisible();

  // Back home, nothing is left in progress.
  await page.getByRole('link', { name: 'Back home' }).first().click();
  await expect(page).toHaveURL(/\/child$/);
  await expect(page.getByTestId('homework-in-progress')).toHaveCount(0);
});
