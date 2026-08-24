// Carries a group-invite token across the Keycloak login redirect. This app has no general
// "return to where I was" mechanism (login always lands on the role-based home route -- see
// role.guard.ts) -- this is a narrow, invite-specific stand-in for one rather than a generic
// return-url feature.
const STORAGE_KEY = 'buddy_pending_invite_token';

export function storePendingInviteToken(token: string): void {
  sessionStorage.setItem(STORAGE_KEY, token);
}

export function takePendingInviteToken(): string | null {
  const token = sessionStorage.getItem(STORAGE_KEY);

  if (token) {
    sessionStorage.removeItem(STORAGE_KEY);
  }

  return token;
}
