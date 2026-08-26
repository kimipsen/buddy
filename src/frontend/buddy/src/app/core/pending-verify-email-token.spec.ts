import { beforeEach, describe, expect, it } from 'vitest';

import { storePendingVerifyEmailToken, takePendingVerifyEmailToken } from './pending-verify-email-token';

const STORAGE_KEY = 'buddy_pending_verify_email_token';

describe('pending-verify-email-token', () => {
  beforeEach(() => {
    sessionStorage.clear();
  });

  describe('takePendingVerifyEmailToken', () => {
    it('returns null when nothing has been stored', () => {
      expect(takePendingVerifyEmailToken()).toBeNull();
    });

    it('returns the token that was stored under the fixed storage key', () => {
      sessionStorage.setItem(STORAGE_KEY, 'verify-email-token-value');

      expect(takePendingVerifyEmailToken()).toBe('verify-email-token-value');
    });

    it('removes the token from sessionStorage after reading it (one-time consume)', () => {
      sessionStorage.setItem(STORAGE_KEY, 'verify-email-token-value');

      takePendingVerifyEmailToken();

      expect(sessionStorage.getItem(STORAGE_KEY)).toBeNull();
    });

    it('returns null on a second call after the first call already consumed the token', () => {
      sessionStorage.setItem(STORAGE_KEY, 'verify-email-token-value');

      const first = takePendingVerifyEmailToken();
      const second = takePendingVerifyEmailToken();

      expect(first).toBe('verify-email-token-value');
      expect(second).toBeNull();
    });

    it('does not touch sessionStorage when there is nothing to remove', () => {
      takePendingVerifyEmailToken();

      expect(sessionStorage.length).toBe(0);
    });

    it('does not read a value stored under a different key', () => {
      sessionStorage.setItem('some_other_key', 'verify-email-token-value');

      expect(takePendingVerifyEmailToken()).toBeNull();
    });
  });

  describe('storePendingVerifyEmailToken', () => {
    it('stores the token under the fixed storage key', () => {
      storePendingVerifyEmailToken('verify-email-token-value');

      expect(sessionStorage.getItem(STORAGE_KEY)).toBe('verify-email-token-value');
    });

    it('overwrites a previously stored token', () => {
      storePendingVerifyEmailToken('first-token');
      storePendingVerifyEmailToken('second-token');

      expect(takePendingVerifyEmailToken()).toBe('second-token');
    });
  });

  describe('round trip', () => {
    it('stores and then takes the exact same token value', () => {
      storePendingVerifyEmailToken('round-trip-token');

      expect(takePendingVerifyEmailToken()).toBe('round-trip-token');
      expect(takePendingVerifyEmailToken()).toBeNull();
    });
  });
});
