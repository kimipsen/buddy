import {
  compareNames,
  firstAndLast,
  sortByFullName,
  sortByName,
  sortByPersonName,
  swapped,
} from './array-utils';

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

describe('sortByName', () => {
  it('sorts locale-aware by name without changing the input', () => {
    const items = [{ name: 'Zoe' }, { name: 'ask' }, { name: 'Bo' }];

    expect(sortByName(items)).toEqual([{ name: 'ask' }, { name: 'Bo' }, { name: 'Zoe' }]);
    expect(items[0]).toEqual({ name: 'Zoe' });
  });
});

describe('compareNames', () => {
  it('compares locale-aware, ignoring case', () => {
    expect(compareNames('ask', 'Bo')).toBeLessThan(0);
    expect(compareNames('Zoe', 'Bo')).toBeGreaterThan(0);
    expect(compareNames('Bo', 'Bo')).toBe(0);
  });
});

describe('sortByFullName', () => {
  it('sorts by given name, then family name, without changing the input', () => {
    const people = [
      { givenName: 'Sam', familyName: 'Kid' },
      { givenName: 'Robin', familyName: 'Zed' },
      { givenName: 'Robin', familyName: 'Abe' },
    ];

    expect(sortByFullName(people)).toEqual([
      { givenName: 'Robin', familyName: 'Abe' },
      { givenName: 'Robin', familyName: 'Zed' },
      { givenName: 'Sam', familyName: 'Kid' },
    ]);
    expect(people[0]).toEqual({ givenName: 'Sam', familyName: 'Kid' });
  });
});

describe('sortByPersonName', () => {
  const named = (givenName: string, familyName: string) => ({ name: { givenName, familyName } });

  it('sorts by given name, then family name, without changing the input', () => {
    const children = [named('Sam', 'Kid'), named('Robin', 'Zed'), named('Robin', 'Abe')];

    expect(sortByPersonName(children)).toEqual([
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
