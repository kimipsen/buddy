import { Language } from '../language';
import { da } from './da';
import { en } from './en';

// Every dotted key path of a dictionary, e.g. 'dashboard.doses.title'.
type KeyPaths<T> = {
  [K in keyof T & string]: T[K] extends string ? K : `${K}.${KeyPaths<T[K]>}`;
}[keyof T & string];

type MissingInDa = Exclude<KeyPaths<typeof en>, KeyPaths<typeof da>>;
type ExtraInDa = Exclude<KeyPaths<typeof da>, KeyPaths<typeof en>>;

// `Record<Language, typeof en>` below only catches keys missing in da: da is a variable, not a
// fresh object literal, so TypeScript's excess-property check doesn't apply and an extra da key
// (at any depth) would compile. This exact, deep key-path comparison fails the build for both,
// and the error message names the offending keys.
type KeyParity = [MissingInDa, ExtraInDa] extends [never, never]
  ? true
  : { missingInDa: MissingInDa; extraInDa: ExtraInDa };
type ExpectTrue<T extends true> = T;
export type TranslationKeyParity = ExpectTrue<KeyParity>;

export const TRANSLATIONS: Record<Language, typeof en> = { en, da };
