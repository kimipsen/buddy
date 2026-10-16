import { describe, expect, it } from 'vitest';

import { en } from './i18n/translations';
import {
  PICTOGRAMS,
  iconText,
  isPictogramSlug,
  parseIcon,
  pictogramIcon,
  pictogramNameKey,
  pictogramSrc,
} from './pictograms';

// The specs run in Node, but the app's tsconfig has no Node types; this is the one call needed.
interface NodeFs {
  readdirSync(path: string): string[];
}

describe('pictograms', () => {
  it('lists exactly the SVG files in public/pictograms', async () => {
    const fs = (await import(/* @vite-ignore */ 'node:' + 'fs')) as NodeFs;
    const files = fs
      .readdirSync('public/pictograms')
      .filter((file) => file.endsWith('.svg'))
      .map((file) => file.slice(0, -'.svg'.length))
      .sort();

    expect([...PICTOGRAMS].sort()).toEqual(files);
  });

  it('has an English name for every pictogram', () => {
    const names = en.pictograms.names as Record<string, string>;

    for (const slug of PICTOGRAMS) {
      const key = pictogramNameKey(slug).split('.').at(-1)!;
      expect(names[key], slug).toBeTruthy();
    }
  });

  it('gives a slug this build lacks the generic name', () => {
    expect(pictogramNameKey('unicorn')).toBe('pictograms.unknown');
    expect(pictogramNameKey('brush-teeth')).toBe('pictograms.names.brushTeeth');
  });

  it('builds and recognises pictogram values', () => {
    expect(pictogramIcon('brush-teeth')).toBe('pictogram:brush-teeth');
    expect(pictogramSrc('brush-teeth')).toBe('/pictograms/brush-teeth.svg');
    expect(isPictogramSlug('bath')).toBe(true);
    expect(isPictogramSlug('unicorn')).toBe(false);
  });

  it('turns an icon into text: the emoji, or nothing for a pictogram', () => {
    expect(iconText('🪥')).toBe('🪥');
    expect(iconText('pictogram:brush-teeth')).toBe('');
    expect(iconText(null)).toBe('');
  });

  describe('parseIcon', () => {
    it('reads no value as no icon', () => {
      expect(parseIcon('')).toBeNull();
      expect(parseIcon(null)).toBeNull();
      expect(parseIcon(undefined)).toBeNull();
    });

    it('reads an emoji as text', () => {
      expect(parseIcon('🪥')).toEqual({ kind: 'emoji', text: '🪥' });
    });

    it('reads a pictogram as its file', () => {
      expect(parseIcon('pictogram:brush-teeth')).toEqual({
        kind: 'pictogram',
        slug: 'brush-teeth',
        src: '/pictograms/brush-teeth.svg',
      });
    });

    it('still reads an unknown pictogram as a pictogram, so the component can fall back', () => {
      expect(parseIcon('pictogram:unicorn')).toEqual({
        kind: 'pictogram',
        slug: 'unicorn',
        src: '/pictograms/unicorn.svg',
      });
    });
  });
});
