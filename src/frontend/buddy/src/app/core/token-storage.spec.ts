import { beforeEach, describe, expect, it } from 'vitest';

import { clearStoredTokens, readStoredTokens, TokenSet, writeStoredTokens } from './token-storage';

const STORAGE_KEY = 'buddy_keycloak_tokens';

describe('token-storage', () => {
  beforeEach(() => {
    localStorage.clear();
    sessionStorage.clear();
  });

  const tokens: TokenSet = {
    accessToken: 'access-token-value',
    refreshToken: 'refresh-token-value',
    idToken: 'id-token-value',
    expiresAt: 1_700_000_000_000
  };

  describe('readStoredTokens', () => {
    it('returns null when nothing has been stored', () => {
      expect(readStoredTokens(localStorage)).toBeNull();
    });

    it('returns the parsed tokens after they have been written under the storage key', () => {
      localStorage.setItem(STORAGE_KEY, JSON.stringify(tokens));

      expect(readStoredTokens(localStorage)).toEqual(tokens);
    });

    it('returns null and does not throw when the stored value is not valid JSON', () => {
      localStorage.setItem(STORAGE_KEY, 'not-json{');

      expect(readStoredTokens(localStorage)).toBeNull();
    });

    it('returns null for an empty string value', () => {
      localStorage.setItem(STORAGE_KEY, '');

      expect(readStoredTokens(localStorage)).toBeNull();
    });

    it('does not read a value stored under a different key', () => {
      localStorage.setItem('some_other_key', JSON.stringify(tokens));

      expect(readStoredTokens(localStorage)).toBeNull();
    });

    it('reads from the specific Storage instance passed in, not any global storage', () => {
      sessionStorage.setItem(STORAGE_KEY, JSON.stringify(tokens));

      expect(readStoredTokens(localStorage)).toBeNull();
      expect(readStoredTokens(sessionStorage)).toEqual(tokens);
    });

    it('parses a stored JSON value even if it does not structurally match TokenSet', () => {
      localStorage.setItem(STORAGE_KEY, JSON.stringify({ unexpected: true }));

      expect(readStoredTokens(localStorage)).toEqual({ unexpected: true });
    });
  });

  describe('writeStoredTokens', () => {
    it('serializes the tokens as JSON under the fixed storage key', () => {
      writeStoredTokens(localStorage, tokens);

      expect(localStorage.getItem(STORAGE_KEY)).toBe(JSON.stringify(tokens));
    });

    it('overwrites a previously stored value', () => {
      writeStoredTokens(localStorage, tokens);
      const updated: TokenSet = { ...tokens, accessToken: 'new-access-token' };

      writeStoredTokens(localStorage, updated);

      expect(readStoredTokens(localStorage)).toEqual(updated);
    });

    it('preserves null refreshToken and idToken values through a write/read round trip', () => {
      const tokensWithNulls: TokenSet = {
        accessToken: 'access-only',
        refreshToken: null,
        idToken: null,
        expiresAt: 0
      };

      writeStoredTokens(localStorage, tokensWithNulls);

      expect(readStoredTokens(localStorage)).toEqual(tokensWithNulls);
    });

    it('writes to the specific Storage instance passed in, not any global storage', () => {
      writeStoredTokens(sessionStorage, tokens);

      expect(localStorage.getItem(STORAGE_KEY)).toBeNull();
      expect(sessionStorage.getItem(STORAGE_KEY)).toBe(JSON.stringify(tokens));
    });
  });

  describe('clearStoredTokens', () => {
    it('removes a previously stored value', () => {
      writeStoredTokens(localStorage, tokens);

      clearStoredTokens(localStorage);

      expect(localStorage.getItem(STORAGE_KEY)).toBeNull();
      expect(readStoredTokens(localStorage)).toBeNull();
    });

    it('does not throw when there is nothing to clear', () => {
      expect(() => clearStoredTokens(localStorage)).not.toThrow();
    });

    it('does not affect a value stored under the same key in a different Storage instance', () => {
      writeStoredTokens(localStorage, tokens);
      writeStoredTokens(sessionStorage, tokens);

      clearStoredTokens(localStorage);

      expect(readStoredTokens(localStorage)).toBeNull();
      expect(readStoredTokens(sessionStorage)).toEqual(tokens);
    });
  });

  describe('round trip', () => {
    it('writes, reads, and clears tokens in sequence', () => {
      expect(readStoredTokens(localStorage)).toBeNull();

      writeStoredTokens(localStorage, tokens);
      expect(readStoredTokens(localStorage)).toEqual(tokens);

      clearStoredTokens(localStorage);
      expect(readStoredTokens(localStorage)).toBeNull();
    });
  });
});
