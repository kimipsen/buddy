import { SEEDED_USERS, expect, test } from './support/auth-fixture';
import { createChild, uniqueName } from './support/guardian-data';
import { getAccessToken } from './support/keycloak-client';
import { readRuntimeConfig } from './support/runtime-config';

// Scoped to the guardian-side assignment only: rating a planned meal requires signing in as the
// child, and children in this app have no independent login flow of their own to drive from a
// guardian-only fast-login fixture (SEEDED_USERS only covers guardian accounts -- see
// auth-fixture.ts -- and a freshly created child's temporary password is a one-time value surfaced
// in the UI, not a stable seeded credential). Exercising that half would mean scraping the
// just-created child's username/temporary password out of the manage-children UI and driving a
// second, real Keycloak login with it -- out of scope for this spec, which sticks to what a
// guardian alone can do: create a meal and assign it to a slot in the current week.
test('guardian creates a meal and assigns it to a slot in the current week', async ({ page, loginAs }) => {
  await loginAs(SEEDED_USERS.alice);

  // A child must exist for the family mealplan scope to be available at all (see
  // GuardianMealplan.load: hasChildren gates the whole page) -- the meal/assignment below aren't
  // themselves tied to this specific child.
  await createChild(page);

  const mealName = uniqueName('E2E Meal');

  await page.goto('/guardian/mealplan');
  await expect(page.getByRole('heading', { name: 'Meals', exact: true })).toBeVisible();
  await expect(page.getByRole('heading', { name: "This week's plan" })).toBeVisible();

  await page.getByLabel('Meal name').fill(mealName);
  await page.getByRole('button', { name: 'Add meal' }).click();

  // The list row renders "<icon> <name>" as one text node (see manage-meals.html), so an exact
  // match against the name alone would never hit -- a substring match is enough here.
  await expect(page.getByText(mealName)).toBeVisible();

  // The assignment grid's first row is always anchored on today (AssignMealplan.buildDays starts
  // from todayIsoDate()) and slot columns run Breakfast/Lunch/Dinner/Snack in that fixed order, so
  // the first textbox in the first row is always today's breakfast meal-picker.
  const todayRow = page.locator('tbody tr').first();
  const breakfastPicker = todayRow.getByRole('textbox').first();

  // Filter down to this meal by typing its (unique) name and confirm with Enter
  // (MealPicker.selectFirstMatch), rather than clicking its row in the dropdown list directly: the
  // dropdown is a `position: fixed` panel anchored below the input, and alice's meal/group library
  // only ever grows across repeated local test runs (nothing here is per-run isolated data), which
  // over time pushes this row far enough down the page that the fixed dropdown renders partly below
  // the viewport -- scrolling the page doesn't help a fixed-position element, so clicking a specific
  // list item becomes flaky regardless of list length. Typing+Enter sidesteps that entirely.
  await breakfastPicker.click();
  await breakfastPicker.fill(mealName);

  const [assignResponse] = await Promise.all([
    page.waitForResponse((res) => res.request().method() === 'PUT' && /\/mealplans\/children\/[^/]+\/plan\?/.test(res.url())),
    breakfastPicker.press('Enter'),
  ]);

  expect(assignResponse.ok()).toBe(true);
  await expect(breakfastPicker).toHaveValue(new RegExp(mealName));

  // Verify persistence directly via the API for the exact child/date/slot just written, instead of
  // reloading the page and trusting the UI to resolve the same "family scope" child again:
  // GuardianMealplan.load always uses whichever child comes first from ListMyChildren, and that
  // list has no stable ordering -- ListForGuardianAsync (MartenGuardianLinkEventStore.cs) queries
  // without an ORDER BY, and GuardianLinkDocument carries no timestamp to sort by even if it did.
  // Once alice (reused across every local run of this spec, never cleaned up) has more than one
  // child, "family scope" can silently resolve to a different sibling between two page loads with
  // no intervening writes to that data at all -- confirmed empirically: a plain reload here
  // sometimes showed a completely different, older sibling's meal instead of this run's own. That's
  // a real ordering bug worth its own fix, but it's orthogonal to what this spec is verifying, so
  // this reads the specific child's plan back over the API rather than through a UI path that's
  // known to be unreliable for reasons that have nothing to do with meal assignment.
  const assignUrl = new URL(assignResponse.url());
  const childId = assignUrl.pathname.match(/\/children\/([^/]+)\/plan$/)?.[1];
  const assignBody = (await assignResponse.json()) as { mealId: string; date: string };

  if (!childId) {
    throw new Error(`Could not extract childId from assignment request URL: ${assignResponse.url()}`);
  }

  const { apiBaseUrl } = readRuntimeConfig();
  const { accessToken } = await getAccessToken(SEEDED_USERS.alice.username, SEEDED_USERS.alice.password);

  const planResponse = await page.request.get(`${apiBaseUrl}/mealplans/children/${childId}/plan`, {
    params: { from: assignBody.date, to: assignBody.date },
    headers: { Authorization: `Bearer ${accessToken}` },
  });

  expect(planResponse.ok()).toBe(true);

  const entries = (await planResponse.json()) as Array<{ date: string; slot: number; mealId: string }>;
  const persisted = entries.find((entry) => entry.date === assignBody.date && entry.slot === 0);

  expect(persisted?.mealId).toBe(assignBody.mealId);
});
