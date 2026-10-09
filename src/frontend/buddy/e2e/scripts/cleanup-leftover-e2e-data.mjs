#!/usr/bin/env node
// One-off cleanup for the children, groups and calendars that e2e runs left on the seeded
// guardians (alice/bob/carol) before the per-test cleanup in e2e/support/created-data-cleanup.ts
// existed.
//
// DRY RUN BY DEFAULT: lists what it would remove. Pass --apply to actually remove it.
//
//   cd src/frontend/buddy
//   node e2e/scripts/cleanup-leftover-e2e-data.mjs            # dry run, all seeded guardians
//   node e2e/scripts/cleanup-leftover-e2e-data.mjs --apply    # delete/unlink everything listed
//   node e2e/scripts/cleanup-leftover-e2e-data.mjs --apply --user bob
//
// Needs the API running (default http://localhost:5193) and Keycloak (default
// http://keycloak:8080, the in-devcontainer hostname -- NOT localhost:9080, see
// .devcontainer/README.md). Override with API_URL / KEYCLOAK_URL.
//
// What counts as e2e leftover data -- nothing is pre-seeded (buddy-realm.json only seeds the three
// guardian logins), and only names in the exact shape guardian-data.ts generates
// (uniqueName(prefix) = prefix + base36 suffix) are touched; anything else is listed as "keep":
//   - children: family name "Testson", given name `E2e<Word>Child<suffix>` (createChild);
//   - groups:   `E2eGroup<suffix>`, `E2eGroupA<suffix>`, `E2eGroupB<suffix>` (createGroup), only
//               where the seeded guardian is the owner;
//   - calendars: `E2eCalendar<suffix>` (createCalendar).
//
// What --apply does (the same as the per-test cleanup):
//   1. DELETE /calendars/{id} and DELETE /groups/{id} (a group delete cascades to its calendars);
//   2. children: the backend has no "delete child" use case, so
//      DELETE /users/me/children/{childId}/guardian-link as each linked seeded guardian;
//   3. deletes Keycloak users in the buddy realm whose username matches `e2echild<base36>` (the
//      child logins createChild() provisions) -- via the master admin (admin/admin, local dev only).
// Deleted groups/calendars and unlinked children stay in the event store as history.

const API_URL = process.env.API_URL ?? 'http://localhost:5193';
const KEYCLOAK_URL = process.env.KEYCLOAK_URL ?? 'http://keycloak:8080';
const REALM = 'buddy';
const CLIENT_ID = 'buddy-frontend';

// Mirrors e2e/support/seeded-users.ts.
const SEEDED = {
  alice: 'alice-test-pw',
  bob: 'bob-test-pw',
  carol: 'carol-test-pw',
};

const E2E_GIVEN_NAME = /^E2e[A-Za-z]*Child[0-9a-z]{8,14}$/;
const E2E_FAMILY_NAME = 'Testson';
const E2E_CHILD_USERNAME = /^e2echild[0-9a-z]{8,14}$/;
const E2E_GROUP_NAME = /^E2eGroup[AB]?[0-9a-z]{8,14}$/;
const E2E_CALENDAR_NAME = /^E2eCalendar[0-9a-z]{8,14}$/;
// GroupRole/CalendarRole serialize as member names.
const OWNER = 'Owner';

const args = process.argv.slice(2);
const apply = args.includes('--apply');
const userIndex = args.indexOf('--user');
const onlyUser = userIndex >= 0 ? args[userIndex + 1] : null;

if (onlyUser && !(onlyUser in SEEDED)) {
  console.error(`--user must be one of ${Object.keys(SEEDED).join(', ')}`);
  process.exit(2);
}

async function token(realm, clientId, username, password) {
  const response = await fetch(`${KEYCLOAK_URL}/realms/${realm}/protocol/openid-connect/token`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
    body: new URLSearchParams({
      grant_type: 'password',
      client_id: clientId,
      username,
      password,
      scope: 'openid',
    }),
  });

  if (!response.ok) {
    throw new Error(`token for ${username}@${realm} failed: ${response.status}`);
  }

  return (await response.json()).access_token;
}

function isE2eChild(child) {
  return (
    child.name?.familyName === E2E_FAMILY_NAME && E2E_GIVEN_NAME.test(child.name?.givenName ?? '')
  );
}

let failures = 0;
let unlinked = 0;
let deleted = 0;

