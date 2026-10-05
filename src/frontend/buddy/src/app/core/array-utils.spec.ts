import { firstAndLast, sortByChildName, swapped } from './array-utils';

describe('firstAndLast', () => {
  it('returns the first and last items', () => {
    expect(firstAndLast(['a', 'b', 'c'])).toEqual(['a', 'c']);
  });

  it('returns the only item twice for a single-item array', () => {
    expect(firstAndLast(['a'])).toEqual(['a', 'a']);
  });

  it('throws for an empty array', () => {
    expect(() => firstAndLast([])).toThrow(RangeError);
  });
});

describe('sortByChildName', () => {
  const named = (givenName: string, familyName: string) => ({ name: { givenName, familyName } });

  it('sorts by given name, then family name, without changing the input', () => {
    const children = [named('Sam', 'Kid'), named('Robin', 'Zed'), named('Robin', 'Abe')];

    expect(sortByChildName(children)).toEqual([
      named('Robin', 'Abe'),
      named('Robin', 'Zed'),
      named('Sam', 'Kid'),
    ]);
    expect(children[0]).toEqual(named('Sam', 'Kid'));
  });
});

describe('swapped', () => {
  it('exchanges the two items without mutating the input', () => {
    const items = ['a', 'b', 'c'];

    expect(swapped(items, 0, 2)).toEqual(['c', 'b', 'a']);
    expect(items).toEqual(['a', 'b', 'c']);
  });

  it('throws when either index is out of range', () => {
    expect(() => swapped(['a', 'b'], 0, 2)).toThrow(RangeError);
    expect(() => swapped(['a', 'b'], -1, 1)).toThrow(RangeError);
  });
});
