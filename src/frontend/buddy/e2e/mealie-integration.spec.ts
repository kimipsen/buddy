import { expect, test } from './support/auth-fixture';
import { createChild, uniqueName } from './support/guardian-data';
import {
  MEALIE_URL,
  addPlanEntry,
  createMealieFamily,
  createRecipe,
  createShoppingList,
  shoppingListItems,
} from './support/mealie-client';

// The Mealie integration end to end (docs/backend/analysis/mealie-integration.md) against a real
// Mealie: ConnectMealie's live check, PreviewMealieImport + CommitMealPlanImport creating a meal
// already linked to its recipe, GetMealieRecipe in the recipe panel, and
// AddMealPlanToMealieShoppingList filling a real Mealie list. Nothing is stubbed: Mealie runs
// locally (MEALIE_URL), so nothing leaves the machine.

function isoDate(date: Date): string {
  const y = date.getFullYear();
  const m = String(date.getMonth() + 1).padStart(2, '0');
  const d = String(date.getDate()).padStart(2, '0');
  return `${y}-${m}-${d}`;
}

test('guardian connects Mealie, imports next week’s plan with its recipe, and fills a Mealie shopping list', async ({
  page,
  loginAs,
  newGuardian,
}) => {
  // In Mealie: a household of its own, a recipe, a dinner a week from today (always inside both
  // the import's default "next week" and the planner's "Next week") and an empty shopping list.
  const name = uniqueName('E2E Mealie');
  const family = await createMealieFamily(name);
  const recipeName = uniqueName('E2E Lasagne');
  const recipe = await createRecipe(family, recipeName, ['500 g minced beef']);
  const inAWeek = new Date();
  inAWeek.setDate(inAWeek.getDate() + 7);
  await addPlanEntry(family, isoDate(inAWeek), 'dinner', recipe.id);
  const listName = uniqueName('E2E shop');
  const listId = await createShoppingList(family, listName);

  // A disposable guardian: the connection, the meal library and the plan are family-wide.
  await loginAs(await newGuardian());
  await createChild(page);

  await page.goto('/guardian/admin');
  const card = page.locator('app-mealie-settings');
  await card.getByLabel('Mealie address').fill(MEALIE_URL);
  await card.getByLabel('API token').fill(family.apiToken);
  await card.getByRole('button', { name: 'Connect', exact: true }).click();
  await expect(card.getByText(`${family.household} · connected as`)).toBeVisible();

  await card.getByRole('button', { name: 'Test connection' }).click();
  await expect(card.getByText(/Connection works · \d+ recipes, 1 shopping lists/)).toBeVisible();

  // The planner offers the Mealie import once connected; it opens on the Mealie source.
  await page.goto('/guardian/mealplan');
  await page.getByRole('link', { name: 'Import from Mealie' }).click();
  await page.getByRole('button', { name: 'Preview Mealie plan' }).click();
  await expect(page.getByRole('heading', { name: 'Check before importing' })).toBeVisible();
  await page.getByRole('button', { name: 'Import 1 days' }).click();
  await expect(page.getByRole('heading', { name: 'Import complete' })).toBeVisible();

  // The imported meal is linked to its recipe, which the panel reads live from Mealie.
  await page.goto('/guardian/mealplan');
  const mealRow = page.locator('app-manage-meals li', { hasText: recipeName });
  await expect(mealRow.getByText('Mealie', { exact: true })).toBeVisible();
  await mealRow.getByRole('button', { name: 'View recipe' }).click();
  await expect(page.getByText(`Linked to the meal “${recipeName}”`)).toBeVisible();
  await expect(
    page.locator('app-mealie-recipe-panel').getByText('500 g minced beef'),
  ).toBeVisible();

  // Next week holds the imported dinner; its recipe goes to the Mealie list.
  const plan = page.locator('app-assign-mealplan');
  await plan.getByRole('button', { name: 'Next week' }).click();
  await plan.getByRole('button', { name: 'Add to shopping list' }).click();
  await plan.getByLabel('Shopping list').selectOption({ label: listName });
  await plan.getByRole('button', { name: 'Add', exact: true }).click();
  await expect(plan.getByText(`Added to “${listName}” in Mealie: ${recipeName} ×1.`)).toBeVisible();

  await expect
    .poll(() => shoppingListItems(family, listId))
    .toContainEqual(expect.stringContaining('minced beef'));

  // Persisted: the connection survives a reload.
  await page.goto('/guardian/admin');
  await expect(page.locator('app-mealie-settings').getByText(MEALIE_URL)).toBeVisible();
});
