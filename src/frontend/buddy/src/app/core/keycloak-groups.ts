// The Keycloak groups that let an account use Buddy, as the backend's KeycloakGroups names them.
// Keycloak's "groups" mapper on the frontend client puts the account's group names in the access
// token. An account in neither group can't use Buddy: the API answers 403 not_a_buddy_member.
export const GUARDIAN_GROUP = 'buddy-guardian';
export const CHILD_GROUP = 'buddy-child';

export type BuddyGroup = typeof GUARDIAN_GROUP | typeof CHILD_GROUP;

/** The `groups` claim of a JWT, or [] when the token is missing, malformed or has none. */
export function readGroups(accessToken: string | undefined): string[] {
  const payload = accessToken?.split('.')[1];

  if (!payload) {
    return [];
  }

  try {
    const base64 = payload.replace(/-/g, '+').replace(/_/g, '/');
    const json = new TextDecoder().decode(Uint8Array.from(atob(base64), (c) => c.charCodeAt(0)));
    const groups: unknown = (JSON.parse(json) as { groups?: unknown }).groups;

    return Array.isArray(groups) ? groups.filter((g): g is string => typeof g === 'string') : [];
  } catch {
    return [];
  }
}
