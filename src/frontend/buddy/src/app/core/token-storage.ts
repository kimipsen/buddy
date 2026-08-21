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
    return JSON.parse(raw) as TokenSet;
  } catch {
    return null;
  }
}

export function writeStoredTokens(storage: Storage, tokens: TokenSet): void {
  storage.setItem(STORAGE_KEY, JSON.stringify(tokens));
}

export function clearStoredTokens(storage: Storage): void {
  storage.removeItem(STORAGE_KEY);
}
