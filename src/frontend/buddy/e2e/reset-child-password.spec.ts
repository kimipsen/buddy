import { expect, test } from './support/auth-fixture';
import { createChild } from './support/guardian-data';

test('a guardian resets a child password and is shown the username and a new one-time password', async ({
  page,
  loginAs,
  newGuardian,
}) => {
  await loginAs(await newGuardian());

  const child = await createChild(page, 'E2eReset');
  const section = page.locator('app-manage-children');
  const createdPassword = await section
    .getByText('Temporary password (shown once):')
    .locator('span')
    .innerText();

  const row = section
    .getByRole('listitem')
    .filter({ hasText: `${child.givenName} ${child.familyName}` });
  await row.getByRole('button', { name: 'Reset password' }).click();
  await expect(
    row.getByText(`Give ${child.givenName} a new password? The current one stops working`),
  ).toBeVisible();
  await row.getByRole('button', { name: 'Reset password' }).click();

  const status = section.getByRole('status');
  await expect(status).toContainText(`${child.givenName} has a new password.`, {
    timeout: 15_000,
  });
  await expect(status).toContainText(`Username: ${child.username}`);

  const resetPassword = await status
    .getByText('Temporary password (shown once):')
    .locator('span')
    .innerText();
  expect(resetPassword).not.toBe('');
  expect(resetPassword).not.toBe(createdPassword);
});
