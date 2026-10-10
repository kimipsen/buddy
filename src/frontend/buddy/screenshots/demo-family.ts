import { type APIRequestContext, request } from '@playwright/test';

import type { DemoFamily } from './pages';

// Seeds the "Holm" demo family that the documentation screenshots show: one guardian (Sara) with
// two children (Emil, Ida), a family group and calendar, tasks, meals, medicine, pickups, work
// locations, progress goals and a print template. Everything goes through the real API as Sara,
// so the screenshots show what a guardian would actually see.
//
// The demo accounts are fixed usernames (DEMO_USERNAMES) that never collide with the seeded
// alice/bob/carol accounts the e2e specs rely on. Every run first removes the previous run's demo
// family (resetDemoFamily) and then builds it fresh, so dates stay relative to today and repeated
// runs don't pile up data.

export const KEYCLOAK_URL = process.env['KEYCLOAK_URL'] ?? 'http://keycloak:8080';
export const API_URL = process.env['BUDDY_API_URL'] ?? 'https://localhost:7076';
const REALM = 'buddy';
const CLIENT_ID = 'buddy-frontend';
const TIME_ZONE = 'Europe/Copenhagen';

// Local-dev-only credentials: the Keycloak master bootstrap admin from .devcontainer/.env (the
// same one e2e/support/keycloak-admin-client.ts uses) and a fixed password for the demo users.
const MASTER_ADMIN = { username: 'admin', password: 'admin' };
export const DEMO_PASSWORD = 'demo-screenshots-pw';

export const DEMO_USERNAMES = {
  guardian: 'demo.sara',
  child: 'demo.emil',
  sibling: 'demo.ida',
} as const;

interface Named {
  id: string;
}

interface TaskTemplateDto extends Named {
  subtasks: Named[];
}

// ---- Dates -------------------------------------------------------------------------------------

function isoDate(date: Date): string {
  const y = date.getFullYear();
  const m = String(date.getMonth() + 1).padStart(2, '0');
  const d = String(date.getDate()).padStart(2, '0');
  return `${y}-${m}-${d}`;
}

function addDays(date: Date, days: number): Date {
  const copy = new Date(date);
  copy.setDate(copy.getDate() + days);
  return copy;
}

const today = new Date();
// Monday of the current week (getDay: 0 = Sunday).
const monday = addDays(today, -((today.getDay() + 6) % 7));
const mondayIso = isoDate(monday);
const todayIso = isoDate(today);
const inDays = (days: number) => isoDate(addDays(today, days));
// Three weeks from this week's Monday: covers the pages that show the next seven days and the
// print sheet, which opens on next week.
const planDays = Array.from({ length: 21 }, (_, i) => addDays(monday, i));
const isWeekday = (date: Date) => date.getDay() >= 1 && date.getDay() <= 5;
const weekdayOf = (iso: string) => new Date(`${iso}T12:00:00`).getDay();
const nextWeekday = (weekday: number) => inDays((weekday - today.getDay() + 7) % 7);

// ---- Keycloak ----------------------------------------------------------------------------------

interface TokenResponse {
  access_token: string;
  refresh_token?: string;
  id_token?: string;
  expires_in: number;
}

async function requestToken(
  username: string,
  password: string,
  realm = REALM,
  clientId = CLIENT_ID,
): Promise<TokenResponse> {
  const response = await fetch(`${KEYCLOAK_URL}/realms/${realm}/protocol/openid-connect/token`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
    body: new URLSearchParams({
      grant_type: 'password',
      client_id: clientId,
      username,
      password,
      scope: 'openid profile email',
    }),
  });

  if (!response.ok) {
    throw new Error(`Token request for '${username}' failed: ${response.status}`);
  }

  return (await response.json()) as TokenResponse;
}

