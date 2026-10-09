import { expect, test } from './support/auth-fixture';

// In-app help (docs/frontend/analysis/in-app-help.md): the shell's "?" button expands the current
// page's help inline, "All help topics" opens /guardian/help at that topic, and the help follows
// the guardian's saved language. Uses a throwaway guardian because it changes their language.
test('a guardian opens a page’s help, follows it to the help page, and reads it in Danish', async ({
  page,
  loginAs,
  newGuardian,
}) => {
  const guardian = await newGuardian();
  await loginAs(guardian);

  await page.goto('/guardian/sleep-diary');
  const helpButton = page.getByRole('button', { name: 'Help for this page' });
  await expect(helpButton).toHaveAttribute('aria-expanded', 'false');

  await helpButton.click();
  await expect(helpButton).toHaveAttribute('aria-expanded', 'true');
  const panel = page.getByRole('region', { name: 'Help: Sleep diary' });
  await expect(panel.getByRole('heading', { name: 'Log a night' })).toBeVisible();

  // Escape folds it away again and leaves focus on the button.
  await page.keyboard.press('Escape');
  await expect(panel).toBeHidden();
  await expect(helpButton).toBeFocused();

  await helpButton.click();
  await panel.getByRole('link', { name: 'All help topics' }).click();
  await expect(page).toHaveURL(/\/guardian\/help\?topic=sleepDiary$/);
  await expect(page.getByRole('heading', { name: 'How Buddy works' })).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Sleep diary', exact: true })).toBeFocused();

  // The help page has no page help of its own.
  await expect(page.getByRole('button', { name: 'Help for this page' })).toHaveCount(0);

  await page.goto('/guardian/admin');
  const profile = page.locator('app-my-profile');
  await profile.getByLabel('Language').selectOption('da');
  await profile.getByRole('button', { name: 'Save language' }).click();
  // Saving switches the UI to Danish at once.
  await expect(profile.getByText('Sprog opdateret.')).toBeVisible();

  await page.goto('/guardian/sleep-diary');
  await page.getByRole('button', { name: 'Hjælp til denne side' }).click();
  const danishPanel = page.getByRole('region', { name: 'Hjælp: Søvndagbog' });
  await expect(danishPanel.getByRole('heading', { name: 'Registrér en nat' })).toBeVisible();
});
