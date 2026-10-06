import { expect, test } from './support/auth-fixture';
import { createChild } from './support/guardian-data';

// Importing an older meal plan from a pasted note (MealplanImport / mealplan-import.ts) against
// the real backend: PreviewMealPlanImport parses and matches without writing, the review decides
// per distinct meal name, CommitMealPlanImport writes one MealPlanEntriesImported, and
// RevertMealPlanImport undoes it. Nothing leaves the machine, so nothing is stubbed.
test('guardian previews a pasted note, merges a spelling variant, imports it, and undoes the import', async ({
  page,
  loginAs,
  newGuardian,
}) => {
  // A disposable guardian: the import writes into the family-wide meal library and plan, which
  // for a seeded guardian is shared with every parallel test's children.
  await loginAs(await newGuardian());
  await createChild(page);

  await page.goto('/guardian/mealplan');
  await page.getByRole('link', { name: 'Import older plans' }).click();
  await expect(
    page.getByRole('heading', { name: 'Bring in the plans you kept somewhere else.' }),
  ).toBeVisible();
  await expect(page.getByText('Nothing has been imported yet.')).toBeVisible();

  // ISO week 3 of 2024: Sunday 14 January (the note lists Sunday first) to Wednesday the 17th.
  await page
    .getByLabel('Meal plan text')
    .fill('Madplan 2024\nU3\nSø: Hotdogs 🌭\nMa: Hotdogs\nTi: Hotdog\nOn: rester\nTo: - Sommerhus');
  await page.getByRole('button', { name: 'Preview import' }).click();

  await expect(page.getByRole('heading', { name: 'Check before importing' })).toBeVisible();
  await expect(page.getByText('5 days from 2024-01-14 to 2024-01-18.')).toBeVisible();
  await expect(page.getByText('Did you mean “Hotdogs”?')).toBeVisible();

  // Leftovers and the away day are skipped by default, so 3 days remain; merging "Hotdog" into
  // "Hotdogs" leaves a single new meal.
  await page
    .getByLabel('What to do with Hotdog', { exact: true })
    .selectOption({ label: 'Same as “Hotdogs”' });
  await expect(page.getByTestId('count-new-meals')).toHaveText('1');

  await page.getByRole('button', { name: 'Import 3 days' }).click();
  await expect(page.getByRole('heading', { name: 'Import complete' })).toBeVisible();
  await expect(page.getByText('3 days added to the meal plan.')).toBeVisible();
  await expect(page.getByText('Meals created: 1.')).toBeVisible();

  await page.reload();
  const row = page.locator('li', { hasText: '2024-01-14 – 2024-01-16 · 3 days' });
  await expect(row).toBeVisible();

  await row.getByRole('button', { name: 'Undo', exact: true }).click();
  await row.getByRole('button', { name: 'Undo import' }).click();
  await expect(row.getByText('Undone')).toBeVisible();

  await page.reload();
  await expect(
    page.locator('li', { hasText: '2024-01-14 – 2024-01-16 · 3 days' }).getByText('Undone'),
  ).toBeVisible();
});