// The TokenSet the app keeps in sessionStorage['buddy_keycloak_tokens'] (src/app/core/token-storage.ts),
// minted by direct grant: the same fast path as the e2e loginAs fixture.
export async function browserTokenSet(username: string, password: string) {
  const body = await requestToken(username, password);

  return {
    accessToken: body.access_token,
    refreshToken: body.refresh_token ?? null,
    idToken: body.id_token ?? null,
    expiresAt: Date.now() + body.expires_in * 1000,
  };
}

async function keycloakAdmin(path: string, init: RequestInit = {}): Promise<Response> {
  const { access_token: token } = await requestToken(
    MASTER_ADMIN.username,
    MASTER_ADMIN.password,
    'master',
    'admin-cli',
  );
  return fetch(`${KEYCLOAK_URL}/admin/realms/${REALM}${path}`, {
    ...init,
    headers: {
      'Content-Type': 'application/json',
      Authorization: `Bearer ${token}`,
      ...init.headers,
    },
  });
}

async function findKeycloakUserId(username: string): Promise<string | null> {
  const response = await keycloakAdmin(
    `/users?username=${encodeURIComponent(username)}&exact=true`,
  );
  const users = (await response.json()) as { id: string }[];
  return users[0]?.id ?? null;
}

async function createGuardianIdentity(): Promise<void> {
  const response = await keycloakAdmin('/users', {
    method: 'POST',
    body: JSON.stringify({
      username: DEMO_USERNAMES.guardian,
      email: 'sara.holm@buddy.test',
      emailVerified: true,
      enabled: true,
      firstName: 'Sara',
      lastName: 'Holm',
      credentials: [{ type: 'password', value: DEMO_PASSWORD, temporary: false }],
    }),
  });

  if (!response.ok) {
    throw new Error(`Creating Keycloak user ${DEMO_USERNAMES.guardian} failed: ${response.status}`);
  }
}

// CreateChild gives a child a temporary password with a pending UPDATE_PASSWORD action, which the
// direct-grant login can't complete. Swap it for a permanent one so the child pages can be shown;
// a non-temporary reset also drops UPDATE_PASSWORD. No user PUT: under Keycloak's user profile a
// partial PUT blanks firstName/lastName, and VERIFY_PROFILE then rejects the direct grant.
async function makeChildLoginUsable(username: string): Promise<void> {
  const id = await findKeycloakUserId(username);

  if (!id) {
    throw new Error(`Keycloak user ${username} not found after CreateChild`);
  }

  await keycloakAdmin(`/users/${id}/reset-password`, {
    method: 'PUT',
    body: JSON.stringify({ type: 'password', value: DEMO_PASSWORD, temporary: false }),
  });
}

// ---- API ---------------------------------------------------------------------------------------

class Api {
  private constructor(
    private readonly context: APIRequestContext,
    private readonly token: string,
  ) {}

  static async as(username: string, password: string): Promise<Api> {
    const context = await request.newContext({ baseURL: API_URL, ignoreHTTPSErrors: true });
    return new Api(context, (await requestToken(username, password)).access_token);
  }

  async send<T>(method: string, path: string, data?: unknown): Promise<T> {
    const response = await this.context.fetch(path, {
      method,
      data,
      headers: {
        Authorization: `Bearer ${this.token}`,
        ...(method === 'POST' ? { 'Idempotency-Key': crypto.randomUUID() } : {}),
      },
    });

    if (!response.ok()) {
      throw new Error(`${method} ${path} failed: ${response.status()} ${await response.text()}`);
    }

    const text = await response.text();
    return (text ? JSON.parse(text) : undefined) as T;
  }

  get = <T>(path: string) => this.send<T>('GET', path);
  post = <T>(path: string, data?: unknown) => this.send<T>('POST', path, data);
  put = <T>(path: string, data?: unknown) => this.send<T>('PUT', path, data);
  patch = <T>(path: string, data?: unknown) => this.send<T>('PATCH', path, data);
  delete = <T>(path: string) => this.send<T>('DELETE', path);

