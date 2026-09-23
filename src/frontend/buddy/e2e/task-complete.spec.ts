import { SEEDED_USERS, expect, test } from './support/auth-fixture';
import { createCalendar, createGroup, uniqueName } from './support/guardian-data';

// Creating a calendar item is the real minimal path to a task appearing on "today" -- a
// task-library template alone (ManageTasks) never puts anything on a calendar by itself; it only
// becomes schedulable once picked from the calendar agenda's "From template" source (see
// agenda.ts's onTemplateSelected/createTaskFromTemplate). That path also forces an assignee (the
// picker only lists a *child's* templates, via the effect keyed on newAssignedTo), and a task
// assigned to a child is then only toggle-able by that child -- TasksToday.canToggle requires
// assignedTo to be null or the current (guardian) user's own id. So a template-scheduled task
// assigned to a child would render on the guardian dashboard but with its toggle disabled. The
// real minimal path for "the guardian completes a task from their own dashboard" is therefore a
// manual (non-template) Task item left unassigned, created straight from the calendar agenda.
test('guardian completes a task from the dashboard tasks-today widget, and it persists', async ({ page, loginAs }) => {
  await loginAs(SEEDED_USERS.alice);

  const groupName = await createGroup(page);
  const calendarName = await createCalendar(page, groupName);

  const title = uniqueName('E2E Task');

  await page.goto('/guardian/calendar');
  await expect(page.getByRole('heading', { name: 'This week' })).toBeVisible();

  const calendarSelect = page.locator('#itemCalendar');
  await expect(calendarSelect).toContainText(calendarName);
  await calendarSelect.selectOption({ label: calendarName });

  await page.getByRole('radio', { name: 'Task' }).click();
  await page.getByLabel('Title').fill(title);
  // Due date defaults to today and the assignee defaults to "Nobody" (unassigned) -- both are
  // exactly what's needed here, so nothing else needs to change before submitting.
  await page.getByRole('button', { name: 'Add to calendar' }).click();

  await expect(page.getByText(title, { exact: true })).toBeVisible();

  await page.goto('/guardian');
  await expect(page.getByRole('heading', { name: 'Guardian dashboard' })).toBeVisible();

  const toggle = page.getByRole('switch', { name: title });
  await expect(toggle).toHaveAttribute('aria-checked', 'false');

  const titleText = page.getByText(title, { exact: true });
  await expect(titleText).not.toHaveClass(/line-through/);

  await toggle.click();

  await expect(toggle).toHaveAttribute('aria-checked', 'true');
  await expect(titleText).toHaveClass(/line-through/);

  await page.reload();

  await expect(page.getByRole('switch', { name: title })).toHaveAttribute('aria-checked', 'true');
  await expect(page.getByText(title, { exact: true })).toHaveClass(/line-through/);
});
