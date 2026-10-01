import { expect, test } from './support/auth-fixture';
import { createCalendar, createChild, createGroup, uniqueName } from './support/guardian-data';

// TaskTemplates are per-child (TaskLibraryAccessTier has no group-sharing axis -- see
// ManageTasks's own comment), so a child is a prerequisite, same as ManageMedicines. Also builds a
// group + calendar and adds the child to that group, purely so the archived template can be
// verified against the *picker* (app-task-picker on the calendar's "add a task" form) rather than
// just against ManageTasks's own list -- which deliberately keeps an archived template visible
// (with a badge) instead of hiding it, so checking there wouldn't prove archiving actually removes
// it from what's schedulable.
test('guardian builds a task template with subtasks, reorders and edits them, then archives it', async ({ page, loginAs, newGuardian }) => {
  // A disposable guardian whose only child is this test's own (see newGuardian in auth-fixture.ts).
  await loginAs(await newGuardian());

  const child = await createChild(page);
  const groupName = await createGroup(page);
  const calendarName = await createCalendar(page, groupName);

  const groupsSection = page.locator('app-manage-groups');
  const groupRow = groupsSection.locator('li', { hasText: groupName });

  await groupRow.getByRole('button', { name: 'Add a child' }).click();
  await groupRow.getByLabel('Choose a child').selectOption({ label: `${child.givenName} ${child.familyName}` });

  const [addChildResponse] = await Promise.all([
    page.waitForResponse((res) => res.request().method() === 'PUT' && /\/groups\/[^/]+\/children\/[^/]+$/.test(res.url())),
    groupRow.getByRole('button', { name: 'Add to group' }).click(),
  ]);
  expect(addChildResponse.ok()).toBe(true);

  await page.goto('/guardian/task-library');
  await expect(page.getByRole('heading', { name: 'Task templates', exact: true })).toBeVisible();

  const templateName = uniqueName('E2E Template');
  await page.getByLabel('Template name').fill(templateName);
  await page.getByRole('button', { name: 'Add template' }).click();

  const templateRow = page.locator('li', { hasText: templateName });
  await expect(templateRow).toBeVisible();

  // A freshly created template auto-expands its subtasks panel (ManageTasks.createTemplate sets
  // expandedTemplateId to the new template's id), so the "Add subtask" form is already visible
  // here without an extra "Edit subtasks" click.
  const subtaskPrefix = uniqueName('E2eSubtask');
  const alphaTitle = `${subtaskPrefix} Alpha`;
  const bravoTitle = `${subtaskPrefix} Bravo`;

  // The subtask row renders "<icon?> <title> · <duration>" as one merged text block (see
  // manage-tasks.html), so an exact match against the title alone would never hit -- a substring
  // match is enough here, mirroring the same "<icon> <name>" pattern other specs already work
  // around (e.g. manage-meals.html, manage-calendars.html).
  await page.getByLabel('Subtask title').fill(alphaTitle);
  await page.getByRole('button', { name: 'Add subtask' }).click();
  await expect(page.getByText(alphaTitle)).toBeVisible();

  await page.getByLabel('Subtask title').fill(bravoTitle);
  await page.getByRole('button', { name: 'Add subtask' }).click();
  await expect(page.getByText(bravoTitle)).toBeVisible();

  // Structural rather than text-based: the template's own <li> wraps its whole expanded subtasks
  // panel (itself a nested <ul><li> list), so a plain `hasText` filter on `li` would also match
  // that *outer* row (its full text includes both subtask titles too) -- and once the edit step
  // below opens a subtask's inline edit form, that row's title becomes an <input value=...>
  // instead of a text node, so any `hasText` filter would stop matching that row entirely at that
  // point. The subtasks <ul> is the innermost list whose rows have Move up/down buttons (the outer
  // templates-list <ul> also has one of those as a deep descendant, so it's excluded by not
  // containing a further nested <ul> itself) -- scoping through that identifies the exact two
  // subtask rows regardless of which one (if any) is mid-edit.
  const subtasksList = page
    .locator('ul')
    .filter({ has: page.getByRole('button', { name: 'Move up' }) })
    .filter({ hasNot: page.locator('ul') });
  const subtaskRows = subtasksList.locator('li');
  await expect(subtaskRows).toHaveCount(2);

  const initialOrder = await subtaskRows.allTextContents();
  expect(initialOrder[0]).toContain('Alpha');
  expect(initialOrder[1]).toContain('Bravo');

  // Reorder via the stepper's move-up/down buttons (Stepper.moveSubtask) rather than any
  // drag-and-drop -- this app has no drag precedent to reuse (see ManageTasks.moveSubtask).
  const [reorderResponse] = await Promise.all([
    page.waitForResponse((res) => res.request().method() === 'PUT' && res.url().includes('/subtasks/order')),
    subtaskRows.filter({ hasText: bravoTitle }).getByRole('button', { name: 'Move up' }).click(),
  ]);
  expect(reorderResponse.ok()).toBe(true);

  const reorderedOrder = await subtaskRows.allTextContents();
  expect(reorderedOrder[0]).toContain('Bravo');
  expect(reorderedOrder[1]).toContain('Alpha');

  // Inline-edit the (now second) Alpha subtask -- referenced by position (`nth(1)`, confirmed by
  // reorderedOrder above) rather than by re-matching its title text: once edit mode opens, the
  // title becomes an <input value=...> instead of a text node, so a `hasText` filter re-evaluated
  // after that point would no longer match the row at all. Position is stable across that
  // transition since Angular doesn't reorder the underlying DOM nodes just from editing one.
  // Scoping every step to this one row also sidesteps the "Subtask title" aria-label being reused,
  // unscoped, by both this row's own edit input and the always-present "Add subtask" input below.
  const editedAlphaTitle = `${alphaTitle} Edited`;
  const alphaRow = subtaskRows.nth(1);
  await alphaRow.getByRole('button', { name: 'Edit', exact: true }).click();
  await alphaRow.getByLabel('Subtask title').fill(editedAlphaTitle);
  await alphaRow.getByRole('button', { name: 'Save', exact: true }).click();

  await expect(alphaRow.getByText(editedAlphaTitle)).toBeVisible();

  // Archive the template and confirm it's flagged as archived in ManageTasks's own list...
  await templateRow.getByRole('button', { name: 'Archive' }).click();
  await expect(templateRow.getByText('Archived', { exact: true })).toBeVisible();

  // ...then confirm the real behavioral effect: it's no longer offered by the task picker used to
  // schedule a calendar task from a template, for the same child it belongs to.
  await page.goto('/guardian/calendar');
  await expect(page.getByRole('heading', { name: 'This week' })).toBeVisible();

  const calendarSelect = page.locator('#itemCalendar');
  await expect(calendarSelect).toContainText(calendarName);
  await calendarSelect.selectOption({ label: calendarName });

  await page.getByRole('radio', { name: 'Task', exact: true }).click();
  await page.getByLabel('Assign to').selectOption({ label: `${child.givenName} ${child.familyName}` });
  await page.getByRole('button', { name: 'From template' }).click();

  const picker = page.getByLabel('No template');
  await picker.click();
  await picker.fill(templateName);

  await expect(page.getByText('No templates match.', { exact: true })).toBeVisible();
});
