import { beforeEach, describe, expect, it } from 'vitest';

import { storePendingInviteToken, takePendingInviteToken } from './pending-invite-token';

const STORAGE_KEY = 'buddy_pending_invite_token';

describe('pending-invite-token', () => {
  beforeEach(() => {
    sessionStorage.clear();
  });

  describe('takePendingInviteToken', () => {
    it('returns null when nothing has been stored', () => {
      expect(takePendingInviteToken()).toBeNull();
    });

    it('returns the token that was stored under the fixed storage key', () => {
      sessionStorage.setItem(STORAGE_KEY, 'invite-token-value');

      expect(takePendingInviteToken()).toBe('invite-token-value');
    });

    it('removes the token from sessionStorage after reading it (one-time consume)', () => {
      sessionStorage.setItem(STORAGE_KEY, 'invite-token-value');

      takePendingInviteToken();

      expect(sessionStorage.getItem(STORAGE_KEY)).toBeNull();
    });

    it('returns null on a second call after the first call already consumed the token', () => {
      sessionStorage.setItem(STORAGE_KEY, 'invite-token-value');

      const first = takePendingInviteToken();
      const second = takePendingInviteToken();

      expect(first).toBe('invite-token-value');
      expect(second).toBeNull();
    });

    it('does not touch sessionStorage when there is nothing to remove', () => {
      takePendingInviteToken();

      expect(sessionStorage).toHaveLength(0);
    });

    it('does not read a value stored under a different key', () => {
      sessionStorage.setItem('some_other_key', 'invite-token-value');

      expect(takePendingInviteToken()).toBeNull();
    });
  });

  describe('storePendingInviteToken', () => {
    it('stores the token under the fixed storage key', () => {
      storePendingInviteToken('invite-token-value');

      expect(sessionStorage.getItem(STORAGE_KEY)).toBe('invite-token-value');
    });

    it('overwrites a previously stored token', () => {
      storePendingInviteToken('first-token');
      storePendingInviteToken('second-token');

      expect(takePendingInviteToken()).toBe('second-token');
    });
  });

  describe('round trip', () => {
    it('stores and then takes the exact same token value', () => {
      storePendingInviteToken('round-trip-token');

      expect(takePendingInviteToken()).toBe('round-trip-token');
      expect(takePendingInviteToken()).toBeNull();
    });
  });
});
