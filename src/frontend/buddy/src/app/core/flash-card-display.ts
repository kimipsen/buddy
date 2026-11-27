// Display helpers for the built-in flash-card sets. The catalogue ships Danish names; the UI
// translates them by BuiltInKey and falls back to the stored name for a key it does not know.
const BUILT_IN_NAME_KEYS: Record<string, string> = {
  'clock:1': 'flashCards.builtIn.names.clock1',
  'clock:2': 'flashCards.builtIn.names.clock2',
  'clock:3': 'flashCards.builtIn.names.clock3',
  'clock:4': 'flashCards.builtIn.names.clock4',
  'sight:1': 'flashCards.builtIn.names.sight1',
  'sight:2': 'flashCards.builtIn.names.sight2',
  'sight:3': 'flashCards.builtIn.names.sight3',
  'sight:4': 'flashCards.builtIn.names.sight4',
  'letters:1': 'flashCards.builtIn.names.letters1',
  'letters:2': 'flashCards.builtIn.names.letters2',
  'english:1': 'flashCards.builtIn.names.english1',
  'english:2': 'flashCards.builtIn.names.english2',
  'english:3': 'flashCards.builtIn.names.english3',
  'countries:1': 'flashCards.builtIn.names.countries1',
  'countries:2': 'flashCards.builtIn.names.countries2',
  'countries:3': 'flashCards.builtIn.names.countries3',
};

// The i18n key for a built-in set's name, or null when the key is missing or unknown.
export function builtInNameKey(builtInKey: string | null | undefined): string | null {
  return builtInKey && Object.hasOwn(BUILT_IN_NAME_KEYS, builtInKey)
    ? (BUILT_IN_NAME_KEYS[builtInKey] ?? null)
    : null;
}

// Where the hands point, as the numerals a child reads (12 for the top). Never the time words.
export function clockAriaParams(clock: { hour: number; minute: number }): {
  hour: number;
  minute: number;
} {
  return { hour: clock.hour % 12 || 12, minute: Math.round(clock.minute / 5) % 12 || 12 };
}

function normalise(text: string): string {
  return text.normalize('NFC').trim().toLocaleLowerCase('da');
}

// A sight word has no back: its answer is the word itself (D7).
export function sameAfterNormalising(a: string, b: string): boolean {
  return normalise(a) === normalise(b);
}
