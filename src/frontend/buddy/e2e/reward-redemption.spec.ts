import { expect, test } from './support/auth-fixture';
import { createCalendar, createChild, createGroup, uniqueName } from './support/guardian-data';
import { makeChildLoginUsable } from './support/keycloak-admin-client';

// Reward redemption end to end (docs/backend/analysis/reward-redemption.md): a guardian adds a
// reward, the child earns a star by completing a task, asks for the reward, the guardian sees the
// request on the dashboard and approves it, and the child sees the outcome and the spent star.
// Everything runs against the real API, Keycloak and Postgres.
//
// A disposable guardian (newGuardian) because createChild is involved and the progress page
// defaults to the guardian's first child. The child is added to the group so the calendar's task
// reaches their home page, where they complete it (a task assigned to a child is only toggle-able
// by that child; see task-complete.spec.ts). The child signs in by direct grant once
// makeChildLoginUsable has replaced its temporary password (see house-rules-acknowledge.spec.ts).
test('a child spends a star on a reward once the guardian approves it', async ({
  page,
  loginAs,
  newGuardian,
}) => {
  const guardian = await newGuardian();
  await loginAs(guardian);
  const child = await createChild(page);
  const groupName = await createGroup(page);
  const calendarName = await createCalendar(page, groupName);
  const childLabel = `${child.givenName} ${child.familyName}`;

  const groupRow = page.locator('app-manage-groups').locator('li', { hasText: groupName });
  await groupRow.getByRole('button', { name: 'Add a child' }).click();
  await groupRow.getByLabel('Choose a child').selectOption({ label: childLabel });
  const [addChildResponse] = await Promise.all([
    page.waitForResponse(
      (res) =>
        res.request().method() === 'PUT' && /\/groups\/[^/]+\/children\/[^/]+$/.test(res.url()),
    ),
    groupRow.getByRole('button', { name: 'Add to group' }).click(),
  ]);
  expect(addChildResponse.ok()).toBe(true);

  // A task for today, assigned to the child: completing it earns one star.
  const taskTitle = uniqueName('E2E Reward task');
  await page.goto('/guardian/calendar');
  await expect(page.getByRole('heading', { name: 'This week' })).toBeVisible();
  const calendarSelect = page.locator('#itemCalendar');
  await expect(calendarSelect).toContainText(calendarName);
  await calendarSelect.selectOption({ label: calendarName });
  await page.getByRole('radio', { name: 'Task', exact: true }).click();
  await page.getByLabel('Title').fill(taskTitle);
  await page.getByLabel('Assign to').selectOption({ label: childLabel });
  await page.getByRole('button', { name: 'Add to calendar' }).click();
  await expect(page.getByText(taskTitle, { exact: true })).toBeVisible();

  // The reward, costing exactly the one star the child is about to earn.
  const rewardName = uniqueName('Screen time ');
  await page.goto('/guardian/progress');
  const rewards = page.locator('app-manage-rewards');
  await expect(rewards.getByRole('heading', { name: 'Rewards', exact: true })).toBeVisible();
  await expect(rewards.getByText('No reward requests are waiting.')).toBeVisible();
  await rewards.getByRole('button', { name: '+ Add a reward' }).click();
  await rewards.getByLabel('Reward', { exact: true }).fill(rewardName);
  await rewards.getByLabel('Stars', { exact: true }).fill('1');
  await rewards.getByRole('button', { name: 'Save rewards' }).click();
  await expect(rewards.getByText('Rewards saved.')).toBeVisible();

  // The child's side: earn the star, then ask for the reward.
  const childLogin = await makeChildLoginUsable(child.username);
  await loginAs(childLogin);
  await expect(page).toHaveURL(/\/child$/);
  const task = page.getByRole('listitem').filter({ hasText: taskTitle });
  await task.getByRole('button', { name: 'Mark done' }).click();
  await expect(task.getByRole('button', { name: 'Mark not done' })).toBeVisible();

  await page.getByRole('link', { name: 'Spend your stars' }).click();
  await expect(page.getByRole('heading', { name: 'Rewards', level: 1 })).toBeVisible();
  await expect(page.getByTestId('reward-balance')).toHaveText(/1 stars to spend/);
  const card = page
    .getByRole('listitem')
    .filter({ has: page.getByRole('heading', { name: rewardName }) });
  await card.getByRole('button', { name: 'Ask for this' }).click();
  await expect(page.getByRole('heading', { name: 'Waiting for a grown-up' })).toBeVisible();
  // The star is reserved while the request waits.
  await expect(page.getByTestId('reward-balance')).toHaveText(/0 stars to spend/);

  // The guardian notices it on the dashboard and approves it.
  await loginAs(guardian);
  await page.goto('/guardian');
  await page.getByRole('link', { name: 'Rewards waiting: 1' }).click();
  await expect(page).toHaveURL(/\/guardian\/progress\?child=/);
  const request = rewards.getByRole('listitem').filter({ hasText: rewardName });
  await request.getByRole('button', { name: 'Approve' }).click();
  await expect(rewards.getByText('No reward requests are waiting.')).toBeVisible();
  await expect(rewards.getByTestId('reward-balance')).toHaveText(
    '0 stars to spend, 1 spent, 1 earned in all.',
  );

  // Persisted, and the child sees the outcome.
  await page.reload();
  await expect(rewards.getByTestId('reward-balance')).toHaveText(
    '0 stars to spend, 1 spent, 1 earned in all.',
  );
  await loginAs(childLogin);
  await page.goto('/child/rewards');
  await expect(page.getByRole('heading', { name: 'Earlier' })).toBeVisible();
  await expect(page.getByRole('listitem').filter({ hasText: rewardName }).last()).toContainText(
    'Yes!',
  );
  await expect(page.getByTestId('reward-balance')).toHaveText(/0 stars to spend/);
});
