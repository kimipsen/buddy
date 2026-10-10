import { expect, test } from './support/auth-fixture';
import { createChild, uniqueName } from './support/guardian-data';
import { makeChildLoginUsable } from './support/keycloak-admin-client';

// The house rules journey end to end (docs/backend/analysis/house-rules.md): a guardian writes a
// rule with a markdown table, the child signs in, sees "1 rule to read" on their home page, reads
// the rendered rule and taps "I've read this", and the guardian then sees that the child has read
// it. Everything runs against the real API, Keycloak and Postgres.
//
// A disposable guardian (newGuardian) because createChild is involved: the page's scope picker
// defaults to the guardian's first scope, which must be this test's child. The rule is the child's
// personal rule, so no group is needed. The child signs in by direct grant once
// makeChildLoginUsable has replaced its temporary password -- the hosted Keycloak form isn't used
// (see child-role-route-guard.spec.ts for why it can't be in this environment).
test('a child reads a new house rule and the guardian sees it was read', async ({
  page,
  loginAs,
  newGuardian,
}) => {
  const guardian = await newGuardian();
  await loginAs(guardian);
  const child = await createChild(page);
  const title = uniqueName('Screen time ');

  await page.goto('/guardian/house-rules');
  await expect(page.getByRole('heading', { name: 'Agree on the rules, together.' })).toBeVisible();

  await page.getByRole('button', { name: 'Add rule' }).click();
  const dialog = page.getByRole('dialog', { name: 'New rule' });
  await dialog.getByLabel('Title').fill(title);
  await dialog
    .getByRole('textbox', { name: 'The rule' })
    .fill('| Day | Time |\n|---|---|\n| Mon | 45 min |\n\nTablet on the charger at **19:30**.');
  await dialog.getByRole('button', { name: 'Save rule' }).click();
  await expect(dialog).toBeHidden();

  // The rule renders as markdown (a real table, bold text), and the child hasn't read it yet.
  const rule = page
    .getByRole('listitem')
    .filter({ has: page.getByRole('heading', { name: title }) });
  await expect(rule.getByRole('columnheader', { name: 'Day' })).toBeVisible();
  await expect(rule.getByRole('cell', { name: '45 min' })).toBeVisible();
  await expect(rule.getByText('19:30')).toBeVisible();
  await expect(rule.getByText(`${child.givenName} hasn’t read it yet`)).toBeVisible();

  // The child's side.
  await loginAs(await makeChildLoginUsable(child.username));
  await expect(page).toHaveURL(/\/child$/);
  await page.getByRole('link', { name: '1 rule to read' }).click();

  await expect(page.getByRole('heading', { name: 'Our rules', level: 1 })).toBeVisible();
  const card = page
    .getByRole('article')
    .filter({ has: page.getByRole('heading', { name: title }) });
  await expect(card.getByText('New', { exact: true })).toBeVisible();
  await expect(card.getByRole('cell', { name: '45 min' })).toBeVisible();
  await card.getByRole('button', { name: 'I’ve read this' }).click();
  await expect(card.getByText('Read', { exact: true })).toBeVisible();

  // Persisted: nothing left to read after a reload.
  await page.reload();
  await expect(card.getByRole('button', { name: 'I’ve read this' })).toBeHidden();
  await page.goto('/child');
  await expect(page.getByRole('link', { name: /rules? to read/ })).toBeHidden();

  // Back to the guardian, who now sees the child's tick.
  await loginAs(guardian);
  await page.goto('/guardian/house-rules');
  await expect(rule.getByText(`${child.givenName} has read it`)).toBeVisible();
});
