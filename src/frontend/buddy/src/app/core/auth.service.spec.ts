import { DOCUMENT } from '@angular/common';
import { TestBed } from '@angular/core/testing';
import { afterEach, describe, expect, it, vi } from 'vitest';

import { AuthService } from './auth.service';
import { generateCodeChallenge } from './pkce';
import { KeycloakConfig, RuntimeConfigService } from './runtime-config.service';
import { readStoredTokens, writeStoredTokens, type TokenSet } from './token-storage';

// Mirrors the private constants in auth.service.ts (not exported, so re-declared here).
const CODE_VERIFIER_STORAGE_KEY = 'buddy_keycloak_code_verifier';
const REFRESH_SKEW_MS = 10_000;

const keycloakConfig: KeycloakConfig = {
  authority: 'https://keycloak.buddy.test',
  clientId: 'buddy-app',
  realm: 'buddy',
  redirectPath: '/auth/callback'
};

interface FetchResponse {
  ok: boolean;
  status: number;
  statusText: string;
  json: () => Promise<unknown>;
}

function jsonResponse(body: unknown, init: { ok?: boolean; status?: number; statusText?: string } = {}): FetchResponse {
  return {
    ok: init.ok ?? true,
    status: init.status ?? 200,
    statusText: init.statusText ?? '',
    json: async () => body
  };
}

function stubFetch(...responses: FetchResponse[]) {
  const fetchMock = vi.fn();

  for (const response of responses) {
    fetchMock.mockResolvedValueOnce(response);
  }

  vi.stubGlobal('fetch', fetchMock as unknown as typeof fetch);

  return fetchMock;
}

interface SetupOptions {
  search?: string;
  origin?: string;
  storedTokens?: TokenSet;
}

function setup(options: SetupOptions = {}) {
  const origin = options.origin ?? 'https://app.buddy.test';

  if (options.storedTokens) {
    writeStoredTokens(sessionStorage, options.storedTokens);
  }

  const historyReplaceState = vi.fn();
  const documentStub = {
    location: {
      search: options.search ?? '',
      origin,
      href: ''
    },
    defaultView: {
      history: { replaceState: historyReplaceState }
    }
  };

  TestBed.configureTestingModule({
    providers: [
      { provide: DOCUMENT, useValue: documentStub },
      { provide: RuntimeConfigService, useValue: { keycloak: keycloakConfig } }
    ]
  });

  const service = TestBed.inject(AuthService);

  return { service, documentStub, historyReplaceState };
}

