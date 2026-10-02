// Typed helpers for the few places that index into an array the caller already knows is
// non-empty or in range (a visible week, a reorder within bounds). Each one throws rather than
// handing back undefined, so a broken assumption fails loudly instead of rendering "undefined".

export type NonEmptyArray<T> = [T, ...T[]];

export function firstAndLast<T>(items: readonly T[]): [T, T] {
  const first = items[0];
  const last = items.at(-1);

  if (first === undefined || last === undefined) {
    throw new RangeError('firstAndLast needs at least one item');
  }

  return [first, last];
}

// A copy of items with the elements at i and j exchanged.
export function swapped<T>(items: readonly T[], i: number, j: number): T[] {
  const a = items[i];
  const b = items[j];

  if (a === undefined || b === undefined) {
    throw new RangeError(`swapped index out of range: ${i}, ${j} (length ${items.length})`);
  }

  const next = [...items];
  next[i] = b;
  next[j] = a;
  return next;
}
