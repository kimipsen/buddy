export const SUPPORTED_LANGUAGES = ['en', 'da'] as const;

export type Language = (typeof SUPPORTED_LANGUAGES)[number];

export const DEFAULT_LANGUAGE: Language = 'en';

export const LANGUAGE_NAMES: Record<Language, string> = {
  en: 'English',
  da: 'Dansk'
};

export function isSupportedLanguage(value: string): value is Language {
  return (SUPPORTED_LANGUAGES as readonly string[]).includes(value);
}

// Best-effort guess for the pre-auth login screen, before the signed-in user's saved language
// resolves (see TranslationService.setLanguageFromServer). Only the primary subtag is matched
// (e.g. "da" from "da-DK"), mirroring the backend's SupportedLanguages.ResolveFromAcceptLanguageHeader,
// which is what actually decides a new user's stored language from the Accept-Language header.
export function detectBrowserLanguage(): Language {
  const candidates = navigator.languages?.length ? navigator.languages : [navigator.language];

  for (const candidate of candidates) {
    const primarySubtag = candidate.split('-')[0].toLowerCase();

    if (isSupportedLanguage(primarySubtag)) {
      return primarySubtag;
    }
  }

  return DEFAULT_LANGUAGE;
}
