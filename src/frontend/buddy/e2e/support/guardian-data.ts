import { type Page, expect } from '@playwright/test';

import { trackCreatedCalendar, trackCreatedChild, trackCreatedGroup } from './created-data-cleanup';

// Shared setup helpers for specs that need real prerequisite data before they can exercise a
// per-child/per-calendar flow -- see the e2e background: nothing is pre-seeded (no child,
// calendar, group, meal, medicine, or task-template data exists for a seeded Keycloak user), and
// the backend never auto-provisions any of it. Every helper here drives the real guardian UI
// (mirroring manage-children.spec.ts/manage-groups.spec.ts/manage-calendars.spec.ts's own
// component specs) rather than calling the API directly, so it stays honest to what a guardian can
// actually do. Names carry a random suffix so parallel specs -- and repeated runs against the same
// persistent backend (see playwright.config.ts's reuseExistingServer) -- never collide.

export interface CreatedChild {
  givenName: string;
  familyName: string;
  username: string;
}

// Not cryptographically unique, just unique enough across parallel workers and repeated runs.
function uniqueSuffix(): string {
  return `${Date.now().toString(36)}${Math.floor(Math.random() * 1e6).toString(36)}`;
}

export function uniqueName(prefix: string): string {
  return `${prefix}${uniqueSuffix()}`;
}

// Creates a child under the currently signed-in guardian via the real manage-children UI on
// /guardian/admin. A child record is a prerequisite for most per-child features (medicine, task
// library, pickups, mealplan all need at least one to exist -- see e.g. ManageMedicines.hasChildren).
export async function createChild(page: Page, prefix = 'E2eChild'): Promise<CreatedChild> {
  const givenName = uniqueName(prefix);
  const familyName = 'Testson';
  const username = uniqueName('e2echild').toLowerCase();

  await page.goto('/guardian/admin');

  // Scoped to the component's own custom-element tag -- "Given name"/"Family name" also label
  // fields on MyProfile (the guardian's own name), rendered on the same admin page.
  const section = page.locator('app-manage-children');
  await expect(section.getByRole('heading', { name: 'Children' })).toBeVisible();

  await section.getByLabel('Given name').fill(givenName);
  await section.getByLabel('Family name').fill(familyName);
  await section.getByLabel('Login username').fill(username);
  await section.getByRole('button', { name: 'Add child' }).click();

  // Creating a child is a multi-step operation (the backend provisions a real Keycloak user via
  // Keycloak's own admin API before appending its own events), and this is the single most-called
  // helper across the whole suite -- give it more room than the default 5s, especially against a
  // long-running local dev backend that's already handled a lot of traffic this session.
  await expect(section.getByText(`${givenName} was created.`)).toBeVisible({ timeout: 15_000 });

  // Unlinked from every seeded guardian (and its Keycloak user deleted) after the test -- see
  // created-data-cleanup.ts and the cleanUpCreatedData auto fixture in auth-fixture.ts. Requires
  // the spec to import `test` from ./support/auth-fixture, which every spec does.
  trackCreatedChild({ givenName, familyName, username });

  return { givenName, familyName, username };
}

// Selects a child by given name in a per-child <select>, if one is even rendered -- a guardian
// with exactly one child never gets one (see e.g. manage-medicines.html's `@if (children().length
// > 1)`). Safe to call unconditionally regardless of how many children currently exist.
//
// Waits (rather than taking an instant count) because the select only attaches once the page's
// own children fetch resolves -- checking immediately after navigation races that fetch and can
// misread "not loaded yet" as "only one child", silently leaving whatever child the page defaulted
// to (its first-loaded one, not necessarily the one a test just created) selected instead.
export async function selectChildIfPresent(page: Page, givenName: string): Promise<void> {
  const select = page.locator('#selectedChildId');

  try {
    await select.waitFor({ state: 'attached', timeout: 5000 });
  } catch {
    return;
  }

  // The select attaches as soon as more than one child is loaded, but the page's *initial* fetch
  // (for whichever child it defaulted to) can still be in flight at that exact moment -- switching
  // immediately races it, and if that still-in-flight response for the *other* child resolves
  // after ours, it clobbers the view (these components don't guard a stale response against a
  // newer selection). Waiting for the network to settle first ensures the initial fetch has
  // already landed before we trigger our own.
  await page.waitForLoadState('networkidle');
  await select.selectOption({ label: givenName });
  await page.waitForLoadState('networkidle');
}

// Creates a group via the real manage-groups UI on /guardian/admin. A group is the entity every
// calendar is owned by (CreateCalendarRequest.groupId is required -- "a calendar is always
// group-owned now", see calendars.service.ts), so this is a prerequisite for createCalendar below.
// Returns the group's name for selecting it afterward.
export async function createGroup(page: Page, prefix = 'E2eGroup'): Promise<string> {
  const name = uniqueName(prefix);

  await page.goto('/guardian/admin');

  const section = page.locator('app-manage-groups');
  await expect(section.getByRole('heading', { name: 'Groups' })).toBeVisible();

  await section.getByLabel('Group name').fill(name);
  await section.getByRole('button', { name: 'Add group' }).click();

  await expect(section.getByText(name, { exact: true })).toBeVisible();

  // Deleted after the test (cascading to its calendars) -- see created-data-cleanup.ts.
  trackCreatedGroup(name);

  return name;
}

// Creates a calendar owned by the given group, via the real manage-calendars UI on
// /guardian/admin. ManageCalendars only fetches its "manageable groups" dropdown once on init (see
// manage-calendars.ts's loadManageableGroups, called from ngOnInit with no reactivity to a group
// created afterward on the same page) -- callers must have navigated to /guardian/admin *after*
// the group already existed for it to show up here, which createGroup's own page.goto guarantees.
export async function createCalendar(page: Page, groupName: string, prefix = 'E2eCalendar'): Promise<string> {
  const name = uniqueName(prefix);

  await page.goto('/guardian/admin');

  const section = page.locator('app-manage-calendars');
  await expect(section.getByRole('heading', { name: 'Calendars' })).toBeVisible();

  const groupSelect = section.locator('#calendarGroupId');
  await expect(groupSelect).toContainText(groupName);
  await groupSelect.selectOption({ label: groupName });

  await section.getByLabel('Calendar name').fill(name);
  await section.getByRole('button', { name: 'Add calendar' }).click();

  // The list row renders "<icon> <name>" as one text node (see manage-calendars.html), so an
  // exact match against the name alone would never hit -- a substring match is enough here.
  await expect(section.getByText(name)).toBeVisible();

  // Deleted after the test -- see created-data-cleanup.ts.
  trackCreatedCalendar(name);

  return name;
}
