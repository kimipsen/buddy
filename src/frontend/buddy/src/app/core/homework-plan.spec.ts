import {
  HomeworkPart,
  isValidPart,
  planHomework,
  splitMinutes,
  totalMinutes,
  withBreaks,
} from './homework-plan';

const part = (id: string, estimateMinutes: number, title = id): HomeworkPart => ({
  id,
  title,
  estimateMinutes,
});

describe('splitMinutes', () => {
  it('splits into near-equal chunks with the larger ones first', () => {
    expect(splitMinutes(40, 15)).toEqual([14, 13, 13]);
    expect(splitMinutes(20, 15)).toEqual([10, 10]);
    expect(splitMinutes(31, 10)).toEqual([8, 8, 8, 7]);
  });

  it('keeps an estimate that fits one chunk whole', () => {
    expect(splitMinutes(10, 15)).toEqual([10]);
    expect(splitMinutes(15, 15)).toEqual([15]);
  });

  it('adds up to the estimate and never exceeds the focus length', () => {
    for (const total of [5, 25, 45, 60, 95, 120]) {
      for (const focus of [5, 10, 15, 25, 45]) {
        const chunks = splitMinutes(total, focus);
        expect(chunks.reduce((a, b) => a + b, 0)).toBe(total);
        expect(Math.max(...chunks)).toBeLessThanOrEqual(focus);
        expect(chunks).toHaveLength(Math.ceil(total / focus));
      }
    }
  });
});

describe('planHomework', () => {
  it('chains every part with a break between every two focus chunks and none at the end', () => {
    const steps = planHomework([part('maths', 40), part('reading', 20), part('spelling', 10)], {
      focusMinutes: 15,
      breakMinutes: 5,
    });

    expect(steps).toEqual([
      { kind: 'focus', partId: 'maths', index: 1, of: 3, minutes: 14 },
      { kind: 'break', minutes: 5 },
      { kind: 'focus', partId: 'maths', index: 2, of: 3, minutes: 13 },
      { kind: 'break', minutes: 5 },
      { kind: 'focus', partId: 'maths', index: 3, of: 3, minutes: 13 },
      { kind: 'break', minutes: 5 },
      { kind: 'focus', partId: 'reading', index: 1, of: 2, minutes: 10 },
      { kind: 'break', minutes: 5 },
      { kind: 'focus', partId: 'reading', index: 2, of: 2, minutes: 10 },
      { kind: 'break', minutes: 5 },
      { kind: 'focus', partId: 'spelling', index: 1, of: 1, minutes: 10 },
    ]);
    expect(totalMinutes(steps)).toBe(95);
  });

  it('plans a single short part as one chunk with no break', () => {
    expect(planHomework([part('a', 5)], { focusMinutes: 15, breakMinutes: 5 })).toEqual([
      { kind: 'focus', partId: 'a', index: 1, of: 1, minutes: 5 },
    ]);
  });

  it('plans nothing for no parts', () => {
    expect(planHomework([], { focusMinutes: 15, breakMinutes: 5 })).toEqual([]);
    expect(withBreaks([], 5)).toEqual([]);
  });
});

describe('isValidPart', () => {
  it('needs a title and an estimate in 5-minute steps from 5 to 120', () => {
    expect(isValidPart(part('a', 5, 'Maths'))).toBe(true);
    expect(isValidPart(part('a', 120, 'Maths'))).toBe(true);
    expect(isValidPart(part('a', 0, 'Maths'))).toBe(false);
    expect(isValidPart(part('a', 125, 'Maths'))).toBe(false);
    expect(isValidPart(part('a', 12, 'Maths'))).toBe(false);
    expect(isValidPart(part('a', 10, '   '))).toBe(false);
    expect(isValidPart(part('a', 10, 'x'.repeat(60)))).toBe(true);
    expect(isValidPart(part('a', 10, 'x'.repeat(61)))).toBe(false);
  });
});
