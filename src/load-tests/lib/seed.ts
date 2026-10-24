import http, { type RefinedResponse, type ResponseType } from 'k6/http';

import { adminToken, login, type TokenSet } from './auth.ts';
import { config } from './config.ts';
import { pathOf, type Body, type Method, type Params, type RouteWith } from './contract.ts';
import { daysFrom } from './dates.ts';

// Builds the load-test families through the Keycloak admin API and the real Buddy API, the same
// way screenshots/demo-family.ts builds the demo family: every event, inline snapshot and index
// document exists exactly as production writes it. Runs in setup(), before the measured stages;
// seeding requests are tagged kind:seed, so they never count against the read/write thresholds,
// and any failure throws, which aborts the run with the API's error in the log.

export interface SeededChild {
  id: string;
  username: string;
  tokens: TokenSet;
  taskItemId: string;
  medicineId: string;
}

export interface SeededFamily {
  guardian: string;
  tokens: TokenSet;
  calendarId: string;
  children: SeededChild[];
  icalPaths: { calendar: string; mealplan: string; pickups: string };
}

type Response = RefinedResponse<ResponseType | undefined>;

interface Named {
  id: string;
}

interface Feed {
  subscriptionPath: string;
}

function fail(what: string, response: Response): never {
  throw new Error(`Seeding failed at ${what}: ${response.status} ${response.body}`);
}

function admin(method: string, path: string, body?: unknown): Response {
  const response = http.request(
    method,
    `${config.keycloakUrl}/admin/realms/${config.realm}${path}`,
    body === undefined ? null : JSON.stringify(body),
    {
      headers: { Authorization: `Bearer ${adminToken()}`, 'Content-Type': 'application/json' },
      tags: { name: 'keycloak-admin', kind: 'seed' },
    },
  );

  if (response.status >= 300) {
    fail(`Keycloak ${method} ${path}`, response);
  }

  return response;
}

function findKeycloakUserId(username: string): string | null {
  const users = admin(
    'GET',
    `/users?username=${encodeURIComponent(username)}&exact=true`,
  ).json() as unknown as Named[];
  return users[0]?.id ?? null;
}

// One user's API calls during seeding: strict (throws on any non-2xx), tagged kind:seed.
class SeedApi {
  constructor(private readonly token: string) {}

  call<T, M extends Method = Method, P extends RouteWith<M> = RouteWith<M>>(
    method: M,
    route: P,
    params: Params = {},
    body?: Body<P, M>,
    query?: string,
  ): T {
    const response = http.request(
      method.toUpperCase(),
      `${config.baseUrl}${pathOf(route, params, query)}`,
      body === undefined ? null : JSON.stringify(body),
      {
        headers: {
          Authorization: `Bearer ${this.token}`,
          'Content-Type': 'application/json',
          ...(method === 'post' ? { 'Idempotency-Key': crypto.randomUUID() } : {}),
        },
        tags: { name: 'seed', kind: 'seed' },
      },
    );

    if (response.status >= 300) {
      fail(`${method.toUpperCase()} ${route}`, response);
    }

    return (response.body ? response.json() : undefined) as T;
  }

  // Methods, not generic arrow-function fields: k6 2.3's TypeScript loader hangs on those.
  get<T>(route: RouteWith<'get'>, params?: Params): T {
    return this.call<T, 'get'>('get', route, params);
  }

  post<T, P extends RouteWith<'post'> = RouteWith<'post'>>(
    route: P,
    params: Params,
    body?: Body<P, 'post'>,
  ): T {
    return this.call<T, 'post', P>('post', route, params, body);
  }

  put<P extends RouteWith<'put'>>(route: P, params: Params, body?: Body<P, 'put'>, query?: string) {
    return this.call<unknown, 'put', P>('put', route, params, body, query);
  }

  patch<P extends RouteWith<'patch'>>(route: P, params: Params, body?: Body<P, 'patch'>) {
    return this.call<unknown, 'patch', P>('patch', route, params, body);
  }

  delete(route: RouteWith<'delete'>, params: Params) {
    return this.call<unknown, 'delete'>('delete', route, params);
  }
}

const pad = (n: number) => String(n).padStart(4, '0');
const daily = { frequency: 'Daily', intervalCount: 1, until: null } as const;
const weekly = { frequency: 'Weekly', intervalCount: 1, until: null } as const;

