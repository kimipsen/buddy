import { describe, expect, it } from 'vitest';

import { dropdownPosition } from './dropdown-position';

describe('dropdownPosition', () => {
  const size = { maxHeight: 224, minWidth: 192 };
  // An iPhone 15 in portrait.
  const phone = { width: 393, height: 852 };

  function anchorAt(top: number, left = 16, width = 200, height = 32) {
    return { top, bottom: top + height, left, width };
  }

  it('opens below the input when the whole list fits there', () => {
    expect(dropdownPosition(anchorAt(100), size, phone)).toEqual({
      position: 'fixed',
      top: '136px',
      left: '16px',
      width: '200px',
      'max-height': '224px',
    });
  });

  it('still opens below when the list exactly fits above the bottom margin', () => {
    // 852 - 16 margin - 4 gap - 224 list = 608 is the lowest bottom edge that fits.
    const style = dropdownPosition(anchorAt(576), size, phone);

    expect(style['top']).toBe('612px');
    expect(style['bottom']).toBeUndefined();
    expect(style['max-height']).toBe('224px');
  });

  it('flips above the input, anchored by its bottom edge, when there is no room below', () => {
    const style = dropdownPosition(anchorAt(780), size, phone);

    expect(style['top']).toBeUndefined();
    // 852 - 780 + 4: the list's bottom edge sits 4px above the input.
    expect(style['bottom']).toBe('76px');
    expect(style['max-height']).toBe('224px');
  });

  it('stays below and shrinks to the room there when there is even less room above', () => {
    const short = { width: 393, height: 300 };
    const style = dropdownPosition(anchorAt(60), size, short);

    // Below: 300 - 92 - 4 - 16 = 188; above: 60 - 4 - 16 = 40.
    expect(style['top']).toBe('96px');
    expect(style['max-height']).toBe('188px');
  });

  it('shrinks a flipped list to the room above it', () => {
    const short = { width: 393, height: 300 };
    const style = dropdownPosition(anchorAt(200), size, short);

    // Above: 200 - 4 - 16 = 180; below: 300 - 232 - 4 - 16 = 48.
    expect(style['bottom']).toBe('104px');
    expect(style['max-height']).toBe('180px');
  });

  it('never squeezes the list below about three rows', () => {
    const tiny = { width: 393, height: 120 };

    expect(dropdownPosition(anchorAt(40), size, tiny)['max-height']).toBe('96px');
  });

  it('is at least the minimum width next to a narrow input', () => {
    expect(dropdownPosition(anchorAt(100, 16, 120), size, phone)['width']).toBe('192px');
  });

  it('shifts left so it does not stick out on the right', () => {
    const style = dropdownPosition(anchorAt(100, 300, 150), size, phone);

    // 393 - 16 - 192 = 185.
    expect(style['left']).toBe('185px');
    expect(style['width']).toBe('192px');
  });

  it('is never wider than the viewport minus its margins', () => {
    const style = dropdownPosition(anchorAt(100, 0, 150), { maxHeight: 224, minWidth: 400 }, phone);

    expect(style['width']).toBe('361px');
    expect(style['left']).toBe('16px');
  });
});
