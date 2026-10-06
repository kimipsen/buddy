import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { storePendingReturnUrl, takePendingReturnUrl } from './pending-return-url';

const STORAGE_KEY = 'buddy_pending_return_url';

describe('pending-return-url', () => {
  beforeEach(() => {
    sessionStorage.clear();
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  describe('takePendingReturnUrl', () => {
    it('returns null when nothing has been stored', () => {
      expect(takePendingReturnUrl()).toBeNull();
    });

    it('returns the URL that was stored under the fixed storage key', () => {
      sessionStorage.setItem(STORAGE_KEY, '/guardian/calendar');

      expect(takePendingReturnUrl()).toBe('/guardian/calendar');
    });

    it('removes the URL from sessionStorage after reading it (one-time consume)', () => {
      sessionStorage.setItem(STORAGE_KEY, '/guardian/calendar');

      takePendingReturnUrl();

      expect(sessionStorage.getItem(STORAGE_KEY)).toBeNull();
    });

    it('returns null on a second call after the first call already consumed the URL', () => {
      sessionStorage.setItem(STORAGE_KEY, '/guardian/calendar');

      const first = takePendingReturnUrl();
      const second = takePendingReturnUrl();

      expect(first).toBe('/guardian/calendar');
      expect(second).toBeNull();
    });

    it('does not touch sessionStorage when there is nothing to remove', () => {
      const removeItem = vi.spyOn(Storage.prototype, 'removeItem');

      takePendingReturnUrl();

      expect(sessionStorage).toHaveLength(0);
      expect(removeItem).not.toHaveBeenCalled();
    });

    it('does not read a value stored under a different key', () => {
      sessionStorage.setItem('some_other_key', '/guardian/calendar');

      expect(takePendingReturnUrl()).toBeNull();
    });
  });

  describe('storePendingReturnUrl', () => {
    it('stores the URL under the fixed storage key', () => {
      storePendingReturnUrl('/guardian/calendar');

      expect(sessionStorage.getItem(STORAGE_KEY)).toBe('/guardian/calendar');
    });

    it('overwrites a previously stored URL', () => {
      storePendingReturnUrl('/guardian/mealplan');
      storePendingReturnUrl('/guardian/calendar?view=month');

      expect(takePendingReturnUrl()).toBe('/guardian/calendar?view=month');
    });
  });

  describe('round trip', () => {
    it('stores and then takes the exact same URL', () => {
      storePendingReturnUrl('/child/calendar');

      expect(takePendingReturnUrl()).toBe('/child/calendar');
      expect(takePendingReturnUrl()).toBeNull();
    });
  });
});
