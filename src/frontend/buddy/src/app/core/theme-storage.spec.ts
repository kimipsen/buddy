import { beforeEach, describe, expect, it } from 'vitest';

import { THEME_STORAGE_KEY, readStoredThemeMode, writeStoredThemeMode } from './theme-storage';

describe('theme-storage', () => {
  beforeEach(() => {
    localStorage.clear();
    sessionStorage.clear();
  });

  describe('readStoredThemeMode', () => {
    it('returns null when nothing has been stored', () => {
      expect(readStoredThemeMode(localStorage)).toBeNull();
    });

    it.each(['light', 'dark', 'system'] as const)('returns the stored mode "%s"', (mode) => {
      localStorage.setItem(THEME_STORAGE_KEY, mode);

      expect(readStoredThemeMode(localStorage)).toBe(mode);
    });

    it('returns null when the stored value is not a valid theme mode', () => {
      localStorage.setItem(THEME_STORAGE_KEY, 'darkest');

      expect(readStoredThemeMode(localStorage)).toBeNull();
    });

    it('returns null for an empty string value', () => {
      localStorage.setItem(THEME_STORAGE_KEY, '');

      expect(readStoredThemeMode(localStorage)).toBeNull();
    });

    it('does not read a value stored under a different key', () => {
      localStorage.setItem('some_other_key', 'dark');

      expect(readStoredThemeMode(localStorage)).toBeNull();
    });

    it('reads from the specific Storage instance passed in, not any global storage', () => {
      sessionStorage.setItem(THEME_STORAGE_KEY, 'dark');

      expect(readStoredThemeMode(localStorage)).toBeNull();
      expect(readStoredThemeMode(sessionStorage)).toBe('dark');
    });
  });

  describe('writeStoredThemeMode', () => {
    it('stores the mode under the fixed storage key', () => {
      writeStoredThemeMode(localStorage, 'dark');

      expect(localStorage.getItem(THEME_STORAGE_KEY)).toBe('dark');
    });

    it('overwrites a previously stored value', () => {
      writeStoredThemeMode(localStorage, 'dark');

      writeStoredThemeMode(localStorage, 'light');

      expect(readStoredThemeMode(localStorage)).toBe('light');
    });

    it('writes to the specific Storage instance passed in, not any global storage', () => {
      writeStoredThemeMode(sessionStorage, 'dark');

      expect(localStorage.getItem(THEME_STORAGE_KEY)).toBeNull();
      expect(sessionStorage.getItem(THEME_STORAGE_KEY)).toBe('dark');
    });
  });

  describe('round trip', () => {
    it('writes and reads a mode in sequence', () => {
      expect(readStoredThemeMode(localStorage)).toBeNull();

      writeStoredThemeMode(localStorage, 'system');

      expect(readStoredThemeMode(localStorage)).toBe('system');
    });
  });
});
