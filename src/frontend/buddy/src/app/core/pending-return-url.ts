// Carries the page a user was on across the Keycloak login redirect when their session expired
// mid-use. Like pending-invite-token.ts, this is a narrow stand-in rather than a general
// return-url feature: it's only stored when the app itself ends an expired session (see
// auth.interceptor.ts and auth.guard.ts), and role.guard.ts only honours it inside the signed-in
// user's own role tree.
const STORAGE_KEY = 'buddy_pending_return_url';

export function storePendingReturnUrl(url: string): void {
  sessionStorage.setItem(STORAGE_KEY, url);
}

export function takePendingReturnUrl(): string | null {
  const url = sessionStorage.getItem(STORAGE_KEY);

  if (url) {
    sessionStorage.removeItem(STORAGE_KEY);
  }

  return url;
}
