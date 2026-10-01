export interface TestUser {
  username: string;
  password: string;
}

// Seeded by .devcontainer/keycloak/buddy-realm.json -- see also TestRealm.json on the backend,
// which uses the same usernames/passwords/emails convention for its own isolated realm.
// Lives in its own module (re-exported by auth-fixture.ts) so cleanup helpers can use it without
// importing the fixture that wires them up.
export const SEEDED_USERS = {
  alice: { username: 'alice', password: 'alice-test-pw' },
  bob: { username: 'bob', password: 'bob-test-pw' },
  carol: { username: 'carol', password: 'carol-test-pw' },
} as const satisfies Record<string, TestUser>;

export const SEEDED_GUARDIANS: readonly TestUser[] = Object.values(SEEDED_USERS);

export function isSeededUsername(username: string): boolean {
  return SEEDED_GUARDIANS.some((user) => user.username === username.toLowerCase());
}
