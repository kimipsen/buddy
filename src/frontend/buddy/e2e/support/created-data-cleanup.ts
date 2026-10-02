import { type APIRequestContext, request } from '@playwright/test';

import { type DisposableGuardian, deleteKeycloakUser } from './keycloak-admin-client';
import { getAccessToken } from './keycloak-client';
import { readRuntimeConfig } from './runtime-config';
import { SEEDED_GUARDIANS, type TestUser, isSeededUsername } from './seeded-users';

// Per-test cleanup for the data guardian-data.ts's createChild()/createGroup()/createCalendar()
// make.
//
// Why: every spec runs as the shared seeded guardians (alice/bob/carol) against the same
// persistent dev database, and nothing used to be removed -- the seeded users piled up ~75-140
// "E2eChild..." children and ~100 "E2eGroup..." groups / ~50 calendars each. Pages that fan out
// per item (the guardian dashboard per child, /guardian/admin per group) then got slow enough to
// time specs out, and before the API's pool cap they exhausted Postgres ("53300: too many clients").
//
// What it removes, via the same API a guardian uses:
//   - calendars and groups: DELETE /calendars/{id}, DELETE /groups/{id} (owner only; deleting a
//     group also deletes its calendars);
//   - children: the backend has no "delete child" use case, so every seeded guardian's link is
//     revoked (DELETE /users/me/children/{childId}/guardian-link) -- the child disappears from
//     their dashboard, admin page and per-child selects -- and the child's Keycloak identity
//     (provisioned by CreateChild) is deleted. The child's own buddy User stream stays, unlinked.
// Matching is by the unique generated name, checked as every seeded guardian plus every disposable
// guardian the test made (a spec can switch identity mid-test, and guardian-invite-accept links a
// child to a second guardian).
//
// Disposable guardians (the newGuardian fixture in auth-fixture.ts) are then soft-deleted in the
// backend (DELETE /users/me) and removed from Keycloak. Specs that depend on *family-wide* state
// (the mealplan family scope, the family AI credential, a guardian's own email) use one instead of
// alice/bob/carol, because a seeded guardian's family is shared with every parallel test: e.g.
// GuardianMealplan resolves the family scope from the guardian's first child, which can be another
// test's child whose links that test's cleanup revokes mid-flow.
//
// Playwright runs one test at a time per worker process, so module-level lists are per-test as
// long as the cleanUpCreatedData auto fixture in auth-fixture.ts drains them after every test.

export interface TrackedChild {
  givenName: string;
  familyName: string;
  username: string;
}

const createdChildren: TrackedChild[] = [];
const createdGroups: string[] = [];
const createdCalendars: string[] = [];
const disposableGuardians: DisposableGuardian[] = [];

export function trackDisposableGuardian(guardian: DisposableGuardian): void {
  disposableGuardians.push(guardian);
}

export function trackCreatedChild(child: TrackedChild): void {
  createdChildren.push(child);
}

export function trackCreatedGroup(name: string): void {
  createdGroups.push(name);
}

export function trackCreatedCalendar(name: string): void {
  createdCalendars.push(name);
}

interface ChildSummaryDto {
  id: string;
  name: { givenName: string; familyName: string };
}

interface NamedDto {
  id: string;
  name: string;
}

async function deleteMatching(
  api: APIRequestContext,
  headers: Record<string, string>,
  listPath: string,
  deletePath: (id: string) => string,
  names: readonly string[],
  label: string,
): Promise<void> {
  if (names.length === 0) {
    return;
  }

  const response = await api.get(listPath, { headers });

  if (!response.ok()) {
    console.warn(`[e2e cleanup] GET ${listPath} failed: ${response.status()}`);
    return;
  }

  for (const item of (await response.json()) as NamedDto[]) {
    if (!names.includes(item.name)) {
      continue;
    }

    const removed = await api.delete(deletePath(item.id), { headers });

    // 403/404: not this guardian's to delete (a member, not the owner), or already gone through a
    // group cascade -- another seeded guardian's pass or the group delete handles it.
    if (!removed.ok() && removed.status() !== 403 && removed.status() !== 404) {
      console.warn(`[e2e cleanup] deleting ${label} ${item.name} failed: ${removed.status()}`);
    }
  }
}

