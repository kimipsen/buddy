import { expect, test } from './support/auth-fixture';
import { createChild } from './support/guardian-data';
import { makeChildLoginUsable } from './support/keycloak-admin-client';

// The child's own timer and timer settings end to end
// (docs/frontend/analysis/child-timer-preferences.md), against the real API, Keycloak and Postgres:
// the child opens My timer from the menu, picks a look that is saved on their account (it survives
// a reload, and the guardian sees it on /guardian/admin), starts a 2-minute timer that survives a
// reload on this device, and a guardian muting the sound hides the sound choices.
//
// A disposable guardian because createChild is involved; the child signs in by direct grant once
// makeChildLoginUsable has replaced its temporary password. No fake browser clock: one ahead of
// real time would expire the session, so the timer isn't run to time's up here (the unit specs of
// ChildTimerPage and the chime cover that).
test('a child picks their own timer look and sound, and starts a timer of their own', async ({
  page,
  loginAs,
  newGuardian,
}) => {
  const guardian = await newGuardian();
  await loginAs(guardian);
  const child = await createChild(page);
  const childLabel = `${child.givenName} ${child.familyName}`;

  const childLogin = await makeChildLoginUsable(child.username);
  await loginAs(childLogin);
  await expect(page).toHaveURL(/\/child$/);

  await page.getByRole('button', { name: 'Open menu' }).click();
  await page.getByRole('link', { name: 'My timer' }).click();
  await expect(page).toHaveURL(/\/child\/timer$/);
  await expect(page.getByRole('heading', { name: 'How my timer looks' })).toBeVisible();

  // Each choice is saved straight away, on the child's account.
  const saved = () =>
    page.waitForResponse(
      (res) =>
        res.request().method() === 'PATCH' && res.url().endsWith('/users/me/timer-preferences'),
    );
  await Promise.all([saved(), page.getByRole('radio', { name: 'Bar' }).click()]);
  await Promise.all([saved(), page.locator('label', { hasText: 'Purple' }).click()]);
  await Promise.all([saved(), page.getByRole('switch', { name: 'Show the minutes' }).click()]);
  await Promise.all([saved(), page.getByRole('radio', { name: 'Gong' }).click()]);

  await page.reload();
  await expect(page.getByRole('radio', { name: 'Bar' })).toHaveAttribute('aria-checked', 'true');
  await expect(page.getByRole('radio', { name: 'Purple' })).toBeChecked();
  await expect(page.getByRole('switch', { name: 'Show the minutes' })).toHaveAttribute(
    'aria-checked',
    'false',
  );
  await expect(page.getByRole('radio', { name: 'Gong' })).toHaveAttribute('aria-checked', 'true');

  // A timer of my own: drawn as the bar the child picked, with the minutes only for a screen
  // reader, and still running after a reload.
  await page.getByRole('button', { name: 'Start a timer for 2 min' }).click();
  const ownTimer = page
    .getByTestId('own-timer')
    .getByRole('timer', { name: /^(2 min|1 min) left$/ });
  await expect(ownTimer).toBeVisible();
  await expect(page.getByTestId('own-timer').getByTestId('timer-bar')).toBeVisible();
  await expect(page.getByTestId('own-timer')).not.toContainText('min left');
  await page.reload();
  await expect(ownTimer).toBeVisible();
  await page.getByRole('button', { name: 'Pause' }).click();
  await expect(page.getByTestId('own-timer').getByRole('timer', { name: 'Paused' })).toBeVisible();
  await page.getByRole('button', { name: 'Stop timer' }).click();
  await expect(page.getByRole('button', { name: 'Start a timer for 2 min' })).toBeVisible();

  // The guardian sees the child's choices and turns the sound off.
  await loginAs(guardian);
  await page.goto('/guardian/admin');
  const childRow = page
    .locator('app-manage-children')
    .getByRole('listitem')
    .filter({ hasText: childLabel })
    .first();
  await expect(childRow.getByTestId('timer-choices')).toContainText(
    'Chosen by the child: Bar · Large · Purple · minutes hidden · Gong',
  );
  const soundSwitch = childRow.getByRole('switch', { name: 'Allow timer sound' });
  await expect(soundSwitch).toHaveAttribute('aria-checked', 'true');
  await soundSwitch.click();
  await expect(soundSwitch).toHaveAttribute('aria-checked', 'false');

  // The child's page now says so, and the sound choices are gone.
  await loginAs(childLogin);
  await page.goto('/child/timer');
  await expect(page.getByText('A grown-up has turned the sound off.')).toBeVisible();
  await expect(page.getByRole('button', { name: /Listen/ })).toHaveCount(0);
  await expect(page.getByRole('radio', { name: 'Purple' })).toBeChecked();
});
