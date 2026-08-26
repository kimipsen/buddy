import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { TranslationService } from './translation.service';

// The Angular Vitest builder doesn't support vi.mock for relative imports (it throws "The
// vi.mock and related methods are not supported for relative imports with the Angular
// unit-test system"), so TRANSLATIONS can't be swapped for a small test fixture here. Instead
// these tests resolve against the real dictionary, picking keys/shapes that are stable and
// unlikely to change: a flat key, a key nested two levels deep, and a key with two
// placeholders, all present (and translated) in both en and da.
describe('TranslationService', () => {
  beforeEach(() => {
    // Deterministic starting point for tests that don't care about browser-language detection.
    vi.stubGlobal('navigator', { languages: ['en-US'], language: 'en-US' });
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  describe('initial language', () => {
    it('seeds the language from the browser when the browser language is supported', () => {
      vi.stubGlobal('navigator', { languages: ['da-DK'], language: 'da-DK' });

      const service = new TranslationService();

      expect(service.language()).toBe('da');
    });

    it('falls back to the default language when the browser language is unsupported', () => {
      vi.stubGlobal('navigator', { languages: ['fr-FR'], language: 'fr-FR' });

      const service = new TranslationService();

      expect(service.language()).toBe('en');
    });
  });

  describe('setLanguage', () => {
    it('updates the current language', () => {
      const service = new TranslationService();

      service.setLanguage('da');

      expect(service.language()).toBe('da');
    });

    it('switches the dictionary used to resolve subsequent translations', () => {
      const service = new TranslationService();

      expect(service.translate('common.loading')).toBe('Loading…');

      service.setLanguage('da');

      expect(service.translate('common.loading')).toBe('Indlæser…');
    });
  });

  describe('setLanguageFromServer', () => {
    it('sets the language when given a supported language code', () => {
      const service = new TranslationService();

      service.setLanguageFromServer('da');

      expect(service.language()).toBe('da');
    });

    it('leaves the language unchanged when given an unsupported language code', () => {
      const service = new TranslationService();

      service.setLanguageFromServer('fr');

      expect(service.language()).toBe('en');
    });

    it('leaves the language unchanged when given an empty string', () => {
      const service = new TranslationService();
      service.setLanguage('da');

      service.setLanguageFromServer('');

      expect(service.language()).toBe('da');
    });
  });

  describe('translate', () => {
    it('resolves a top-level key to its string value', () => {
      const service = new TranslationService();

      expect(service.translate('common.signOut')).toBe('Sign out');
    });

    it('resolves a nested dotted key to its string value', () => {
      const service = new TranslationService();

      expect(service.translate('mealplan.manageMeals.title')).toBe('Meals');
    });

    it('returns the key itself when the key does not exist in the dictionary', () => {
      const service = new TranslationService();

      expect(service.translate('does.not.exist')).toBe('does.not.exist');
    });

    it('returns the key itself when an intermediate segment does not exist', () => {
      const service = new TranslationService();

      expect(service.translate('common.missing.value')).toBe('common.missing.value');
    });

    it('returns the key itself when the resolved value is an object rather than a leaf string', () => {
      const service = new TranslationService();

      expect(service.translate('common')).toBe('common');
    });

    it('returns the key itself when a segment attempts to traverse into a string node', () => {
      const service = new TranslationService();

      // "common.signOut" resolves to a string; trying to go one level deeper than that must
      // fail rather than throwing or accidentally indexing into the string.
      expect(service.translate('common.signOut.nope')).toBe('common.signOut.nope');
    });

    it('leaves placeholders untouched when no params are given', () => {
      const service = new TranslationService();

      expect(service.translate('mealplan.manageMeals.pageIndicator')).toBe('Page {current} of {total}');
    });

    it('substitutes every placeholder present in params', () => {
      const service = new TranslationService();

      expect(service.translate('mealplan.manageMeals.pageIndicator', { current: '2', total: '5' })).toBe('Page 2 of 5');
    });

    it('coerces numeric params to strings when interpolating', () => {
      const service = new TranslationService();

      expect(service.translate('mealplan.manageMeals.pageIndicator', { current: 2, total: 5 })).toBe('Page 2 of 5');
    });

    it('leaves a placeholder unresolved when its key is missing from params', () => {
      const service = new TranslationService();

      expect(service.translate('mealplan.manageMeals.pageIndicator', { current: 2 })).toBe('Page 2 of {total}');
    });

    it('ignores params that do not correspond to any placeholder in the template', () => {
      const service = new TranslationService();

      expect(service.translate('common.signOut', { unused: 'value' })).toBe('Sign out');
    });

    it('resolves against the dictionary for the currently selected language', () => {
      const service = new TranslationService();
      service.setLanguage('da');

      expect(service.translate('mealplan.manageMeals.pageIndicator', { current: 2, total: 5 })).toBe('Side 2 af 5');
    });
  });
});