  dispose = () => this.context.dispose();
}

// Optional extras (a completed task, a taken dose, invites) only make the screenshots richer, so a
// failure there is logged rather than failing the whole run.
function warn(error: unknown): void {
  console.warn(`[demo seed] ${error instanceof Error ? error.message : String(error)}`);
}

// ---- Mailpit -----------------------------------------------------------------------------------

const MAILPIT_URL = process.env['MAILPIT_BASE_URL'] ?? 'http://mailpit:8025';

// Invite links look like <FrontendBaseUrl>/invite/<token> or /guardian-invite/<token>. Returns
// null rather than failing: the invite pages are a nice-to-have in the screenshot set.
async function inviteTokenFromMail(to: string, pathSegment: string): Promise<string | null> {
  for (let attempt = 0; attempt < 10; attempt++) {
    const search = await fetch(
      `${MAILPIT_URL}/api/v1/search?query=${encodeURIComponent(`to:${to}`)}`,
    ).catch(() => null);
    const messages = search?.ok
      ? ((await search.json()) as { messages: { ID: string }[] }).messages
      : [];

    for (const { ID } of messages) {
      const message = (await (await fetch(`${MAILPIT_URL}/api/v1/message/${ID}`)).json()) as {
        Text: string;
      };
      const match = new RegExp(`/${pathSegment}/([A-Za-z0-9_-]+)`).exec(message.Text);

      if (match) {
        return match[1];
      }
    }

    await new Promise((resolve) => setTimeout(resolve, 500));
  }

  return null;
}

async function deleteMail(to: string): Promise<void> {
  await fetch(`${MAILPIT_URL}/api/v1/search?query=${encodeURIComponent(`to:${to}`)}`, {
    method: 'DELETE',
  }).catch(() => undefined);
}

// ---- Reset -------------------------------------------------------------------------------------

// Removes the previous run's demo family: Sara's groups (and with them their calendars), her
// links to the children, her backend account, and all three Keycloak identities.
export async function resetDemoFamily(): Promise<void> {
  if (await findKeycloakUserId(DEMO_USERNAMES.guardian)) {
    const api = await Api.as(DEMO_USERNAMES.guardian, DEMO_PASSWORD).catch(() => null);

    if (api) {
      try {
        for (const group of await api.get<Named[]>('/groups')) {
          await api.delete(`/groups/${group.id}`).catch(warn);
        }

        for (const child of await api.get<Named[]>('/users/me/children')) {
          await api.delete(`/users/me/children/${child.id}/guardian-link`).catch(warn);
        }

        await api.delete('/users/me').catch(warn);
      } finally {
        await api.dispose();
      }
    }
  }

  for (const username of Object.values(DEMO_USERNAMES)) {
    const id = await findKeycloakUserId(username);

    if (id) {
      await keycloakAdmin(`/users/${id}`, { method: 'DELETE' });
    }
  }

  await deleteMail('demo.invitee@buddy.test');
  await deleteMail('demo.coparent@buddy.test');
}

// ---- Seed --------------------------------------------------------------------------------------

