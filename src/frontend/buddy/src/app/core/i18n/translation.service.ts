import { Injectable, computed, signal } from '@angular/core';

import { Language, detectBrowserLanguage, isSupportedLanguage } from './language';
import { TRANSLATIONS } from './translations';
import { TranslationValue } from './translation.types';

@Injectable({ providedIn: 'root' })
export class TranslationService {
  // Seeded from the browser's own language before any user is known -- this is what the login
  // screen renders in. Once ensureCurrentUser resolves, setLanguageFromServer replaces it with the
  // signed-in user's saved preference (itself seeded from the Accept-Language header on first
  // sign-in -- see GetOrCreateUserHandler on the backend), so this guess only matters pre-auth.
  private readonly languageState = signal<Language>(detectBrowserLanguage());
  readonly language = this.languageState.asReadonly();

  private readonly dictionary = computed(() => TRANSLATIONS[this.languageState()]);

  setLanguageFromServer(language: string): void {
    if (isSupportedLanguage(language)) {
      this.languageState.set(language);
    }
  }

  setLanguage(language: Language): void {
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

function resolve(dictionary: Record<string, TranslationValue>, key: string): TranslationValue | undefined {
  return key.split('.').reduce<TranslationValue | undefined>((node, segment) => {
    return node && typeof node === 'object' ? node[segment] : undefined;
  }, dictionary);
}

// Placeholders use single braces ("{name}") rather than Angular's own "{{ }}" interpolation syntax
// to keep a translated string with a placeholder unambiguous inside a template expression.
function interpolate(template: string, params: Record<string, string | number>): string {
  return template.replace(/\{(\w+)\}/g, (match, key: string) => (key in params ? String(params[key]) : match));
}
