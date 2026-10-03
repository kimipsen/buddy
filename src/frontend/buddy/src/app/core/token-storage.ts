export interface TokenSet {
  accessToken: string;
  refreshToken: string | null;
  idToken: string | null;
  expiresAt: number;
}

const STORAGE_KEY = 'buddy_keycloak_tokens';

export function readStoredTokens(storage: Storage): TokenSet | null {
  const raw = storage.getItem(STORAGE_KEY);

  if (!raw) {
    return null;
  }

  try {
    const parsed: unknown = JSON.parse(raw);
    return isTokenSet(parsed) ? parsed : null;
  } catch {
    return null;
  }
}

// Session storage is writable by anything on the page's origin, so a stored value is checked
// before it is trusted as a TokenSet.
export function isTokenSet(value: unknown): value is TokenSet {
  if (typeof value !== 'object' || value === null) {
    return false;
  }

  const candidate = value as Record<string, unknown>;

  return (
    typeof candidate['accessToken'] === 'string' &&
    (candidate['refreshToken'] === null || typeof candidate['refreshToken'] === 'string') &&
    (candidate['idToken'] === null || typeof candidate['idToken'] === 'string') &&
    typeof candidate['expiresAt'] === 'number'
  );
}

export function writeStoredTokens(storage: Storage, tokens: TokenSet): void {
  storage.setItem(STORAGE_KEY, JSON.stringify(tokens));
}

export function clearStoredTokens(storage: Storage): void {
  storage.removeItem(STORAGE_KEY);
}
