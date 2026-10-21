// Drives the family's Mealie (docs/backend/analysis/mealie-integration.md) directly through its
// own API, to set up what a family would have made in Mealie by hand: a household, a non-admin
// user with an API token, recipes, plan entries and a shopping list. Every call uses Mealie's
// documented first-run admin, or the user it creates.
//
// Where Mealie is: MEALIE_URL, else the devcontainer's `mealie` service (http://mealie:9000), or
// its published port on a CI runner. Until the devcontainer is rebuilt with that service, start one
// with `task dev:mealie` and run with MEALIE_URL=http://localhost:9925.
//
// The API must allow a plain-http, private-network Mealie: Playwright starts it with
// Mealie__AllowHttp and Mealie__AllowPrivateNetworks (playwright.config.ts,
// playwright.screenshots.config.ts), but an API that is already running is reused as is. Stop it
// first, or set Mealie:AllowHttp and Mealie:AllowPrivateNetworks in appsettings.Development.json.
export const MEALIE_URL =
  process.env['MEALIE_URL'] ?? (process.env['CI'] ? 'http://localhost:9926' : 'http://mealie:9000');

const ADMIN_EMAIL = 'changeme@example.com';
const ADMIN_PASSWORD = 'MyPassword';

async function call<T>(token: string, method: string, path: string, body?: unknown): Promise<T> {
  const response = await fetch(`${MEALIE_URL}${path}`, {
    method,
    headers: {
      Authorization: `Bearer ${token}`,
      ...(body === undefined ? {} : { 'Content-Type': 'application/json' }),
    },
    body: body === undefined ? undefined : JSON.stringify(body),
  });

  if (!response.ok) {
    throw new Error(`Mealie ${method} ${path}: ${response.status} ${await response.text()}`);
  }

  const text = await response.text();
  return (text ? JSON.parse(text) : undefined) as T;
}

async function login(username: string, password: string): Promise<string> {
  const response = await fetch(`${MEALIE_URL}/api/auth/token`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
    body: new URLSearchParams({ username, password }),
  });

  if (!response.ok) {
    throw new Error(`Mealie login as ${username}: ${response.status} ${await response.text()}`);
  }

  return ((await response.json()) as { access_token: string }).access_token;
}

export interface MealieFamily {
  household: string;
  // The long-lived API token a guardian pastes into Buddy.
  apiToken: string;
}

// A household of its own (so nothing another run planned or listed shows up) with a non-admin
// user, as the Buddy settings card tells a family to make.
export async function createMealieFamily(name: string): Promise<MealieFamily> {
  const admin = await login(ADMIN_EMAIL, ADMIN_PASSWORD);
  const self = await call<{ groupId: string; group: string }>(admin, 'GET', '/api/users/self');
  const household = name;
  await call(admin, 'POST', '/api/admin/households', { groupId: self.groupId, name: household });

  const username = name.toLowerCase().replace(/[^a-z0-9]+/g, '');
  const password = `${username}-Pw1!`;
  await call(admin, 'POST', '/api/admin/users', {
    username,
    fullName: name,
    email: `${username}@buddy.test`,
    password,
    admin: false,
    group: self.group,
    household,
  });

  const session = await login(username, password);
  const { token } = await call<{ token: string }>(session, 'POST', '/api/users/api-tokens', {
    name: 'buddy',
  });

  return { household, apiToken: token };
}

// The same, for a fixed name that may exist from an earlier run (the doc screenshots' demo
// family): an existing household and user are reused, and a fresh API token is made.
export async function ensureMealieFamily(
  household: string,
  username: string,
): Promise<MealieFamily> {
  const admin = await login(ADMIN_EMAIL, ADMIN_PASSWORD);
  const self = await call<{ groupId: string; group: string }>(admin, 'GET', '/api/users/self');
  const password = `${username}-Pw1!`;

  await call(admin, 'POST', '/api/admin/households', {
    groupId: self.groupId,
    name: household,
  }).catch(() => undefined);
  await call(admin, 'POST', '/api/admin/users', {
    username,
    fullName: household,
    email: `${username}@buddy.test`,
    password,
    admin: false,
    group: self.group,
    household,
  }).catch(() => undefined);

  const session = await login(username, password);
  const { token } = await call<{ token: string }>(session, 'POST', '/api/users/api-tokens', {
    name: `buddy-${Date.now()}`,
  });

  return { household, apiToken: token };
}

// An existing recipe of that name, or a new one.
export async function ensureRecipe(
  family: MealieFamily,
  name: string,
  ingredients: string[],
): Promise<{ id: string; slug: string }> {
  const found = await call<{ items: { id: string; slug: string; name: string }[] }>(
    family.apiToken,
    'GET',
    `/api/recipes?search=${encodeURIComponent(name)}&perPage=50`,
  );
  const existing = found.items.find((recipe) => recipe.name === name);

  return existing
    ? { id: existing.id, slug: existing.slug }
    : createRecipe(family, name, ingredients);
}

export async function ensureShoppingList(family: MealieFamily, name: string): Promise<string> {
  const lists = await call<{ items: { id: string; name: string }[] }>(
    family.apiToken,
    'GET',
    '/api/households/shopping/lists?perPage=-1',
  );

  return lists.items.find((list) => list.name === name)?.id ?? createShoppingList(family, name);
}

export async function createRecipe(
  family: MealieFamily,
  name: string,
  ingredients: string[],
): Promise<{ id: string; slug: string }> {
  const slug = await call<string>(family.apiToken, 'POST', '/api/recipes', { name });
  await call(family.apiToken, 'PATCH', `/api/recipes/${slug}`, {
    recipeIngredient: ingredients.map((note) => ({ note, display: note })),
    recipeInstructions: [{ text: 'Cook it.' }],
  });
  const recipe = await call<{ id: string }>(family.apiToken, 'GET', `/api/recipes/${slug}`);

  return { id: recipe.id, slug };
}

export async function addPlanEntry(
  family: MealieFamily,
  date: string,
  entryType: 'breakfast' | 'lunch' | 'dinner' | 'side' | 'snack',
  recipeId: string,
): Promise<void> {
  await call(family.apiToken, 'POST', '/api/households/mealplans', { date, entryType, recipeId });
}

export async function createShoppingList(family: MealieFamily, name: string): Promise<string> {
  return (
    await call<{ id: string }>(family.apiToken, 'POST', '/api/households/shopping/lists', { name })
  ).id;
}

export async function shoppingListItems(family: MealieFamily, listId: string): Promise<string[]> {
  const list = await call<{ listItems: { display?: string; note?: string }[] }>(
    family.apiToken,
    'GET',
    `/api/households/shopping/lists/${listId}`,
  );

  return list.listItems.map((item) => item.display ?? item.note ?? '');
}
