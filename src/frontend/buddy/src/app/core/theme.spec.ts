import { describe, expect, it } from 'vitest';

import { DEFAULT_THEME_MODE, THEME_MODES, isThemeMode } from './theme';

describe('isThemeMode', () => {
  it.each(THEME_MODES)('returns true for the theme mode "%s"', (mode) => {
    expect(isThemeMode(mode)).toBe(true);
  });

  it('returns false for an unsupported value', () => {
    expect(isThemeMode('darkest')).toBe(false);
  });

  it('returns false for an empty string', () => {
    expect(isThemeMode('')).toBe(false);
  });

  it('is case-sensitive, rejecting an uppercase variant of a supported mode', () => {
    expect(isThemeMode('DARK')).toBe(false);
  });
});

describe('THEME_MODES / DEFAULT_THEME_MODE', () => {
  it('lists light, dark, and system as the supported modes', () => {
    expect(THEME_MODES).toEqual(['light', 'dark', 'system']);
  });

  it('defaults to system', () => {
    expect(DEFAULT_THEME_MODE).toBe('system');
  });
});
