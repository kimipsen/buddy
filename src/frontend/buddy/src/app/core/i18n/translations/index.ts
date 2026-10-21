import { Language } from '../language';
import type { da } from './da';
import type { en } from './en';

// Every dotted key path of a dictionary, e.g. 'dashboard.doses.title'.
type KeyPaths<T> = {
  [K in keyof T & string]: T[K] extends string ? K : `${K}.${KeyPaths<T[K]>}`;
}[keyof T & string];

type MissingInDa = Exclude<KeyPaths<typeof en>, KeyPaths<typeof da>>;
type ExtraInDa = Exclude<KeyPaths<typeof da>, KeyPaths<typeof en>>;

// Typing da as `Dictionary` (below) only catches keys missing in da: da is a variable, not a fresh
// object literal, so TypeScript's excess-property check doesn't apply and an extra da key (at any
// depth) would compile. This exact, deep key-path comparison fails the build for both, and the
// error message names the offending keys.
type KeyParity = [MissingInDa, ExtraInDa] extends [never, never]
  ? true
  : { missingInDa: MissingInDa; extraInDa: ExtraInDa };
type ExpectTrue<T extends true> = T;
export type TranslationKeyParity = ExpectTrue<KeyParity>;

export type Dictionary = typeof en;

// Every language is a lazy chunk, English too: it was over a quarter of the initial bundle. The app
// initializer awaits the browser language's dictionary (TranslationService.ready), so the first
// screen still renders translated. `import type` above keeps both out of the initial bundle while
// the parity check still compares them key by key.
const DICTIONARIES: Record<Language, () => Promise<Dictionary>> = {
  en: () => import('./en').then((m) => m.en),
  da: () => import('./da').then((m) => m.da),
};

// Dictionaries available without a fetch. None in the app; the unit test setup preloads English so
// specs render English text synchronously.
const preloaded: Partial<Record<Language, Dictionary>> = {};

export function preloadDictionary(language: Language, dictionary: Dictionary): void {
  preloaded[language] = dictionary;
}

export function preloadedDictionaries(): Partial<Record<Language, Dictionary>> {
  return { ...preloaded };
}

export function loadDictionary(language: Language): Promise<Dictionary> {
  const ready = preloaded[language];

  return ready ? Promise.resolve(ready) : DICTIONARIES[language]();
}
