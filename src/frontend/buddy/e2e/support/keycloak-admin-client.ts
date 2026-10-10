import { readRuntimeConfig } from './runtime-config';
import { isSeededUsername } from './seeded-users';

// Provisions (and tears down) a throwaway Keycloak user directly against the buddy realm's Admin
// REST API -- used by delete-account.spec.ts to get a genuinely fresh, non-seeded guardian account
// it can safely delete, and by the newGuardian fixture (auth-fixture.ts) for specs that need a
// guardian whose family/account state no parallel test shares. Every other e2e spec depends on
// alice/bob/carol (SEEDED_USERS in support/seeded-users.ts, seeded via
// .devcontainer/keycloak/buddy-realm.json) continuing to exist, so this module must never be able
// to touch those -- it never takes a username as input from a caller, it always mints its own
// random one.
//
// There's no self-service signup anywhere in this app (buddy-realm.json has
// "registrationAllowed": false, and the guardian-invite-accept flow requires the invitee to already
// have an authenticated, email-verified Keycloak identity -- see accept-guardian-invite.ts /
// AcceptGuardianInvite.Handler.cs on the backend). The only genuinely new identity this app itself
// ever provisions via the UI is a *child* account (CreateChild.Handler.cs), but that's created with
// a temporary password and a pending UPDATE_PASSWORD required action that Keycloak's direct-grant
// flow can't itself satisfy (see BuddyApiFixture.SetPermanentPasswordAsync's comment on the backend
// for the same limitation). So this mirrors that backend integration-test fixture's own pattern
// instead (BuddyApiFixture.CreateUserAsync/GetAdminTokenAsync): authenticate as Keycloak's built-in
// master-realm bootstrap admin (admin/admin -- see .devcontainer/.env's KEYCLOAK_ADMIN* vars, a
// local-dev-only credential, not a production secret) and create the user with a *permanent*
// password up front, so it's immediately usable via the normal direct-grant flow
// (keycloak-client.ts's getAccessToken) with no required-action step to drive.
const MASTER_ADMIN_USERNAME = 'admin';
const MASTER_ADMIN_PASSWORD = 'admin';

// Keycloak firstName of every disposable guardian -- what the app shows as their given name (e.g.
// the pickup cell's guardian options).
export const DISPOSABLE_GUARDIAN_GIVEN_NAME = 'E2e';

export interface DisposableGuardian {
  username: string;
  password: string;
  email: string;
}

async function getMasterAdminToken(authority: string): Promise<string> {
  const response = await fetch(`${authority}/realms/master/protocol/openid-connect/token`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
    body: new URLSearchParams({
      grant_type: 'password',
      client_id: 'admin-cli',
      username: MASTER_ADMIN_USERNAME,
      password: MASTER_ADMIN_PASSWORD,
    }),
  });

  if (!response.ok) {
    throw new Error(
      `Keycloak master admin token request failed: ${response.status} ${response.statusText}`,
    );
  }

  const body = (await response.json()) as { access_token: string };
  return body.access_token;
}

// Creates the disposable user and returns its credentials. A no-guardian-links, no-special-role
// account resolves to the 'guardian' role in this app (see account.service.ts's resolveRole --
// role is derived from having guardian links, not a stored flag), which is exactly what's needed to
// reach /guardian/admin's delete-account section.
export async function createDisposableGuardian(prefix = 'e2edelete'): Promise<DisposableGuardian> {
  const { keycloak } = readRuntimeConfig();
  const suffix = `${Date.now().toString(36)}${Math.floor(Math.random() * 1e6).toString(36)}`;
  const username = `${prefix}${suffix}`.toLowerCase();
  const password = `Pw-${suffix}-Aa1!`;
  const email = `${username}@buddy.test`;

  const adminToken = await getMasterAdminToken(keycloak.authority);

  const response = await fetch(`${keycloak.authority}/admin/realms/${keycloak.realm}/users`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      Authorization: `Bearer ${adminToken}`,
    },
    body: JSON.stringify({
      username,
      email,
      emailVerified: true,
      enabled: true,
      firstName: DISPOSABLE_GUARDIAN_GIVEN_NAME,
      lastName: 'Disposable',
      credentials: [{ type: 'password', value: password, temporary: false }],
    }),
  });

  if (!response.ok) {
    throw new Error(
      `Keycloak admin user creation for '${username}' failed: ${response.status} ${response.statusText}`,
    );
  }

  return { username, password, email };
}

