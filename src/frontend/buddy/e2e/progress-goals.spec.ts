import { expect, test } from './support/auth-fixture';
import { createChild } from './support/guardian-data';

// ConfigureGoalPostsValidator (backend) requires the list to be non-empty, each threshold
// strictly positive, each icon non-empty, and thresholds strictly ascending with no duplicates --
// matching the frontend's own "Make sure star counts are positive and increasing" error text. This
// spec always adds two rows with thresholds 5 then 10 so both the frontend's canSave() gate and
// the backend's own validation are satisfied throughout.
test('guardian adds and removes goal-post rows, and changes persist after reload', async ({
  page,
  loginAs,
  newGuardian,
}) => {
  // A disposable guardian whose only child is this test's own (see newGuardian in auth-fixture.ts).
  await loginAs(await newGuardian());

  await createChild(page, 'E2eProgressChild');

  await page.goto('/guardian/progress');

  const section = page.locator('app-manage-progress-goals');
  await expect(section.getByRole('heading', { name: 'Goal posts' })).toBeVisible();

  const addGoalButton = section.getByRole('button', { name: '+ Add another goal' });
  const saveButton = section.getByRole('button', { name: 'Save goal posts' });
  const removeButton = section.getByRole('button', { name: 'Remove' });

  // Even a brand-new, never-configured child comes back with rows already pre-filled: the
  // backend resolves an unconfigured child's goal posts to its own built-in default scale rather
  // than an empty list (GoalPostResolver.DefaultGoalPosts: 5/10/25/50/100 -- see
  // GoalPostResolver.cs and ProgressSummary.From), and ManageProgressGoals renders exactly what it
  // gets back. Clear those out first so addRow below appends at a known index (0, not "however
  // many defaults happened to precede it") -- this is purely local component state until Save is
  // clicked, so it's safe to do without ever persisting the now-empty list. The heading renders
  // before the goal posts load, so wait for the default rows before counting them.
  await expect(removeButton.first()).toBeVisible();
  let remaining = await removeButton.count();

  while (remaining > 0) {
    await removeButton.first().click();
    // Removing a row re-renders the whole list (@for tracks by $index, so every row past the one
    // removed gets a new DOM node) -- wait for that reflow to actually land before querying the
    // count again, or the next click can race a mid-render "first" button as it's being replaced.
    await expect(removeButton).toHaveCount(remaining - 1);
    remaining -= 1;
  }

  await addGoalButton.click();
  await section.locator('[name="goalThreshold0"]').fill('5');
  await section.locator('[name="goalIcon0"]').fill('🌟');
  await section.locator('[name="goalLabel0"]').fill('Getting started');

  await addGoalButton.click();
  await section.locator('[name="goalThreshold1"]').fill('10');
  await section.locator('[name="goalIcon1"]').fill('🏆');
  await section.locator('[name="goalLabel1"]').fill('Superstar');

  await expect(saveButton).toBeEnabled();
  await saveButton.click();

  await expect(section.getByText('Goal posts saved.')).toBeVisible();

  // Reload and confirm both rows persisted, in order, with their exact values.
  await page.reload();

  await expect(section.locator('[name="goalThreshold0"]')).toHaveValue('5');
  await expect(section.locator('[name="goalIcon0"]')).toHaveValue('🌟');
  await expect(section.locator('[name="goalLabel0"]')).toHaveValue('Getting started');
  await expect(section.locator('[name="goalThreshold1"]')).toHaveValue('10');
  await expect(section.locator('[name="goalIcon1"]')).toHaveValue('🏆');
  await expect(section.locator('[name="goalLabel1"]')).toHaveValue('Superstar');

  // Remove the second (most recently added) row and confirm only the first survives a save +
  // reload. Rows render in the order rows() holds them, so the second "Remove" button in the DOM
  // is unambiguously the second row's -- no need for a fixed-position-dropdown-style click that
  // could drift as data accumulates.
  await removeButton.nth(1).click();
  await expect(section.locator('[name="goalThreshold1"]')).toHaveCount(0);

  await saveButton.click();
  await expect(section.getByText('Goal posts saved.')).toBeVisible();

  await page.reload();

  await expect(section.locator('[name="goalThreshold0"]')).toHaveValue('5');
  await expect(section.locator('[name="goalThreshold1"]')).toHaveCount(0);
});