// Lists `path`, then deletes (with --apply) the owned items whose name matches `pattern`.
async function cleanNamed(username, headers, path, pattern, label) {
  const response = await fetch(`${API_URL}${path}`, { headers });

  if (!response.ok) {
    console.error(`${username}: listing ${label}s failed: ${response.status}`);
    failures++;
    return;
  }

  const items = await response.json();
  const leftovers = items.filter((item) => pattern.test(item.name) && item.role === OWNER);
  console.log(
    `${username}: ${items.length} ${label}s, ${leftovers.length} e2e leftovers, ${items.length - leftovers.length} kept`,
  );

  for (const item of items.filter((candidate) => !leftovers.includes(candidate))) {
    console.log(`  keep    ${label} ${item.name} (${item.id}, role ${item.role})`);
  }

  for (const item of leftovers) {
    if (!apply) {
      console.log(`  remove  ${label} ${item.name} (${item.id})`);
      continue;
    }

    const removed = await fetch(`${API_URL}${path}/${item.id}`, { method: 'DELETE', headers });

    // 404: already gone via its group's cascade.
    if (removed.ok || removed.status === 404) {
      deleted++;
    } else {
      failures++;
      console.error(`  FAILED  ${label} ${item.name} (${item.id}): ${removed.status}`);
    }
  }
}

for (const [username, password] of Object.entries(SEEDED)) {
  if (onlyUser && username !== onlyUser) {
    continue;
  }

  const accessToken = await token(REALM, CLIENT_ID, username, password);
  const headers = { Authorization: `Bearer ${accessToken}` };

  await cleanNamed(username, headers, '/calendars', E2E_CALENDAR_NAME, 'calendar');
  await cleanNamed(username, headers, '/groups', E2E_GROUP_NAME, 'group');

  const response = await fetch(`${API_URL}/users/me/children`, { headers });

  if (!response.ok) {
    console.error(`${username}: listing children failed: ${response.status}`);
    failures++;
    continue;
  }

  const children = await response.json();
  const leftovers = children.filter(isE2eChild);
  const kept = children.filter((child) => !isE2eChild(child));

  console.log(
    `${username}: ${children.length} children, ${leftovers.length} e2e leftovers, ${kept.length} kept`,
  );
  for (const child of kept) {
    console.log(`  keep    ${child.name.givenName} ${child.name.familyName} (${child.id})`);
  }

  for (const child of leftovers) {
    if (!apply) {
      console.log(`  remove  ${child.name.givenName} ${child.name.familyName} (${child.id})`);
      continue;
    }

    const revoke = await fetch(`${API_URL}/users/me/children/${child.id}/guardian-link`, {
      method: 'DELETE',
      headers,
    });

    if (revoke.ok) {
      unlinked++;
      console.log(`  removed ${child.name.givenName} (${child.id})`);
    } else {
      failures++;
      console.error(`  FAILED  ${child.name.givenName} (${child.id}): ${revoke.status}`);
    }
  }
}

// Keycloak child identities. Only when cleaning up for every seeded guardian: a child username
// can't be mapped back to one guardian (ChildSummary has no username).
if (!onlyUser) {
  const adminToken = await token('master', 'admin-cli', 'admin', 'admin');
  const adminHeaders = { Authorization: `Bearer ${adminToken}` };
  const search = await fetch(
    `${KEYCLOAK_URL}/admin/realms/${REALM}/users?search=e2echild&max=1000&briefRepresentation=true`,
    {
      headers: adminHeaders,
    },
  );

  if (!search.ok) {
    console.error(`Keycloak user search failed: ${search.status}`);
    failures++;
  } else {
    const users = (await search.json()).filter(
      (user) => E2E_CHILD_USERNAME.test(user.username) && !(user.username in SEEDED),
    );
    console.log(`keycloak: ${users.length} e2echild* users`);

    for (const user of users) {
      if (!apply) {
        console.log(`  remove  ${user.username}`);
        continue;
      }

      const removed = await fetch(`${KEYCLOAK_URL}/admin/realms/${REALM}/users/${user.id}`, {
        method: 'DELETE',
        headers: adminHeaders,
      });

      if (!removed.ok) {
        failures++;
        console.error(`  FAILED  ${user.username}: ${removed.status}`);
      }
    }
  }
}

console.log(
  apply
    ? `done: ${deleted} groups/calendars deleted, ${unlinked} child links removed, ${failures} failures`
    : 'dry run only -- re-run with --apply to remove the listed items',
);
process.exit(failures > 0 ? 1 : 0);
