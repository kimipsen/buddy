import { expect, test } from './support/auth-fixture';
import { createChild } from './support/guardian-data';
import { makeChildLoginUsable } from './support/keycloak-admin-client';

// The First – Then board end to end (docs/backend/analysis/first-then-board.md), against the real
// API, Keycloak and Postgres: a guardian sets "Homework → iPad" for a child; the child sees it on
// /child, opens it and taps Done, which unlocks the Then; the guardian sees it unlocked after a
// reload and ends the board; the child's page then says there is no board.
//
// A disposable guardian because createChild is involved. The child signs in by direct grant once
// makeChildLoginUsable has replaced its temporary password (as in feelings-check-in.spec.ts).
test('a guardian sets a board, the child unlocks the Then, the guardian ends it', async ({
  page,
  loginAs,
  newGuardian,
}) => {
  const guardian = await newGuardian();
  await loginAs(guardian);
  const child = await createChild(page);

  // The guardian sets the board from the starter suggestions.
  await page.goto('/guardian/first-then');
  await expect(page.getByRole('heading', { name: 'First – Then', level: 2 })).toBeVisible();
  await expect(page.getByTestId('guardian-first-then-none')).toBeVisible();
  await page.getByRole('button', { name: 'Homework' }).click();
  await page.getByRole('button', { name: 'iPad' }).click();
  await page.getByLabel('How long?').selectOption({ label: '20 min' });
  await page.getByRole('button', { name: `Show to ${child.givenName}` }).click();
  await expect(page.getByTestId('guardian-first-then-state')).toContainText('Waiting for Homework');

  // The child sees it at the top of /child and opens it.
  const childLogin = await makeChildLoginUsable(child.username);
  await loginAs(childLogin);
  await expect(page).toHaveURL(/\/child$/);
  const card = page.getByTestId('first-then-card');
  await expect(card).toContainText('First Homework, then iPad');
  await card.click();
  await expect(page).toHaveURL(/\/child\/first-then$/);

  // Done unlocks the Then.
  await expect(page.getByTestId('first-then-then')).toContainText('After Homework');
  await page.getByRole('button', { name: 'Done!' }).click();
  await expect(page.getByTestId('first-then-then')).toContainText('Now: iPad!');
  await expect(page.getByRole('timer')).toBeVisible();

  // The guardian sees it unlocked and ends it.
  await loginAs(guardian);
  await page.goto('/guardian/first-then');
  await expect(page.getByTestId('guardian-first-then-state')).toContainText(
    'Unlocked: time for iPad',
  );
  await page.getByRole('button', { name: 'End board' }).click();
  await expect(page.getByTestId('guardian-first-then-none')).toBeVisible();

  // The child's page has nothing left to show.
  await loginAs(childLogin);
  await page.goto('/child/first-then');
  await expect(page.getByText('No board right now')).toBeVisible();
});