// Revokes a guardian's link to each of our created children, and removes any of our created
// calendars/groups this guardian owns. Best-effort per guardian: one guardian's failure is logged
// and never stops the rest.
async function cleanUpGuardianOwnedData(
  api: APIRequestContext,
  guardians: readonly TestUser[],
  calendars: readonly string[],
  groups: readonly string[],
  children: readonly TrackedChild[],
): Promise<void> {
  for (const guardian of guardians) {
    try {
      const { accessToken } = await getAccessToken(guardian.username, guardian.password);
      const headers = { Authorization: `Bearer ${accessToken}` };

      // Calendars before groups: a group delete cascades to its calendars anyway, but a calendar
      // can also live in a group this test didn't create.
      await deleteMatching(
        api,
        headers,
        '/calendars',
        (id) => `/calendars/${id}`,
        calendars,
        'calendar',
      );
      await deleteMatching(api, headers, '/groups', (id) => `/groups/${id}`, groups, 'group');

      if (children.length === 0) {
        continue;
      }

      const response = await api.get('/users/me/children', { headers });

      if (!response.ok()) {
        console.warn(
          `[e2e cleanup] listing ${guardian.username}'s children failed: ${response.status()}`,
        );
        continue;
      }

      for (const child of (await response.json()) as ChildSummaryDto[]) {
        const isOurs = children.some(
          (created) =>
            created.givenName === child.name.givenName &&
            created.familyName === child.name.familyName,
        );

        if (!isOurs) {
          continue;
        }

        const revoke = await api.delete(`/users/me/children/${child.id}/guardian-link`, {
          headers,
        });

        if (!revoke.ok()) {
          console.warn(
            `[e2e cleanup] unlinking ${child.name.givenName} from ${guardian.username} failed: ${revoke.status()}`,
          );
        }
      }
    } catch (error) {
      console.warn(`[e2e cleanup] cleanup as ${guardian.username} failed:`, error);
    }
  }
}

// Soft-deletes the backend user for each disposable guardian this test minted.
async function deleteDisposableGuardianAccounts(
  api: APIRequestContext,
  disposables: readonly DisposableGuardian[],
): Promise<void> {
  for (const guardian of disposables) {
    try {
      const { accessToken } = await getAccessToken(guardian.username, guardian.password);
      const removed = await api.delete('/users/me', {
        headers: { Authorization: `Bearer ${accessToken}` },
      });

      // 403 is user_not_provisioned: the guardian never opened the app, so there's no backend
      // user to delete.
      if (!removed.ok() && removed.status() !== 404 && removed.status() !== 403) {
        console.warn(
          `[e2e cleanup] deleting backend user ${guardian.username} failed: ${removed.status()}`,
        );
      }
    } catch (error) {
      console.warn(`[e2e cleanup] deleting backend user ${guardian.username} failed:`, error);
    }
  }
}

async function deleteDisposableGuardianKeycloakIdentities(
  disposables: readonly DisposableGuardian[],
): Promise<void> {
  for (const guardian of disposables) {
    // Fail-safe: never a seeded guardian, only identities createDisposableGuardian() minted.
    if (guardian.username.startsWith('e2e') && !isSeededUsername(guardian.username)) {
      await deleteKeycloakUser(guardian.username);
    }
  }
}

async function deleteChildKeycloakIdentities(children: readonly TrackedChild[]): Promise<void> {
  for (const child of children) {
    // Fail-safe: only ever delete the throwaway child identities createChild() mints.
    if (child.username.startsWith('e2echild') && !isSeededUsername(child.username)) {
      await deleteKeycloakUser(child.username);
    }
  }
}

// Best-effort: a cleanup failure is logged, never thrown, so it can't turn a passing test red or
// hide the real failure of a failing one.
export async function cleanUpCreatedData(): Promise<void> {
  const children = createdChildren.splice(0);
  const groups = createdGroups.splice(0);
  const calendars = createdCalendars.splice(0);
  const disposables = disposableGuardians.splice(0);

  if (children.length + groups.length + calendars.length + disposables.length === 0) {
    return;
  }

  const { apiBaseUrl } = readRuntimeConfig();
  const api = await request.newContext({ baseURL: apiBaseUrl, ignoreHTTPSErrors: true });

  try {
    const guardians: readonly TestUser[] = [...SEEDED_GUARDIANS, ...disposables];
    await cleanUpGuardianOwnedData(api, guardians, calendars, groups, children);
    await deleteDisposableGuardianAccounts(api, disposables);
  } finally {
    await api.dispose();
  }

  await deleteDisposableGuardianKeycloakIdentities(disposables);
  await deleteChildKeycloakIdentities(children);
}
