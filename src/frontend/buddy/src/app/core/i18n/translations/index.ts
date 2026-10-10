import { Language } from '../language';
import type { da } from './da';
import { en } from './en';

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

export { en };

// English is bundled (the default, and the fallback while another language loads). Every other
// language is a lazy chunk; `import type` above keeps da out of the initial bundle while the
// parity check still compares it key by key.
const LAZY_DICTIONARIES: Record<Exclude<Language, 'en'>, () => Promise<Dictionary>> = {
  da: () => import('./da').then((m) => m.da),
};

export function loadDictionary(language: Language): Promise<Dictionary> {
  return language === 'en' ? Promise.resolve(en) : LAZY_DICTIONARIES[language]();
}
