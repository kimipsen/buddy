// Carries a guardian-invite token across the Keycloak login redirect. Mirrors
// pending-invite-token.ts exactly, kept as its own narrow stand-in rather than a shared/generic
// one for the same reason that file gives.
const STORAGE_KEY = 'buddy_pending_guardian_invite_token';

export function storePendingGuardianInviteToken(token: string): void {
  sessionStorage.setItem(STORAGE_KEY, token);
}

export function takePendingGuardianInviteToken(): string | null {
  const token = sessionStorage.getItem(STORAGE_KEY);

  if (token) {
    sessionStorage.removeItem(STORAGE_KEY);
  }

  return token;
}
