import { DOCUMENT } from '@angular/common';
import { computed, inject, Injectable, signal } from '@angular/core';

import { generateCodeChallenge, generateCodeVerifier } from './pkce';
import { RuntimeConfigService } from './runtime-config.service';
import { clearStoredTokens, readStoredTokens, writeStoredTokens, type TokenSet } from './token-storage';

const CODE_VERIFIER_KEY = 'buddy_keycloak_code_verifier';
// Refresh a bit ahead of expiry to avoid racing an in-flight request against an expired token.
const REFRESH_SKEW_MS = 10_000;

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly document = inject(DOCUMENT);
  private readonly runtimeConfig = inject(RuntimeConfigService);
  private readonly tokens = signal<TokenSet | null>(readStoredTokens(sessionStorage));
  private refreshInFlight: Promise<string | null> | null = null;

  readonly isAuthenticated = computed(() => this.tokens() !== null);

  async completeLoginRedirect(): Promise<void> {
    const searchParams = new URLSearchParams(this.document.location.search);
    const code = searchParams.get('code');

    if (!code) {
      return;
    }

    const codeVerifier = sessionStorage.getItem(CODE_VERIFIER_KEY);
    sessionStorage.removeItem(CODE_VERIFIER_KEY);

    if (!codeVerifier) {
      this.document.defaultView?.history.replaceState({}, '', this.keycloak.redirectPath);
      return;
    }

    const keycloak = this.keycloak;
    const redirectUri = `${this.document.location.origin}${keycloak.redirectPath}`;

    const tokens = await this.exchangeToken({
      grant_type: 'authorization_code',
      code,
      redirect_uri: redirectUri,
      client_id: keycloak.clientId,
      code_verifier: codeVerifier
    });

    this.setTokens(tokens);
    this.document.defaultView?.history.replaceState({}, '', keycloak.redirectPath);
  }

  async login(): Promise<void> {
    const currentOrigin = this.document.location.origin;
    const keycloak = this.keycloak;
    const redirectUri = `${currentOrigin}${keycloak.redirectPath}`;
    const loginUrl = new URL(
      `${keycloak.authority}/realms/${keycloak.realm}/protocol/openid-connect/auth`
    );

    const codeVerifier = generateCodeVerifier();
    const codeChallenge = await generateCodeChallenge(codeVerifier);
    sessionStorage.setItem(CODE_VERIFIER_KEY, codeVerifier);

    loginUrl.searchParams.set('client_id', keycloak.clientId);
    loginUrl.searchParams.set('redirect_uri', redirectUri);
    loginUrl.searchParams.set('response_type', 'code');
    loginUrl.searchParams.set('scope', 'openid profile email');
    loginUrl.searchParams.set('code_challenge', codeChallenge);
    loginUrl.searchParams.set('code_challenge_method', 'S256');

    this.document.location.href = loginUrl.toString();
  }

  logout(): void {
    const idToken = this.tokens()?.idToken ?? null;

    this.tokens.set(null);
    clearStoredTokens(sessionStorage);

    const keycloak = this.keycloak;
    const logoutUrl = new URL(
      `${keycloak.authority}/realms/${keycloak.realm}/protocol/openid-connect/logout`
    );
    logoutUrl.searchParams.set('post_logout_redirect_uri', `${this.document.location.origin}/login`);

    if (idToken) {
      logoutUrl.searchParams.set('id_token_hint', idToken);
    } else {
      logoutUrl.searchParams.set('client_id', keycloak.clientId);
    }

    this.document.location.href = logoutUrl.toString();
  }

  /** Returns a valid access token for calling the backend, refreshing it first if it's expired or about to expire. */
  async getAccessToken(): Promise<string | null> {
    const current = this.tokens();

    if (!current) {
      return null;
    }

    if (current.expiresAt - REFRESH_SKEW_MS > Date.now()) {
      return current.accessToken;
    }

    this.refreshInFlight ??= this.refreshAccessToken(current).finally(() => {
      this.refreshInFlight = null;
    });

    return this.refreshInFlight;
  }

  private async refreshAccessToken(current: TokenSet): Promise<string | null> {
    if (!current.refreshToken) {
      this.tokens.set(null);
      clearStoredTokens(sessionStorage);
      return null;
    }

    try {
      const refreshed = await this.exchangeToken({
        grant_type: 'refresh_token',
        refresh_token: current.refreshToken,
        client_id: this.keycloak.clientId
      });

      this.setTokens(refreshed);
      return refreshed.accessToken;
    } catch {
      this.tokens.set(null);
      clearStoredTokens(sessionStorage);
      return null;
    }
  }

  private async exchangeToken(params: Record<string, string>): Promise<TokenSet> {
    const keycloak = this.keycloak;
    const tokenUrl = `${keycloak.authority}/realms/${keycloak.realm}/protocol/openid-connect/token`;

    const response = await fetch(tokenUrl, {
      method: 'POST',
      headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
      body: new URLSearchParams(params)
    });

    if (!response.ok) {
      throw new Error(`Token request failed: ${response.status} ${response.statusText}`);
    }

    const body = await response.json() as {
      access_token: string;
      refresh_token?: string;
      id_token?: string;
      expires_in: number;
    };

    return {
      accessToken: body.access_token,
      refreshToken: body.refresh_token ?? null,
      idToken: body.id_token ?? null,
      expiresAt: Date.now() + body.expires_in * 1000
    };
  }

  private setTokens(tokens: TokenSet): void {
    this.tokens.set(tokens);
    writeStoredTokens(sessionStorage, tokens);
  }

  private get keycloak() {
    return this.runtimeConfig.keycloak;
  }
}
