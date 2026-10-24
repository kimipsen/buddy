import http from 'k6/http';

import { config } from './config.ts';

// Direct-grant (resource owner password) tokens, the same flow as e2e/support/keycloak-client.ts.
// Keycloak traffic is tagged kind:auth, so it never counts against the API's read/write thresholds.

export interface TokenSet {
  accessToken: string;
  refreshToken: string;
  // Epoch milliseconds.
  expiresAt: number;
}

interface TokenResponse {
  access_token: string;
  refresh_token: string;
  expires_in: number;
}

const tokenUrl = (realm: string) =>
  `${config.keycloakUrl}/realms/${realm}/protocol/openid-connect/token`;

function requestToken(form: Record<string, string>, realm = config.realm): TokenSet {
  const response = http.post(tokenUrl(realm), form, {
    tags: { name: 'keycloak-token', kind: 'auth' },
  });

  if (response.status !== 200) {
    throw new Error(`Keycloak token request failed: ${response.status} ${response.body}`);
  }

  const body = response.json() as unknown as TokenResponse;
  return {
    accessToken: body.access_token,
    refreshToken: body.refresh_token,
    expiresAt: Date.now() + body.expires_in * 1000,
  };
}

export function login(username: string, password = config.password): TokenSet {
  return requestToken({
    grant_type: 'password',
    client_id: config.clientId,
    username,
    password,
    scope: 'openid profile email',
  });
}

// The master realm's built-in admin-cli client, as e2e/support/keycloak-admin-client.ts uses. Its
// tokens live 60 s, so adminToken() re-logs in when the cached one is about to expire.
let admin: TokenSet | null = null;

export function adminToken(): string {
  if (!admin || admin.expiresAt - Date.now() < 15_000) {
    admin = requestToken(
      {
        grant_type: 'password',
        client_id: 'admin-cli',
        username: config.keycloakAdmin.username,
        password: config.keycloakAdmin.password,
      },
      'master',
    );
  }

  return admin.accessToken;
}

// Per VU: the realm's access tokens live 5 minutes and a soak runs for hours, so a token is
// refreshed once less than 60 s of it is left. A failed refresh (the SSO session ended) falls back
// to a fresh password login rather than failing the iteration with a wave of 401s.
const cache = new Map<string, TokenSet>();

export function bearer(username: string, seeded?: TokenSet): string {
  let tokens = cache.get(username) ?? seeded ?? login(username);

  if (tokens.expiresAt - Date.now() < 60_000) {
    try {
      tokens = requestToken({
        grant_type: 'refresh_token',
        client_id: config.clientId,
        refresh_token: tokens.refreshToken,
      });
    } catch {
      tokens = login(username);
    }
  }

  cache.set(username, tokens);
  return tokens.accessToken;
}
