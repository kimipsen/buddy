import { describe, expect, it } from 'vitest';

import { exampleCard, isValidLimit, limitRange } from './math-example';

describe('math example', () => {
  it.each([
    ['Addition', 20, '10 + 10', '20'],
    ['Addition', 7, '4 + 3', '7'],
    ['Subtraction', 20, '20 − 10', '10'],
    ['Subtraction', 7, '7 − 3', '4'],
    ['Multiplication', 7, '7 × 3', '21'],
    ['Division', 7, '21 ÷ 7', '3'],
  ] as const)('makes a %s card for %i', (operation, limit, question, answer) => {
    expect(exampleCard(operation, limit)).toEqual({ question, answer });
  });

  it('allows sums up to 100 and tables up to 12, the way the backend does', () => {
    expect(limitRange('Addition')).toEqual({ min: 2, max: 100 });
    expect(limitRange('Subtraction')).toEqual({ min: 2, max: 100 });
    expect(limitRange('Multiplication')).toEqual({ min: 1, max: 12 });
    expect(limitRange('Division')).toEqual({ min: 1, max: 12 });
  });

  it('rejects numbers outside the range and fractions', () => {
    expect(isValidLimit('Addition', 100)).toBe(true);
    expect(isValidLimit('Addition', 101)).toBe(false);
    expect(isValidLimit('Addition', 1)).toBe(false);
    expect(isValidLimit('Multiplication', 12)).toBe(true);
    expect(isValidLimit('Multiplication', 13)).toBe(false);
    expect(isValidLimit('Multiplication', 0)).toBe(false);
    expect(isValidLimit('Division', 2.5)).toBe(false);
    expect(isValidLimit('Division', Number.NaN)).toBe(false);
  });
});