// 1-3 children, weighted towards 2: the per-child fan-out is what the dashboard journey measures.
const childCount = (index: number) => [1, 2, 2, 3][index % 4];

function seedChild(
  api: SeedApi,
  index: number,
  c: number,
  groupId: string,
  calendarId: string,
  guardianId: string,
) {
  const username = `${config.userPrefix}.c${pad(index)}${'abc'[c]}`;
  const { id } = api.post<Named, '/users/me/children'>(
    '/users/me/children',
    {},
    {
      givenName: `Child ${'ABC'[c]}`,
      familyName: `Family ${index}`,
      username,
      kind: 'Parent',
    },
  );
  const child = { childId: id };

  // CreateChild gives the child a temporary password the direct grant can't complete; a
  // non-temporary reset drops UPDATE_PASSWORD (the same fix as demo-family.ts).
  const keycloakId = findKeycloakUserId(username);
  if (!keycloakId) {
    throw new Error(`Keycloak user ${username} not found after CreateChild`);
  }
  admin('PUT', `/users/${keycloakId}/reset-password`, {
    type: 'password',
    value: config.password,
    temporary: false,
  });

  api.patch('/users/me/children/{childId}/timezone', child, { timeZoneId: config.timeZone });
  api.put('/groups/{groupId}/children/{childId}', { groupId, childId: id });

  const task = api.post<Named, '/calendars/{calendarId}/items'>(
    '/calendars/{calendarId}/items',
    { calendarId },
    {
      title: 'Brush teeth',
      icon: '🪥',
      color: '#0ea5e9',
      schedule: {
        kind: 1,
        dueDate: { date: daysFrom(-60), time: '07:15:00' },
        isAllDay: false,
        assignedTo: id,
      },
      recurrence: daily,
    },
  );

  const medicine = api.post<Named, '/medicines/children/{childId}/schedules'>(
    '/medicines/children/{childId}/schedules',
    child,
    {
      name: 'Methylphenidate',
      dosage: '10 mg',
      icon: '💊',
      color: '#0ea5e9',
      times: ['07:30:00'],
      startDate: daysFrom(-60),
      endDate: null,
    },
  );

  const meal = api.post<Named, '/mealplans/children/{childId}/meals'>(
    '/mealplans/children/{childId}/meals',
    child,
    {
      name: 'Oatmeal with berries',
      icon: '🥣',
      color: '#f59e0b',
      description: null,
    },
  );
  for (let day = -3; day <= 7; day++) {
    api.put(
      '/mealplans/children/{childId}/plan',
      child,
      { mealId: meal.id, notes: null },
      `date=${daysFrom(day)}&slot=Breakfast`,
    );
  }

  for (let day = 0; day <= 4; day++) {
    api.put(
      '/pickups/children/{childId}/assignments',
      child,
      { assignee: { kind: 0, guardianId }, time: '15:00:00', notes: null },
      `date=${daysFrom(day)}&slot=PickUp`,
    );
  }

  api.put('/first-then/children/{childId}', child, {
    revision: 0,
    first: { title: 'Homework', icon: 'pictogram:homework' },
    then: { title: 'iPad', icon: 'pictogram:screen-time' },
    thenMinutes: 20,
  });

  // Optional history (SEED_HISTORY_DAYS): long streams, so the soak profile can show whether
  // inline snapshots keep rehydration flat as streams grow (event-stream-snapshots.md).
  for (let back = 1; back <= config.historyDays; back++) {
    api.put(
      '/medicines/children/{childId}/doses/{medicineId}',
      { childId: id, medicineId: medicine.id },
      { status: 'Taken' },
      `date=${daysFrom(-back)}&time=07:30:00`,
    );
    api.put(
      '/sleep-diary/children/{childId}/entries/{date}',
      { childId: id, date: daysFrom(-back) },
      sleepEntry(back),
    );
  }

  return { id, username, taskItemId: task.id, medicineId: medicine.id };
}