export async function seedDemoFamily(): Promise<DemoFamily> {
  await resetDemoFamily();
  await createGuardianIdentity();

  const api = await Api.as(DEMO_USERNAMES.guardian, DEMO_PASSWORD);

  try {
    const me = await api.get<{ id: string }>('/users/me');
    await api.patch('/users/me/name', { givenName: 'Sara', familyName: 'Holm' });
    await api.patch('/users/me/timezone', { timeZoneId: TIME_ZONE });
    await api.patch('/users/me/language', { language: 'en' });

    // Children ------------------------------------------------------------------------------
    const emil = await api.post<Named>('/users/me/children', {
      givenName: 'Emil',
      familyName: 'Holm',
      username: DEMO_USERNAMES.child,
      kind: 'Parent',
    });
    const ida = await api.post<Named>('/users/me/children', {
      givenName: 'Ida',
      familyName: 'Holm',
      username: DEMO_USERNAMES.sibling,
      kind: 'Parent',
    });
    await makeChildLoginUsable(DEMO_USERNAMES.child);

    for (const child of [emil, ida]) {
      await api.patch(`/users/me/children/${child.id}/timezone`, { timeZoneId: TIME_ZONE });
    }

    // Group and calendars ------------------------------------------------------------------
    const group = await api.post<Named>('/groups', { name: 'Holm family' });
    await api.put(`/groups/${group.id}/children/${emil.id}`);
    await api.put(`/groups/${group.id}/children/${ida.id}`);

    const family = await api.post<Named>('/calendars', {
      name: 'Family',
      timeZoneId: TIME_ZONE,
      groupId: group.id,
      icon: '🏡',
    });
    const school = await api.post<Named>('/calendars', {
      name: 'School & activities',
      timeZoneId: TIME_ZONE,
      groupId: group.id,
      icon: '🎒',
    });

    const event = (
      calendarId: string,
      title: string,
      icon: string,
      color: string,
      date: string,
      start: string,
      end: string,
      recurrence: unknown = null,
    ) =>
      api.post(`/calendars/${calendarId}/items`, {
        title,
        icon,
        color,
        schedule: {
          kind: 0,
          startsAt: { date, time: start },
          endsAt: { date, time: end },
          isAllDay: false,
        },
        recurrence,
      });

    const weekly = { frequency: 'Weekly', intervalCount: 1, until: null };
    await event(
      school.id,
      'Football practice',
      '⚽',
      '#10b981',
      nextWeekday(2),
      '16:00:00',
      '17:30:00',
      weekly,
    );
    await event(
      school.id,
      'Swimming',
      '🏊',
      '#0ea5e9',
      nextWeekday(4),
      '15:30:00',
      '16:30:00',
      weekly,
    );
    await event(family.id, 'Dentist – Emil', '🦷', '#f43f5e', todayIso, '14:00:00', '14:45:00');
    await event(family.id, 'Grandma visits', '👵', '#f59e0b', inDays(2), '11:00:00', '15:00:00');
    await event(
      school.id,
      'Parent–teacher meeting',
      '🏫',
      '#6366f1',
      inDays(4),
      '17:00:00',
      '18:00:00',
    );

    // Task library and scheduled tasks ------------------------------------------------------
    const morning = await api.post<Named>(`/task-templates/children/${emil.id}`, {
      name: 'Morning routine',
      icon: '🌅',
      color: '#f97316',
    });
    let morningSubtasks: Named[] = [];
    for (const [title, icon, duration] of [
      ['Get dressed', '👕', '00:05:00'],
      ['Eat breakfast', '🥣', '00:15:00'],
      ['Brush teeth', '🪥', '00:03:00'],
      ['Pack school bag', '🎒', '00:05:00'],
    ]) {
      ({ subtasks: morningSubtasks } = await api.post<TaskTemplateDto>(
        `/task-templates/${morning.id}/subtasks`,
        { title, icon, duration },
      ));
    }

    const bedtime = await api.post<Named>(`/task-templates/children/${emil.id}`, {
      name: 'Bedtime',
      icon: '🌙',
      color: '#6366f1',
    });
    for (const [title, icon, duration] of [
      ['Bath', '🛁', '00:15:00'],
      ['Pyjamas on', '🩳', '00:05:00'],
      ['Read a story', '📖', '00:15:00'],
    ]) {
      await api.post(`/task-templates/${bedtime.id}/subtasks`, { title, icon, duration });
    }

    const daily = { frequency: 'Daily', intervalCount: 1, until: null };
    const morningItem = await api.post<Named>(`/calendars/${family.id}/items/from-template`, {
      taskTemplateId: morning.id,
      startDate: mondayIso,
      startTime: '07:00:00',
      title: 'Morning routine',
      color: '#f97316',
      icon: '🌅',
      recurrence: daily,
      assignedTo: emil.id,
    });
    await api.post(`/calendars/${family.id}/items/from-template`, {
      taskTemplateId: bedtime.id,
      startDate: mondayIso,
      startTime: '19:30:00',
      title: 'Bedtime',
      color: '#6366f1',
      icon: '🌙',
      recurrence: daily,
      assignedTo: emil.id,
    });
    await api.post(`/calendars/${family.id}/items`, {
      title: 'Tidy your room',
      icon: '🧸',
      color: '#84cc16',
      schedule: {
        kind: 1,
        dueDate: { date: todayIso, time: '17:00:00' },
        isAllDay: false,
        assignedTo: emil.id,
      },
      recurrence: null,
    });

    // Mornings done so far this week (template tasks complete per subtask), so Emil has stars.
    for (const date of planDays.map(isoDate).filter((d) => d <= todayIso)) {
      for (const subtask of morningSubtasks) {
        await api
          .patch(
            `/calendars/${family.id}/items/${morningItem.id}/subtasks/${subtask.id}/completion`,
            {
              date,
              isCompleted: true,
            },
          )
          .catch(warn);
      }
    }

    await api.put(`/progress/children/${emil.id}/goals`, {
      goalPosts: [
        { threshold: 5, icon: '🍦', label: 'Ice cream' },
        { threshold: 15, icon: '🎬', label: 'Cinema trip' },
        { threshold: 30, icon: '🧩', label: 'New Lego set' },
      ],
    });

    // Rewards: one already approved and one waiting, so both the child's page and the guardian's
    // show requests. Emil has at least four stars (one Monday morning's subtasks), enough for both;
    // the third is out of reach. Optional, like the subtask completions above.
    try {
      const { rewards } = await api.put<{ rewards: Named[] }>(
        `/progress/children/${emil.id}/rewards`,
        {
          rewards: [
            { name: 'Extra screen time', icon: '📱', cost: 2 },
            { name: 'Choose Friday’s dinner', icon: '🍕', cost: 2 },
            { name: 'Cinema trip', icon: '🎬', cost: 50 },
          ],
        },
      );
      const emilApi = await Api.as(DEMO_USERNAMES.child, DEMO_PASSWORD);
      const approved = await emilApi.post<{ rewardRequests: Named[] }>(
        '/progress/me/reward-requests',
        { rewardId: rewards[0].id },
      );
      await api.post(
        `/progress/children/${emil.id}/reward-requests/${approved.rewardRequests[0].id}/approve`,
      );
      await emilApi.post('/progress/me/reward-requests', { rewardId: rewards[1].id });
      await emilApi.dispose();
    } catch (error: unknown) {
      warn(error);
    }

    // Meal plan -----------------------------------------------------------------------------
    const meals: Record<string, Named> = {};
    for (const [name, icon, color, description] of [
      ['Oatmeal with berries', '🥣', '#f59e0b', 'With a spoon of honey'],
      ['Rye bread sandwiches', '🥪', '#84cc16', null],
      ['Spaghetti bolognese', '🍝', '#f43f5e', 'Emil’s favourite'],
      ['Fish fingers & potatoes', '🐟', '#0ea5e9', null],
      ['Chicken tacos', '🌮', '#f97316', 'Taco Friday'],
      ['Apple and carrot sticks', '🍎', '#10b981', null],
    ] as const) {
      meals[name] = await api.post<Named>(`/mealplans/children/${emil.id}/meals`, {
        name,
        icon,
        color,
        description,
      });
    }

    // Indexed by getDay (0 = Sunday).
    const dinners = [
      'Spaghetti bolognese',
      'Fish fingers & potatoes',
      'Spaghetti bolognese',
      'Fish fingers & potatoes',
      'Spaghetti bolognese',
      'Chicken tacos',
      'Chicken tacos',
    ];
    for (const day of planDays) {
      const assign = (slot: 'Breakfast' | 'Lunch' | 'Dinner' | 'Snack', meal: string) =>
        api.put(`/mealplans/children/${emil.id}/plan?date=${isoDate(day)}&slot=${slot}`, {
          mealId: meals[meal].id,
          notes: null,
        });
      await assign('Breakfast', 'Oatmeal with berries');
      if (isWeekday(day)) await assign('Lunch', 'Rye bread sandwiches');
      await assign('Dinner', dinners[day.getDay()]);
      await assign('Snack', 'Apple and carrot sticks');
    }

    // One earlier import, so the import page's history has a row to show.
    await api.post(`/mealplans/children/${emil.id}/imports`, {
      format: 'weekly-note',
      archiveSingleUse: true,
      entries: [
        'Spaghetti bolognese',
        'Fish fingers & potatoes',
        'Pancakes',
        'Chicken tacos',
        'Rye bread sandwiches',
        'Homemade pizza',
        'Spaghetti bolognese',
      ].map((meal, index) => ({
        date: `2025-03-0${index + 2}`,
        slot: 'Dinner',
        ...(meals[meal] ? { mealId: meals[meal].id } : { newMealName: meal }),
        notes: '',
      })),
    });

    // AI assistant: a fake key (storing one never calls the provider; the first key becomes the
    // active one) and the data-sharing acknowledgement, so the assistant page shows its start form
    // instead of the "add a key" prompt. Nothing here ever sends a chat message.
    await api.put(`/mealplans/children/${emil.id}/ai/providers/Anthropic/key`, {
      apiKey: 'sk-ant-demo-screenshots-not-a-real-key',
    });
    await api.put(`/mealplans/children/${emil.id}/ai/data-sharing-acknowledgement`);

    // Medicine ------------------------------------------------------------------------------
    const ritalin = await api.post<Named>(`/medicines/children/${emil.id}/schedules`, {
      name: 'Methylphenidate',
      dosage: '10 mg',
      icon: '💊',
      color: '#0ea5e9',
      times: ['07:30:00', '12:00:00'],
      startDate: mondayIso,
      endDate: null,
    });
    await api.post(`/medicines/children/${emil.id}/schedules`, {
      name: 'Melatonin',
      dosage: '3 mg',
      icon: '🌙',
      color: '#6366f1',
      times: ['19:00:00'],
      startDate: mondayIso,
      endDate: null,
    });
    await api
      .put(`/medicines/children/${emil.id}/doses/${ritalin.id}?date=${todayIso}&time=07:30:00`, {
        status: 'Taken',
      })
      .catch(warn);

    // Sleep diary ---------------------------------------------------------------------------
    // Ten nights ending last night (the form opens on tonight, still blank), varied a little so
    // the history and the shared table read like a real fortnight.
    const nights = [
      { bed: '20:00', asleep: '20:30', wake: '06:30', wakeUps: [], total: 600, tired: false },
      {
        bed: '20:15',
        asleep: '21:00',
        wake: '06:45',
        wakeUps: [['02:10', 20]],
        total: 565,
        tired: false,
      },
      {
        bed: '20:00',
        asleep: '20:40',
        wake: '06:10',
        wakeUps: [
          ['03:30', 30],
          ['05:00', 10],
        ],
        total: 530,
        tired: true,
      },
      { bed: '19:45', asleep: '20:15', wake: '06:30', wakeUps: [], total: 615, tired: false },
      {
        bed: '20:30',
        asleep: '21:30',
        wake: '07:00',
        wakeUps: [['01:00', 15]],
        total: 555,
        tired: true,
      },
    ] as const;
    for (let back = 1; back <= 10; back++) {
      const night = nights[back % nights.length];
      await api.put(`/sleep-diary/children/${emil.id}/entries/${inDays(-back)}`, {
        routineStartTime: '19:00',
        ritualStartTime: '19:20',
        ritualEndTime: '19:50',
        bedTime: night.bed,
        fellAsleepTime: night.asleep,
        nightWakeUps: night.wakeUps.map(([startTime, durationMinutes]) => ({
          startTime,
          durationMinutes,
        })),
        morningWakeTime: night.wake,
        isTired: night.tired,
        naps: [],
        totalSleepMinutes: night.total,
        remarks: night.tired ? 'Took a long time to settle after the ritual.' : '',
      });
    }
    await api.put(`/sleep-diary/children/${emil.id}/hygiene-notes`, {
      notes: 'No screens after 18:30. Blackout curtains. Same bedtime song every night.',
    });
    const sleepShare = await api
      .post<{ token: string }>(`/sleep-diary/children/${emil.id}/share-links`, {
        expiresAt: new Date(Date.now() + 30 * 24 * 60 * 60 * 1000).toISOString(),
      })
      .catch((error: unknown) => {
        warn(error);
        return null;
      });

    // House rules ---------------------------------------------------------------------------
    // A household book for the family group and one personal rule for Emil. Ida has read every
    // household rule (Sara went through them with her); Emil read "Dinner" before it changed and
    // hasn't read the others, so the guardian page and his page show "new" and "changed".
    interface RuleBookDto {
      rules: { id: string; title: string; revision: number }[];
    }
    const householdRules = `/house-rules/groups/${group.id}/rules`;
    await api.post(householdRules, {
      title: 'Screen time',
      body: [
        '| Day | Time | When |',
        '|:---|:---:|---|',
        '| Mon–Thu | 45 min | after homework |',
        '| Fri–Sun | 90 min | after lunch |',
        '',
        '- Tablet goes on the charger in the kitchen at **19:30**.',
        '- One reminder, then a 5-minute warning, then it’s off.',
      ].join('\n'),
    });
    await api.post(householdRules, {
      title: 'Dinner',
      body: 'Phones stay in the basket by the door.',
    });
    const household = await api.post<RuleBookDto>(householdRules, {
      title: 'Bedtime',
      body: '1. Pyjamas and teeth\n2. One story\n3. Lights out at **20:00**',
    });
    const dinner = household.rules.find((rule) => rule.title === 'Dinner');
    for (const rule of household.rules) {
      await api
        .put(`${householdRules}/${rule.id}/acknowledgement`, {
          revision: rule.revision,
          childId: ida.id,
        })
        .catch(warn);
    }
    if (dinner) {
      await api
        .put(`${householdRules}/${dinner.id}/acknowledgement`, { revision: 1, childId: emil.id })
        .catch(warn);
      await api
        .put(`${householdRules}/${dinner.id}`, {
          title: 'Dinner',
          body: 'Phones stay in the basket by the door. **Ask** before you leave the table.',
          requireReacknowledgement: true,
        })
        .catch(warn);
      await api
        .put(`${householdRules}/${dinner.id}/acknowledgement`, { revision: 2, childId: ida.id })
        .catch(warn);
    }
    await api.post(`/house-rules/children/${emil.id}/rules`, {
      title: 'Gaming',
      body: '- [x] Homework first\n- [ ] 45 minutes on school days\n- [ ] Stop when the timer rings',
    });

    // Work locations ------------------------------------------------------------------------
    const office = await api.post<Named>('/work-locations/me/locations', {
      name: 'Office',
      icon: '🏢',
      color: '#6366f1',
    });
    const home = await api.post<Named>('/work-locations/me/locations', {
      name: 'Home office',
      icon: '🏠',
      color: '#10b981',
    });
    await api.put('/work-locations/me/pattern', {
      cycleWeeks: 1,
      anchorMonday: mondayIso,
      days: [
        { week: 0, day: 'Monday', locationId: office.id },
        { week: 0, day: 'Tuesday', locationId: home.id },
        { week: 0, day: 'Wednesday', locationId: office.id },
        { week: 0, day: 'Thursday', locationId: office.id },
        { week: 0, day: 'Friday', locationId: home.id },
      ],
    });

    // Babysitters ---------------------------------------------------------------------------
    const maja = await api.post<Named>('/babysitters/me', {
      name: 'Maja',
      contactInfo: '+45 20 30 40 50',
    });
    await api.post('/babysitters/me', { name: 'Freja', contactInfo: 'freja@example.com' });

    // Pickups -------------------------------------------------------------------------------
    for (const date of planDays.filter(isWeekday).map(isoDate)) {
      const weekday = weekdayOf(date);
      await api.put(`/pickups/children/${emil.id}/assignments?date=${date}&slot=DropOff`, {
        assignee: { kind: 0, guardianId: me.id },
        time: '07:45:00',
        notes: null,
      });
      await api.put(`/pickups/children/${emil.id}/assignments?date=${date}&slot=PickUp`, {
        assignee:
          weekday === 4
            ? { kind: 3, hostName: 'Oscar’s family', location: 'Birkevej 12', contactInfo: '' }
            : weekday === 3
              ? { kind: 4, guardianId: me.id, babysitterId: maja.id }
              : { kind: 0, guardianId: me.id },
        time: weekday === 2 || weekday === 4 ? '17:30:00' : '15:00:00',
        notes: weekday === 2 ? 'After football' : null,
      });
    }

    // Print template ------------------------------------------------------------------------
    const row = (fields: Record<string, unknown>) => ({
      childId: null,
      mealGroupId: null,
      mealSlot: null,
      guardianId: null,
      workLocationId: null,
      calendarIds: null,
      assignedToId: null,
      titleFilter: null,
      maxItems: null,
      showTime: false,
      showAssignee: false,
      heightWeight: 1,
      ...fields,
    });
    const template = await api.post<Named>('/print-templates', {
      name: 'Fridge week plan',
      groupId: group.id,
    });
    await api.put(`/print-templates/${template.id}/rows`, {
      rows: [
        row({
          kind: 'CalendarEvents',
          label: 'Activities',
          calendarIds: [school.id],
          showTime: true,
          heightWeight: 2,
        }),
        row({ kind: 'Pickup', label: 'Pickup', childId: emil.id }),
        row({ kind: 'Meal', label: 'Dinner', childId: emil.id, mealSlot: 'Dinner' }),
        row({ kind: 'WorkLocation', label: 'Sara works', guardianId: me.id }),
        row({
          kind: 'TaskChecklist',
          label: 'Emil’s tasks',
          calendarIds: [family.id],
          assignedToId: emil.id,
          heightWeight: 2,
        }),
      ],
    });
    await api.put(`/print-templates/${template.id}/babysitter-colors`, {
      colors: [{ guardianId: me.id, babysitterId: maja.id, color: '#a855f7' }],
    });

    // Invites, so the public invite pages have something to preview -------------------------
    await api
      .post(`/groups/${group.id}/invites`, { email: 'demo.invitee@buddy.test', role: 'Member' })
      .catch(warn);
    await api
      .post(`/users/me/children/${emil.id}/guardian-invites`, {
        email: 'demo.coparent@buddy.test',
        kind: 'Parent',
      })
      .catch(warn);

    return {
      guardian: { username: DEMO_USERNAMES.guardian, password: DEMO_PASSWORD },
      child: { username: DEMO_USERNAMES.child, password: DEMO_PASSWORD },
      printTemplateId: template.id,
      groupInviteToken: await inviteTokenFromMail('demo.invitee@buddy.test', 'invite'),
      guardianInviteToken: await inviteTokenFromMail('demo.coparent@buddy.test', 'guardian-invite'),
      sleepDiaryShareToken: sleepShare?.token ?? null,
      childId: emil.id,
      groupId: group.id,
    };
  } finally {
    await api.dispose();
  }
}
