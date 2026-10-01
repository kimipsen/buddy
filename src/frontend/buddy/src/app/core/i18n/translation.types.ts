export type TranslationValue = string | { [key: string]: TranslationValue };

export type TranslationDictionary = Record<string, TranslationValue>;
