import { expect, test } from './support/auth-fixture';
import { createChild } from './support/guardian-data';
import { makeChildLoginUsable } from './support/keycloak-admin-client';

// Keeping the screen on while a child's timer runs (docs/frontend/analysis/keep-screen-awake.md),
// against the real API, Keycloak and Postgres: a guardian sets a First – Then board with a 20-minute
// Then; the child taps Done, the Then starts counting down, and the page asks for a screen wake
// lock. Before that, with nothing counting down, it asks for none.
//
// The First – Then board is the quickest way to a running timer: a routine on /child would have to
// be scheduled at the current minute. Headless Chromium may refuse a real wake lock, so
// navigator.wakeLock is replaced with a recorder before the app loads. A disposable guardian
// because createChild is involved; the child signs in as in first-then.spec.ts.
test('a running First – Then timer keeps the child’s screen on', async ({
  page,
  loginAs,
  newGuardian,
}) => {
  await page.addInitScript(() => {
    const calls: string[] = [];
    (window as unknown as { wakeLockCalls: string[] }).wakeLockCalls = calls;
    Object.defineProperty(navigator, 'wakeLock', {
      configurable: true,
      value: {
        request: async (type: string) => {
          calls.push(`request:${type}`);
          return {
            released: false,
            release: async () => {
              calls.push('release');
            },
          };
        },
      },
    });
  });
  const wakeLockCalls = () =>
    page.evaluate(() => (window as unknown as { wakeLockCalls: string[] }).wakeLockCalls);

  const guardian = await newGuardian();
  await loginAs(guardian);
  const child = await createChild(page);

  await page.goto('/guardian/first-then');
  await expect(page.getByTestId('guardian-first-then-none')).toBeVisible();
  await page.getByRole('button', { name: 'Homework' }).click();
  await page.getByRole('button', { name: 'iPad' }).click();
  await page.getByLabel('How long?').selectOption({ label: '20 min' });
  await page.getByRole('button', { name: `Show to ${child.givenName}` }).click();
  await expect(page.getByTestId('guardian-first-then-state')).toContainText('Waiting for Homework');

  await loginAs(await makeChildLoginUsable(child.username));
  await page.goto('/child/first-then');
  await expect(page.getByTestId('first-then-then')).toContainText('After Homework');
  expect(await wakeLockCalls()).toEqual([]);

  await page.getByRole('button', { name: 'Done!' }).click();
  await expect(page.getByRole('timer')).toBeVisible();
  await expect.poll(wakeLockCalls).toEqual(['request:screen']);
});
