import { beforeEach, describe, expect, it } from 'vitest';

import { storePendingGuardianInviteToken, takePendingGuardianInviteToken } from './pending-guardian-invite-token';

const STORAGE_KEY = 'buddy_pending_guardian_invite_token';

describe('pending-guardian-invite-token', () => {
  beforeEach(() => {
    sessionStorage.clear();
  });

  describe('takePendingGuardianInviteToken', () => {
    it('returns null when nothing has been stored', () => {
      expect(takePendingGuardianInviteToken()).toBeNull();
    });

    it('returns the token that was stored under the fixed storage key', () => {
      sessionStorage.setItem(STORAGE_KEY, 'guardian-invite-token-value');

      expect(takePendingGuardianInviteToken()).toBe('guardian-invite-token-value');
    });

    it('removes the token from sessionStorage after reading it (one-time consume)', () => {
      sessionStorage.setItem(STORAGE_KEY, 'guardian-invite-token-value');

      takePendingGuardianInviteToken();

      expect(sessionStorage.getItem(STORAGE_KEY)).toBeNull();
    });

    it('returns null on a second call after the first call already consumed the token', () => {
      sessionStorage.setItem(STORAGE_KEY, 'guardian-invite-token-value');

      const first = takePendingGuardianInviteToken();
      const second = takePendingGuardianInviteToken();

      expect(first).toBe('guardian-invite-token-value');
      expect(second).toBeNull();
    });

    it('does not touch sessionStorage when there is nothing to remove', () => {
      takePendingGuardianInviteToken();

      expect(sessionStorage.length).toBe(0);
    });

    it('does not read a value stored under a different key', () => {
      sessionStorage.setItem('some_other_key', 'guardian-invite-token-value');

      expect(takePendingGuardianInviteToken()).toBeNull();
    });
  });

  describe('storePendingGuardianInviteToken', () => {
    it('stores the token under the fixed storage key', () => {
      storePendingGuardianInviteToken('guardian-invite-token-value');

      expect(sessionStorage.getItem(STORAGE_KEY)).toBe('guardian-invite-token-value');
    });

    it('overwrites a previously stored token', () => {
      storePendingGuardianInviteToken('first-token');
      storePendingGuardianInviteToken('second-token');

      expect(takePendingGuardianInviteToken()).toBe('second-token');
    });
  });

  describe('round trip', () => {
    it('stores and then takes the exact same token value', () => {
      storePendingGuardianInviteToken('round-trip-token');

      expect(takePendingGuardianInviteToken()).toBe('round-trip-token');
      expect(takePendingGuardianInviteToken()).toBeNull();
    });
  });
});
