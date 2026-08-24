// Carries an email-verification token across the Keycloak login redirect, mirroring
// pending-invite-token.ts -- see that file for why this app uses a narrow, feature-specific
// stand-in here rather than a general return-url mechanism.
const STORAGE_KEY = 'buddy_pending_verify_email_token';

export function storePendingVerifyEmailToken(token: string): void {
  sessionStorage.setItem(STORAGE_KEY, token);
}

export function takePendingVerifyEmailToken(): string | null {
  const token = sessionStorage.getItem(STORAGE_KEY);

  if (token) {
    sessionStorage.removeItem(STORAGE_KEY);
  }

  return token;
}
