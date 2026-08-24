import { Language } from '../language';
import { da } from './da';
import { en } from './en';

// da is typed against en's shape so a missing or extra key in either language fails to compile
// instead of silently falling back to the raw key at runtime.
export const TRANSLATIONS: Record<Language, typeof en> = { en, da };
