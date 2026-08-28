import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { THEME_STORAGE_KEY } from './theme-storage';
import { ThemeService } from './theme.service';

// jsdom doesn't implement matchMedia, so every test needs a stub. This fake supports the single
// listener ThemeService registers and lets tests simulate the OS preference changing at runtime.
function stubMatchMedia(initialMatches: boolean) {
  const listeners: Array<(event: { matches: boolean }) => void> = [];
  const mediaQueryList = {
    matches: initialMatches,
    media: '(prefers-color-scheme: dark)',
    addEventListener: (_type: string, listener: (event: { matches: boolean }) => void) => {
      listeners.push(listener);
    },
    removeEventListener: () => {}
  };

  vi.stubGlobal('matchMedia', vi.fn().mockReturnValue(mediaQueryList));

  return {
    emitChange(matches: boolean): void {
      mediaQueryList.matches = matches;
      listeners.forEach((listener) => listener({ matches }));
    }
  };
}

describe('ThemeService', () => {
  beforeEach(() => {
    localStorage.clear();
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  describe('initial mode', () => {
    it('defaults to "system" when nothing is stored', () => {
      stubMatchMedia(false);

      const service = new ThemeService();

      expect(service.mode()).toBe('system');
    });

    it('seeds the mode from a previously stored value', () => {
      localStorage.setItem(THEME_STORAGE_KEY, 'dark');
      stubMatchMedia(false);

      const service = new ThemeService();

      expect(service.mode()).toBe('dark');
    });

    it('falls back to "system" when the stored value is invalid', () => {
      localStorage.setItem(THEME_STORAGE_KEY, 'darkest');
      stubMatchMedia(false);

      const service = new ThemeService();

      expect(service.mode()).toBe('system');
    });
  });

  describe('isDark', () => {
    it('is true when the mode is "dark", regardless of the OS preference', () => {
      stubMatchMedia(false);
      const service = new ThemeService();

      service.setMode('dark');

      expect(service.isDark()).toBe(true);
    });

    it('is false when the mode is "light", regardless of the OS preference', () => {
      stubMatchMedia(true);
      const service = new ThemeService();

      service.setMode('light');

      expect(service.isDark()).toBe(false);
    });

    it('follows the OS preference when the mode is "system"', () => {
      stubMatchMedia(true);
      const service = new ThemeService();

      service.setMode('system');

      expect(service.isDark()).toBe(true);
    });

    it('reacts live to OS preference changes while in "system" mode', () => {
      const media = stubMatchMedia(false);
      const service = new ThemeService();
      service.setMode('system');
      expect(service.isDark()).toBe(false);

      media.emitChange(true);

      expect(service.isDark()).toBe(true);
    });

    it('ignores OS preference changes while not in "system" mode', () => {
      const media = stubMatchMedia(false);
      const service = new ThemeService();
      service.setMode('light');

      media.emitChange(true);

      expect(service.isDark()).toBe(false);
    });
  });

  describe('setMode', () => {
    it('updates the current mode', () => {
      stubMatchMedia(false);
      const service = new ThemeService();

      service.setMode('dark');

      expect(service.mode()).toBe('dark');
    });

    it('persists the mode to localStorage', () => {
      stubMatchMedia(false);
      const service = new ThemeService();

      service.setMode('dark');

      expect(localStorage.getItem(THEME_STORAGE_KEY)).toBe('dark');
    });
  });
});
