import { describe, expect, it } from 'vitest';

import { da } from './i18n/translations/da';
import { en } from './i18n/translations/en';
import { builtInNameKey, clockAriaParams, sameAfterNormalising } from './flash-card-display';

const KEYS = [
  'clock:1',
  'clock:2',
  'clock:3',
  'clock:4',
  'sight:1',
  'sight:2',
  'sight:3',
  'sight:4',
  'letters:1',
  'letters:2',
  'english:1',
  'english:2',
  'english:3',
  'countries:1',
  'countries:2',
  'countries:3',
];

function lookup(dictionary: object, path: string): unknown {
  return path
    .split('.')
    .reduce<unknown>(
      (node, part) => (node as Record<string, unknown> | undefined)?.[part],
      dictionary,
    );
}

describe('builtInNameKey', () => {
  it.each(KEYS)('%s has a name in English and Danish', (key) => {
    const i18nKey = builtInNameKey(key);
    expect(i18nKey).not.toBeNull();
    expect(typeof lookup(en, i18nKey!)).toBe('string');
    expect(typeof lookup(da, i18nKey!)).toBe('string');
  });

  it('returns null for a missing or unknown key, including inherited property names', () => {
    expect(builtInNameKey(null)).toBeNull();
    expect(builtInNameKey(undefined)).toBeNull();
    expect(builtInNameKey('')).toBeNull();
    expect(builtInNameKey('clock:9')).toBeNull();
    expect(builtInNameKey('toString')).toBeNull();
  });

  it('names the sets the way the catalogue does in Danish', () => {
    expect(lookup(da, builtInNameKey('clock:1')!)).toBe('Klokken: hele timer');
    expect(lookup(da, builtInNameKey('countries:1')!)).toBe('Lande: Norden');
  });
});

describe('clockAriaParams', () => {
  it.each([
    [3, 0, 3, 12],
    [3, 30, 3, 6],
    [12, 0, 12, 12],
    [6, 45, 6, 9],
    [11, 55, 11, 11],
    [0, 5, 12, 1],
  ])('%i:%i points to %i and %i', (hour, minute, h, m) => {
    expect(clockAriaParams({ hour, minute })).toEqual({ hour: h, minute: m });
  });

  it('describes the hand positions in both languages and never the time words', () => {
    for (const dictionary of [en, da]) {
      const template = dictionary.flashCards.clock.ariaLabel;
      expect(template).toContain('{hour}');
      expect(template).toContain('{minute}');
    }
    expect(da.flashCards.clock.ariaLabel).not.toMatch(/halv|kvart/i);
  });
});

describe('sameAfterNormalising', () => {
  it.each([
    ['hund', 'hund'],
    ['  Hund ', 'hund'],
    ['ÆØÅ', 'æøå'],
    ['Å', 'å'],
    ['Å', 'Å'],
    ['Å', 'å'],
  ])('%s equals %s', (a, b) => {
    expect(sameAfterNormalising(a, b)).toBe(true);
  });

  it('tells different words apart', () => {
    expect(sameAfterNormalising('hund', 'kat')).toBe(false);
    expect(sameAfterNormalising('A', 'Å')).toBe(false);
  });
});
