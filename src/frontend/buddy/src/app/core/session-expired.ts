// The /login query string that tells the login page a session expired, rather than the user never
// having signed in. Written by auth.interceptor.ts and the route guards, read by the login page.
export const SESSION_EXPIRED_REASON = 'session-expired';
export const SESSION_EXPIRED_QUERY_PARAMS = { reason: SESSION_EXPIRED_REASON };