export function seedFamily(index: number): SeededFamily {
  const guardian = `${config.userPrefix}.g${pad(index)}`;

  admin('POST', '/users', {
    username: guardian,
    email: `${guardian}@load.buddy.test`,
    emailVerified: true,
    enabled: true,
    firstName: 'Load',
    lastName: `Family ${index}`,
    // Without it Buddy turns the account away (the buddy-guardian Keycloak group).
    groups: ['/buddy-guardian'],
    credentials: [{ type: 'password', value: config.password, temporary: false }],
  });

  const tokens = login(guardian);
  const api = new SeedApi(tokens.accessToken);
  const me = api.get<Named>('/users/me');
  api.patch('/users/me/timezone', {}, { timeZoneId: config.timeZone });

  const group = api.post<Named, '/groups'>('/groups', {}, { name: `Load family ${index}` });
  const calendar = api.post<Named, '/calendars'>(
    '/calendars',
    {},
    {
      name: 'Family',
      timeZoneId: config.timeZone,
      groupId: group.id,
      icon: '🏡',
    },
  );
  api.post(
    '/calendars/{calendarId}/items',
    { calendarId: calendar.id },
    {
      title: 'Football practice',
      icon: '⚽',
      color: '#10b981',
      schedule: {
        kind: 0,
        startsAt: { date: daysFrom(-30), time: '16:00:00' },
        endsAt: { date: daysFrom(-30), time: '17:30:00' },
        isAllDay: false,
      },
      recurrence: weekly,
    },
  );

  const children = Array.from({ length: childCount(index) }, (_, c) =>
    seedChild(api, index, c, group.id, calendar.id, me.id),
  );

  const calendarFeed = api.post<Feed>('/calendars/{calendarId}/ical-tokens', {
    calendarId: calendar.id,
  });
  const mealplanFeed = api.post<Feed>('/mealplans/children/{childId}/ical-tokens', {
    childId: children[0].id,
  });
  const pickupFeed = api.post<Feed>('/pickups/me/ical-tokens', {});

  return {
    guardian,
    tokens,
    calendarId: calendar.id,
    children: children.map((child) => ({ ...child, tokens: login(child.username) })),
    icalPaths: {
      calendar: calendarFeed.subscriptionPath,
      mealplan: mealplanFeed.subscriptionPath,
      pickups: pickupFeed.subscriptionPath,
    },
  };
}

export function seedFamilies(count: number): SeededFamily[] {
  const families: SeededFamily[] = [];

  for (let i = 1; i <= count; i++) {
    families.push(seedFamily(i));

    if (i % 25 === 0 || i === count) {
      console.log(`seeded ${i}/${count} families (${config.userPrefix})`);
    }
  }

  return families;
}

// A night in the sleep diary, varied a little by the day, in the shape demo-family.ts logs.
export function sleepEntry(
  seed: number,
): Body<'/sleep-diary/children/{childId}/entries/{date}', 'put'> {
  const late = seed % 3 === 0;
  return {
    routineStartTime: '19:00',
    ritualStartTime: '19:20',
    ritualEndTime: '19:50',
    bedTime: late ? '20:30' : '20:00',
    fellAsleepTime: late ? '21:15' : '20:30',
    nightWakeUps: late ? [{ startTime: '02:10', durationMinutes: 20 }] : [],
    morningWakeTime: '06:30',
    isTired: late,
    naps: [],
    totalSleepMinutes: late ? 535 : 600,
    remarks: '',
  };
}

// Removes what seedFamilies created: each child (DeleteChild), the family group (and with it the
// calendar), the guardian's account, then any Keycloak identity left with the run's prefix. Best
// effort: a failure is logged, never thrown, so one leftover doesn't hide the run's results.
export function cleanupFamilies(families: SeededFamily[], userPrefix: string): void {
  for (const family of families) {
    try {
      const api = new SeedApi(login(family.guardian).accessToken);

      for (const child of family.children) {
        api.delete('/users/me/children/{childId}', { childId: child.id });
      }
      for (const group of api.get<Named[]>('/groups')) {
        api.delete('/groups/{groupId}', { groupId: group.id });
      }
      api.delete('/users/me', {});
    } catch (error) {
      console.warn(`cleanup of ${family.guardian} failed: ${String(error)}`);
    }
  }

  try {
    const leftovers = admin(
      'GET',
      `/users?search=${encodeURIComponent(userPrefix)}&max=1000`,
    ).json() as unknown as {
      id: string;
      username: string;
    }[];

    for (const user of leftovers.filter((u) => u.username.startsWith(userPrefix))) {
      admin('DELETE', `/users/${user.id}`);
    }
  } catch (error) {
    console.warn(`Keycloak cleanup for ${userPrefix} failed: ${String(error)}`);
  }
}