describe('AuthService', () => {
  afterEach(() => {
    sessionStorage.clear();
    vi.unstubAllGlobals();
    vi.useRealTimers();
  });

  describe('initial state', () => {
    it('reports unauthenticated when there are no stored tokens', () => {
      const { service } = setup();

      expect(service.isAuthenticated()).toBe(false);
    });

    it('reports authenticated when tokens are already present in storage at startup', () => {
      const tokens: TokenSet = { accessToken: 'a', refreshToken: 'r', idToken: null, expiresAt: Date.now() + 100_000 };
      const { service } = setup({ storedTokens: tokens });

      expect(service.isAuthenticated()).toBe(true);
    });
  });

  describe('login', () => {
    it('stores a PKCE verifier and redirects to the Keycloak authorize endpoint with a matching challenge', async () => {
      const { service, documentStub } = setup();

      await service.login();

      const verifier = sessionStorage.getItem(CODE_VERIFIER_STORAGE_KEY);
      expect(verifier).toBeTruthy();

      const url = new URL(documentStub.location.href);
      expect(url.origin + url.pathname).toBe(`${keycloakConfig.authority}/realms/${keycloakConfig.realm}/protocol/openid-connect/auth`);
      expect(url.searchParams.get('client_id')).toBe(keycloakConfig.clientId);
      expect(url.searchParams.get('redirect_uri')).toBe(`https://app.buddy.test${keycloakConfig.redirectPath}`);
      expect(url.searchParams.get('response_type')).toBe('code');
      expect(url.searchParams.get('scope')).toBe('openid profile email');
      expect(url.searchParams.get('code_challenge_method')).toBe('S256');

      const expectedChallenge = await generateCodeChallenge(verifier!);
      expect(url.searchParams.get('code_challenge')).toBe(expectedChallenge);
    });
  });

  describe('completeLoginRedirect', () => {
    it('does nothing when there is no authorization code in the URL', async () => {
      const { service, historyReplaceState } = setup({ search: '' });
      const fetchMock = stubFetch();

      await service.completeLoginRedirect();

      expect(fetchMock).not.toHaveBeenCalled();
      expect(historyReplaceState).not.toHaveBeenCalled();
      expect(service.isAuthenticated()).toBe(false);
    });

    it('resets the URL without exchanging a code when no verifier was stored for it', async () => {
      const { service, historyReplaceState } = setup({ search: '?code=abc123' });
      const fetchMock = stubFetch();

      await service.completeLoginRedirect();

      expect(fetchMock).not.toHaveBeenCalled();
      expect(historyReplaceState).toHaveBeenCalledWith({}, '', keycloakConfig.redirectPath);
      expect(service.isAuthenticated()).toBe(false);
    });

    it('exchanges the code for tokens using the verifier stored by login(), then resets the URL', async () => {
      const { service, documentStub, historyReplaceState } = setup();

      await service.login();
      documentStub.location.href = '';
      documentStub.location.search = '?code=abc123&state=xyz';

      const fetchMock = stubFetch(
        jsonResponse({ access_token: 'new-access', refresh_token: 'new-refresh', id_token: 'new-id', expires_in: 300 })
      );

      await service.completeLoginRedirect();

      expect(service.isAuthenticated()).toBe(true);
      expect(readStoredTokens(sessionStorage)).toEqual({
        accessToken: 'new-access',
        refreshToken: 'new-refresh',
        idToken: 'new-id',
        expiresAt: expect.any(Number)
      });
      expect(sessionStorage.getItem(CODE_VERIFIER_STORAGE_KEY)).toBeNull();
      expect(historyReplaceState).toHaveBeenCalledWith({}, '', keycloakConfig.redirectPath);

      const [url, init] = fetchMock.mock.calls[0] as [string, RequestInit];
      expect(url).toBe(`${keycloakConfig.authority}/realms/${keycloakConfig.realm}/protocol/openid-connect/token`);
      expect(init.method).toBe('POST');
      const body = init.body as URLSearchParams;
      expect(body.get('grant_type')).toBe('authorization_code');
      expect(body.get('code')).toBe('abc123');
      expect(body.get('redirect_uri')).toBe(`https://app.buddy.test${keycloakConfig.redirectPath}`);
      expect(body.get('client_id')).toBe(keycloakConfig.clientId);
      expect(body.get('code_verifier')).toBeTruthy();
    });

    it('leaves the session unauthenticated and does not reset the URL when the exchange fails', async () => {
      const { service, documentStub, historyReplaceState } = setup();

      await service.login();
      documentStub.location.search = '?code=bad-code';

      stubFetch(jsonResponse({}, { ok: false, status: 400, statusText: 'Bad Request' }));

      await expect(service.completeLoginRedirect()).rejects.toThrow('Token request failed: 400 Bad Request');

      expect(service.isAuthenticated()).toBe(false);
      expect(readStoredTokens(sessionStorage)).toBeNull();
      // Only login()'s own navigation touched history.replaceState never gets called on this path.
      expect(historyReplaceState).not.toHaveBeenCalled();
    });
  });

  describe('logout', () => {
    it('clears the session and redirects to Keycloak logout with an id_token_hint when an id token is present', () => {
      const tokens: TokenSet = { accessToken: 'a', refreshToken: 'r', idToken: 'id-123', expiresAt: Date.now() + 100_000 };
      const { service, documentStub } = setup({ storedTokens: tokens });

      service.logout();

      expect(service.isAuthenticated()).toBe(false);
      expect(readStoredTokens(sessionStorage)).toBeNull();

      const url = new URL(documentStub.location.href);
      expect(url.origin + url.pathname).toBe(`${keycloakConfig.authority}/realms/${keycloakConfig.realm}/protocol/openid-connect/logout`);
      expect(url.searchParams.get('id_token_hint')).toBe('id-123');
      expect(url.searchParams.get('client_id')).toBeNull();
      expect(url.searchParams.get('post_logout_redirect_uri')).toBe('https://app.buddy.test/login');
    });

    it('falls back to client_id when signed in but the stored token set has no id token', () => {
      const tokens: TokenSet = { accessToken: 'a', refreshToken: 'r', idToken: null, expiresAt: Date.now() + 100_000 };
      const { service, documentStub } = setup({ storedTokens: tokens });

      service.logout();

      const url = new URL(documentStub.location.href);
      expect(url.searchParams.get('client_id')).toBe(keycloakConfig.clientId);
      expect(url.searchParams.get('id_token_hint')).toBeNull();
    });

    it('falls back to client_id when there was never a session to begin with', () => {
      const { service, documentStub } = setup();

      service.logout();

      const url = new URL(documentStub.location.href);
      expect(url.searchParams.get('client_id')).toBe(keycloakConfig.clientId);
      expect(url.searchParams.get('id_token_hint')).toBeNull();
      expect(service.isAuthenticated()).toBe(false);
    });
  });

  describe('getAccessToken', () => {
    it('returns null and makes no request when there are no stored tokens', async () => {
      const { service } = setup();
      const fetchMock = stubFetch();

      const token = await service.getAccessToken();

      expect(token).toBeNull();
      expect(fetchMock).not.toHaveBeenCalled();
    });

    it('returns the current access token without refreshing when it is not close to expiry', async () => {
      const tokens: TokenSet = { accessToken: 'still-valid', refreshToken: 'r', idToken: null, expiresAt: Date.now() + 60_000 };
      const { service } = setup({ storedTokens: tokens });
      const fetchMock = stubFetch();

      const token = await service.getAccessToken();

      expect(token).toBe('still-valid');
      expect(fetchMock).not.toHaveBeenCalled();
    });

    it('treats a token expiring exactly at the refresh skew boundary as needing a refresh', async () => {
      const now = 1_700_000_000_000;
      vi.setSystemTime(now);
      const tokens: TokenSet = { accessToken: 'old', refreshToken: 'refresh-me', idToken: null, expiresAt: now + REFRESH_SKEW_MS };
      const { service } = setup({ storedTokens: tokens });
      const fetchMock = stubFetch(jsonResponse({ access_token: 'new', refresh_token: 'new-r', expires_in: 300 }));

      const token = await service.getAccessToken();

      expect(fetchMock).toHaveBeenCalledTimes(1);
      expect(token).toBe('new');
    });

    it('refreshes an expired token and returns the new access token', async () => {
      const tokens: TokenSet = { accessToken: 'old', refreshToken: 'refresh-me', idToken: 'old-id', expiresAt: Date.now() - 1000 };
      const { service } = setup({ storedTokens: tokens });
      const fetchMock = stubFetch(
        jsonResponse({ access_token: 'new-access', refresh_token: 'new-refresh', id_token: 'new-id', expires_in: 300 })
      );

      const token = await service.getAccessToken();

      expect(token).toBe('new-access');
      expect(service.isAuthenticated()).toBe(true);
      expect(readStoredTokens(sessionStorage)?.accessToken).toBe('new-access');

      const [url, init] = fetchMock.mock.calls[0] as [string, RequestInit];
      expect(url).toBe(`${keycloakConfig.authority}/realms/${keycloakConfig.realm}/protocol/openid-connect/token`);
      const body = init.body as URLSearchParams;
      expect(body.get('grant_type')).toBe('refresh_token');
      expect(body.get('refresh_token')).toBe('refresh-me');
      expect(body.get('client_id')).toBe(keycloakConfig.clientId);
    });

    it('clears the session and returns null when a refresh request fails', async () => {
      const tokens: TokenSet = { accessToken: 'old', refreshToken: 'refresh-me', idToken: null, expiresAt: Date.now() - 1000 };
      const { service } = setup({ storedTokens: tokens });
      stubFetch(jsonResponse({}, { ok: false, status: 401, statusText: 'Unauthorized' }));

      const token = await service.getAccessToken();

      expect(token).toBeNull();
      expect(service.isAuthenticated()).toBe(false);
      expect(readStoredTokens(sessionStorage)).toBeNull();
    });

    it('clears the session without a network call when there is no refresh token to use', async () => {
      const tokens: TokenSet = { accessToken: 'old', refreshToken: null, idToken: null, expiresAt: Date.now() - 1000 };
      const { service } = setup({ storedTokens: tokens });
      const fetchMock = stubFetch();

      const token = await service.getAccessToken();

      expect(token).toBeNull();
      expect(fetchMock).not.toHaveBeenCalled();
      expect(service.isAuthenticated()).toBe(false);
    });

    it('shares a single in-flight refresh across concurrent callers', async () => {
      const tokens: TokenSet = { accessToken: 'old', refreshToken: 'refresh-me', idToken: null, expiresAt: Date.now() - 1000 };
      const { service } = setup({ storedTokens: tokens });
      const fetchMock = stubFetch(jsonResponse({ access_token: 'new-access', refresh_token: 'new-refresh', expires_in: 300 }));

      const [first, second] = await Promise.all([service.getAccessToken(), service.getAccessToken()]);

      expect(first).toBe('new-access');
      expect(second).toBe('new-access');
      expect(fetchMock).toHaveBeenCalledTimes(1);
    });

    it('starts a new refresh after a previous one has completed', async () => {
      const tokens: TokenSet = { accessToken: 'old', refreshToken: 'refresh-me', idToken: null, expiresAt: Date.now() - 1000 };
      const { service } = setup({ storedTokens: tokens });
      const fetchMock = stubFetch(
        jsonResponse({ access_token: 'first-refresh', refresh_token: 'r2', expires_in: 0 }),
        jsonResponse({ access_token: 'second-refresh', refresh_token: 'r3', expires_in: 300 })
      );

      const first = await service.getAccessToken();
      const second = await service.getAccessToken();

      expect(first).toBe('first-refresh');
      expect(second).toBe('second-refresh');
      expect(fetchMock).toHaveBeenCalledTimes(2);
    });
  });
});
