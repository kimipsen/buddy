import { Injectable, computed, signal } from '@angular/core';

import { Language, detectBrowserLanguage, isSupportedLanguage } from './language';
import { Dictionary, en, loadDictionary } from './translations';
import { TranslationValue } from './translation.types';

@Injectable({ providedIn: 'root' })
export class TranslationService {
  // English ships with the app; any other language is a lazy chunk, fetched the first time it's
  // needed (it was most of the initial bundle when every dictionary was bundled up front).
  private readonly loaded = signal<Partial<Record<Language, Dictionary>>>({ en });
  // The language in effect, set only once its dictionary has loaded, so the UI never shows a key
  // or mixes languages. Until then the previous language (English at first) stays on screen.
  private readonly languageState = signal<Language>('en');
  readonly language = this.languageState.asReadonly();

  private readonly dictionary = computed(() => this.loaded()[this.languageState()] ?? en);

  // The last language asked for: a slower load for an earlier request must not win.
  private requested: Language = 'en';

  // Seeded from the browser's own language before any user is known -- this is what the login
  // screen renders in. Once ensureCurrentUser resolves, setLanguageFromServer replaces it with the
  // signed-in user's saved preference (itself seeded from the Accept-Language header on first
  // sign-in -- see GetOrCreateUserHandler on the backend), so this guess only matters pre-auth.
  // The app initializer awaits it, so the first screen already renders in that language.
  private readonly initial = this.setLanguage(detectBrowserLanguage());

  ready(): Promise<void> {
    return this.initial;
  }

  setLanguageFromServer(language: string): Promise<void> {
    return isSupportedLanguage(language) ? this.setLanguage(language) : Promise.resolve();
  }

  async setLanguage(language: Language): Promise<void> {
    this.requested = language;
    const dictionary = this.loaded()[language] ?? (await loadDictionary(language));

    if (this.requested !== language) {
      return;
    }

    this.loaded.update((loaded) => ({ ...loaded, [language]: dictionary }));
    this.languageState.set(language);
  }

  translate(key: string, params?: Record<string, string | number>): string {
    const value = resolve(this.dictionary(), key);

    if (typeof value !== 'string') {
      return key;
    }

    return params ? interpolate(value, params) : value;
  }
}

function resolve(
  dictionary: Record<string, TranslationValue>,
  key: string,
): TranslationValue | undefined {
  return key.split('.').reduce<TranslationValue | undefined>((node, segment) => {
    return node && typeof node === 'object' ? node[segment] : undefined;
  }, dictionary);
}

// Placeholders use single braces ("{name}") rather than Angular's own "{{ }}" interpolation syntax
// to keep a translated string with a placeholder unambiguous inside a template expression.
function interpolate(template: string, params: Record<string, string | number>): string {
  return template.replace(/\{(\w+)\}/g, (match, key: string) =>
    key in params ? String(params[key]) : match,
  );
}
