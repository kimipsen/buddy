import { describe, expect, it } from 'vitest';

import { SUPPORTED_LANGUAGES } from '../language';
import { TranslationValue } from '../translation.types';
import { loadDictionary } from '.';

// Every leaf of a dictionary as [dotted key, value].
function leaves(value: TranslationValue, path = ''): [string, string][] {
  if (typeof value === 'string') {
    return [[path, value]];
  }

  return Object.entries(value).flatMap(([key, child]) =>
    leaves(child, path ? `${path}.${key}` : key),
  );
}

describe('translation dictionaries', () => {
  it.each(SUPPORTED_LANGUAGES)('has text for every key in %s', async (language) => {
    const dictionary = await loadDictionary(language);

    const blank = leaves(dictionary)
      .filter(([, text]) => text.trim() === '')
      .map(([key]) => key);

    expect(blank).toEqual([]);
  });
});
