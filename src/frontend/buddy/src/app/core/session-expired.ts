// The /login query string that tells the login page a session expired, rather than the user never
// having signed in. Written by auth.interceptor.ts and the route guards, read by the login page.
export const SESSION_EXPIRED_REASON = 'session-expired';
export const SESSION_EXPIRED_QUERY_PARAMS = { reason: SESSION_EXPIRED_REASON };

// The /login query string for an account that signed in but is in neither Buddy Keycloak group
// (keycloak-groups.ts). Written by roleRedirectGuard on its way out through Keycloak's logout.
export const NO_ACCESS_REASON = 'no-access';
export const NO_ACCESS_QUERY_PARAMS = { reason: NO_ACCESS_REASON };
