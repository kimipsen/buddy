import { expect, test } from './support/auth-fixture';
import { createChild, uniqueName } from './support/guardian-data';

// Pictogram support (docs/frontend/analysis/pictograms.md) through the real API: a guardian
// gives a routine step the "Brush teeth" pictogram from the icon picker's grid, the step shows the
// picture instead of an emoji, and it is still there after a reload (stored as
// "pictogram:brush-teeth" in the existing Icon string).
//
// A disposable guardian because task templates are per child and createChild is involved.
test('guardian gives a routine step a pictogram from the picker, and it persists', async ({
  page,
  loginAs,
  newGuardian,
}) => {
  await loginAs(await newGuardian());
  await createChild(page);

  await page.goto('/guardian/task-library');
  await expect(page.getByRole('heading', { name: 'Task templates', exact: true })).toBeVisible();

  const templateName = uniqueName('E2E Pictogram routine');
  await page.getByLabel('Template name').fill(templateName);
  await page.getByRole('button', { name: 'Add template' }).click();
  await expect(page.locator('li', { hasText: templateName })).toBeVisible();

  // The new template's subtask form is open (ManageTasks expands a new template). Several icon
  // pickers are on the page (new template, new subtask) and a picker has no accessible name of
  // its own, so it is found inside the form whose button adds the subtask. (Not by its input:
  // the input is replaced by the picture once a pictogram is chosen.)
  const stepTitle = uniqueName('E2E Teeth');
  await page.getByLabel('Subtask title').fill(stepTitle);
  const stepPicker = page
    .locator('form')
    .filter({ has: page.getByRole('button', { name: 'Add subtask' }) })
    .locator('app-icon-picker');
  await stepPicker.getByRole('button', { name: 'Pictures' }).click();

  const grid = page.getByRole('dialog', { name: 'Choose a picture' });
  await grid.getByRole('searchbox', { name: 'Search pictures' }).fill('teeth');
  await grid.getByRole('button', { name: 'Brush teeth' }).click();
  await expect(grid).toBeHidden();

  // The chosen pictogram replaces the emoji input with its picture, name and a remove button.
  await expect(stepPicker.getByRole('button', { name: 'Remove picture' })).toBeVisible();
  await expect(stepPicker).toContainText('Brush teeth');

  const [addResponse] = await Promise.all([
    page.waitForResponse(
      (res) =>
        res.request().method() === 'POST' && /\/task-templates\/[^/]+\/subtasks$/.test(res.url()),
    ),
    page.getByRole('button', { name: 'Add subtask' }).click(),
  ]);
  expect(addResponse.ok()).toBe(true);
  expect(addResponse.request().postDataJSON()).toMatchObject({ icon: 'pictogram:brush-teeth' });

  // The step row shows the picture (decorative next to its title, so selected by its file). The
  // step's own row is the innermost <li>: the template's row is an outer <li> that contains it.
  const stepRow = () =>
    page.locator('li', { hasText: stepTitle }).filter({ hasNot: page.locator('li') });
  await expect(stepRow().locator('img[src="/pictograms/brush-teeth.svg"]')).toBeVisible();
  await expect(stepRow()).not.toContainText('pictogram:');

  await page.reload();
  await expect(page.getByRole('heading', { name: 'Task templates', exact: true })).toBeVisible();
  await page
    .locator('li', { hasText: templateName })
    .getByRole('button', { name: 'Edit subtasks' })
    .click();
  await expect(stepRow().locator('img[src="/pictograms/brush-teeth.svg"]')).toBeVisible();
});
