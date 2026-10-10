import { readRuntimeConfig } from './runtime-config';

// Same shape as the app's own TokenSet (src/app/core/token-storage.ts).
export interface TokenSet {
  accessToken: string;
  refreshToken: string | null;
  idToken: string | null;
  expiresAt: number;
}

// Direct-grant (resource owner password) token fetch against the real Keycloak instance --
// mirrors BuddyApiFixture.GetAccessTokenAsync on the backend, which uses the same flow to get a
// real, JWKS-verifiable token without driving a login form for every test. The buddy-frontend
// client has directAccessGrantsEnabled precisely to support this (see
// .devcontainer/keycloak/buddy-realm.json).
export async function getAccessToken(username: string, password: string): Promise<TokenSet> {
  const { keycloak } = readRuntimeConfig();
  const tokenUrl = `${keycloak.authority}/realms/${keycloak.realm}/protocol/openid-connect/token`;

  const response = await fetch(tokenUrl, {
    method: 'POST',
    headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
    body: new URLSearchParams({
      grant_type: 'password',
      client_id: keycloak.clientId,
      username,
      password,
      scope: 'openid profile email',
    }),
  });

  if (!response.ok) {
    throw new Error(
      `Keycloak direct-grant token request for '${username}' failed: ${response.status} ${response.statusText} ${await response.text()}`,
    );
  }

  const body = (await response.json()) as {
    access_token: string;
    refresh_token?: string;
    id_token?: string;
    expires_in: number;
  };

  return {
    accessToken: body.access_token,
    refreshToken: body.refresh_token ?? null,
    idToken: body.id_token ?? null,
    expiresAt: Date.now() + body.expires_in * 1000,
  };
}
