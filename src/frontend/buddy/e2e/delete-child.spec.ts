import { expect, test } from './support/auth-fixture';
import { createChild } from './support/guardian-data';

test('a guardian deletes a child only they guard, and it disappears from their children', async ({
  page,
  loginAs,
  newGuardian,
}) => {
  // A disposable guardian whose only child is this test's own (see newGuardian in auth-fixture.ts).
  await loginAs(await newGuardian());

  const child = await createChild(page, 'E2eDeleted');
  const fullName = `${child.givenName} ${child.familyName}`;

  const section = page.locator('app-manage-children');
  const row = section.getByRole('listitem').filter({ hasText: fullName });
  await row.getByRole('button', { name: 'Delete' }).click();

  await expect(row.getByText(`Delete ${fullName} and all their data?`)).toBeVisible();
  await row.getByRole('button', { name: 'Delete' }).click();

  await expect(section.getByText(fullName)).toHaveCount(0, { timeout: 15_000 });
});
