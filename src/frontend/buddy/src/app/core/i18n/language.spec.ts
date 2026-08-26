import { afterEach, describe, expect, it, vi } from 'vitest';

import { DEFAULT_LANGUAGE, LANGUAGE_NAMES, SUPPORTED_LANGUAGES, detectBrowserLanguage, isSupportedLanguage } from './language';

describe('isSupportedLanguage', () => {
  it.each(SUPPORTED_LANGUAGES)('returns true for the supported language "%s"', (language) => {
    expect(isSupportedLanguage(language)).toBe(true);
  });

  it('returns false for an unsupported language code', () => {
    expect(isSupportedLanguage('fr')).toBe(false);
  });

  it('returns false for an empty string', () => {
    expect(isSupportedLanguage('')).toBe(false);
  });

  it('is case-sensitive, rejecting an uppercase variant of a supported code', () => {
    expect(isSupportedLanguage('EN')).toBe(false);
  });
});

describe('SUPPORTED_LANGUAGES / DEFAULT_LANGUAGE / LANGUAGE_NAMES', () => {
  it('lists English and Danish as the supported languages', () => {
    expect(SUPPORTED_LANGUAGES).toEqual(['en', 'da']);
  });

  it('defaults to English', () => {
    expect(DEFAULT_LANGUAGE).toBe('en');
  });

  it('provides a display name for every supported language', () => {
    for (const language of SUPPORTED_LANGUAGES) {
      expect(LANGUAGE_NAMES[language]).toEqual(expect.any(String));
      expect(LANGUAGE_NAMES[language].length).toBeGreaterThan(0);
    }
  });
});

describe('detectBrowserLanguage', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('returns the primary subtag of a supported navigator.languages entry', () => {
    vi.stubGlobal('navigator', { languages: ['da-DK'], language: 'da-DK' });

    expect(detectBrowserLanguage()).toBe('da');
  });

  it('matches the exact language code when no region subtag is present', () => {
    vi.stubGlobal('navigator', { languages: ['en'], language: 'en' });

    expect(detectBrowserLanguage()).toBe('en');
  });

  it('skips unsupported entries and returns the first supported candidate in navigator.languages', () => {
    vi.stubGlobal('navigator', { languages: ['fr-FR', 'de-DE', 'da-DK', 'en-US'], language: 'fr-FR' });

    expect(detectBrowserLanguage()).toBe('da');
  });

  it('falls back to the default language when no candidate is supported', () => {
    vi.stubGlobal('navigator', { languages: ['fr-FR', 'de-DE'], language: 'fr-FR' });

    expect(detectBrowserLanguage()).toBe(DEFAULT_LANGUAGE);
  });

  it('falls back to navigator.language when navigator.languages is empty', () => {
    vi.stubGlobal('navigator', { languages: [], language: 'da-DK' });

    expect(detectBrowserLanguage()).toBe('da');
  });

  it('falls back to navigator.language when navigator.languages is undefined', () => {
    vi.stubGlobal('navigator', { languages: undefined, language: 'da-DK' });

    expect(detectBrowserLanguage()).toBe('da');
  });

  it('matches case-insensitively', () => {
    vi.stubGlobal('navigator', { languages: ['DA-DK'], language: 'DA-DK' });

    expect(detectBrowserLanguage()).toBe('da');
  });

  it('returns the default language when navigator.language itself is unsupported', () => {
    vi.stubGlobal('navigator', { languages: [], language: 'fr-FR' });

    expect(detectBrowserLanguage()).toBe(DEFAULT_LANGUAGE);
  });
});
