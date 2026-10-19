import { expect, test } from './support/auth-fixture';
import { createChild, uniqueName } from './support/guardian-data';

// A disposable guardian with one child, for the same reason as mealplan-assignment.spec.ts: the
// family scope resolves from the guardian's first child, which a seeded guardian shares with
// parallel tests. See docs/backend/analysis/eating-out-meals.md.
test('guardian creates an eating-out meal and plans a dinner out', async ({
  page,
  loginAs,
  newGuardian,
}) => {
  const guardian = await newGuardian();
  await loginAs(guardian);
  await createChild(page);

  const mealName = uniqueName('E2E Restaurant');

  await page.goto('/guardian/mealplan');
  await expect(page.getByRole('heading', { name: "This week's plan" })).toBeVisible();

  await page.getByLabel('Meal name').fill(mealName);
  await page.getByRole('radio', { name: 'Restaurant' }).click();
  await page.getByRole('button', { name: 'Add meal' }).click();

  const mealRow = page.getByRole('listitem').filter({ hasText: mealName });
  await expect(mealRow.getByText('Eating out · Restaurant')).toBeVisible();

  // Today's row; the slot columns run Breakfast/Lunch/Dinner/Snack, so the third picker is dinner.
  const dinnerCell = page.locator('tbody tr').first().locator('td').nth(2);
  await dinnerCell.getByRole('textbox').click();

  const [assignResponse] = await Promise.all([
    page.waitForResponse(
      (res) =>
        res.request().method() === 'PUT' && /\/mealplans\/children\/[^/]+\/plan\?/.test(res.url()),
    ),
    page.getByRole('button', { name: mealName }).click(),
  ]);

  expect(assignResponse.ok()).toBe(true);
  expect(((await assignResponse.json()) as { eatingOut: string }).eatingOut).toBe('Restaurant');
  await expect(dinnerCell.getByText('Eating out', { exact: true })).toBeVisible();
  await expect(dinnerCell.getByRole('textbox')).toHaveValue(new RegExp(mealName));
});