// Best-effort cleanup of the Keycloak identity itself: DeleteCurrentUser on the buddy backend
// (Features/Users/DeleteCurrentUser) only soft-deletes buddy's own User aggregate (appends a
// UserDeleted event) -- it never calls Keycloak's Admin API, so the Keycloak account a
// delete-account test signs in as would otherwise linger in the realm indefinitely across repeated
// runs. Not safety-critical (the account is already fully disposable and never alice/bob/carol),
// just hygiene; failures are swallowed so a cleanup hiccup never fails the test itself.
export async function deleteKeycloakUser(username: string): Promise<void> {
  try {
    const { keycloak } = readRuntimeConfig();
    const adminToken = await getMasterAdminToken(keycloak.authority);

    const lookup = await fetch(
      `${keycloak.authority}/admin/realms/${keycloak.realm}/users?username=${encodeURIComponent(username)}&exact=true`,
      { headers: { Authorization: `Bearer ${adminToken}` } },
    );

    if (!lookup.ok) {
      return;
    }

    const users = (await lookup.json()) as { id: string }[];
    const userId = users[0]?.id;

    if (!userId) {
      return;
    }

    await fetch(`${keycloak.authority}/admin/realms/${keycloak.realm}/users/${userId}`, {
      method: 'DELETE',
      headers: { Authorization: `Bearer ${adminToken}` },
    });
  } catch {
    // Best-effort only -- see comment above.
  }
}

// Ends every Keycloak session of a disposable user (Admin API "Sign out" for that user), which is
// what an SSO-session idle timeout does: the refresh token stops working while an already-issued
// access token stays valid until it expires. Used by session-expiry.spec.ts. Refuses the seeded
// users -- every parallel spec signs in as them, and ending their sessions would break those runs.
export async function endKeycloakSessions(user: DisposableGuardian): Promise<void> {
  if (isSeededUsername(user.username)) {
    throw new Error(`Refusing to end the sessions of seeded user '${user.username}'.`);
  }

  const { keycloak } = readRuntimeConfig();
  const adminToken = await getMasterAdminToken(keycloak.authority);

  const lookup = await fetch(
    `${keycloak.authority}/admin/realms/${keycloak.realm}/users?username=${encodeURIComponent(user.username)}&exact=true`,
    { headers: { Authorization: `Bearer ${adminToken}` } },
  );
  const users = (await lookup.json()) as { id: string }[];
  const userId = users[0]?.id;

  if (!userId) {
    throw new Error(`Keycloak user '${user.username}' not found.`);
  }

  const response = await fetch(
    `${keycloak.authority}/admin/realms/${keycloak.realm}/users/${userId}/logout`,
    { method: 'POST', headers: { Authorization: `Bearer ${adminToken}` } },
  );

  if (!response.ok) {
    throw new Error(
      `Ending Keycloak sessions for '${user.username}' failed: ${response.status} ${response.statusText}`,
    );
  }
}

// Gives a child created through the manage-children UI (createChild in guardian-data.ts) a
// permanent password and clears its UPDATE_PASSWORD required action, so loginAs can sign in as the
// child by direct grant -- the step the screenshot seed takes for demo.emil. Only for e2e children
// (an 'e2echild' username): it must never touch a seeded or real account.
export async function makeChildLoginUsable(
  username: string,
): Promise<{ username: string; password: string }> {
  if (!username.startsWith('e2echild') || isSeededUsername(username)) {
    throw new Error(`Refusing to set a password for '${username}', which isn't an e2e child.`);
  }

  const { keycloak } = readRuntimeConfig();
  const adminToken = await getMasterAdminToken(keycloak.authority);
  const users = `${keycloak.authority}/admin/realms/${keycloak.realm}/users`;
  const headers = { 'Content-Type': 'application/json', Authorization: `Bearer ${adminToken}` };

  const lookup = await fetch(`${users}?username=${encodeURIComponent(username)}&exact=true`, {
    headers,
  });
  const userId = ((await lookup.json()) as { id: string }[])[0]?.id;

  if (!userId) {
    throw new Error(`Keycloak user '${username}' not found.`);
  }

  const password = `E2e-${Math.random().toString(36).slice(2)}-pw`;
  const reset = await fetch(`${users}/${userId}/reset-password`, {
    method: 'PUT',
    headers,
    body: JSON.stringify({ type: 'password', value: password, temporary: false }),
  });
  const update = await fetch(`${users}/${userId}`, {
    method: 'PUT',
    headers,
    body: JSON.stringify({ requiredActions: [] }),
  });

  if (!reset.ok || !update.ok) {
    throw new Error(
      `Making '${username}' able to sign in failed: ${reset.status} / ${update.status}`,
    );
  }

  return { username, password };
}
