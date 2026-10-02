import { describe, expect, it } from 'vitest';

import { mapWithConcurrency } from './map-with-concurrency';

interface Deferred<T> {
  promise: Promise<T>;
  resolve: (value: T) => void;
  reject: (reason: unknown) => void;
}

function deferred<T>(): Deferred<T> {
  let resolve!: (value: T) => void;
  let reject!: (reason: unknown) => void;
  const promise = new Promise<T>((res, rej) => {
    resolve = res;
    reject = rej;
  });

  return { promise, resolve, reject };
}

// Lets every already-resolved promise continuation run before asserting.
const flush = () => new Promise<void>((resolve) => setTimeout(resolve, 0));

describe('mapWithConcurrency', () => {
  it('never has more than `limit` tasks in flight and still runs every item', async () => {
    const pending = Array.from({ length: 7 }, () => deferred<number>());
    const started: number[] = [];
    let inFlight = 0;
    let maxInFlight = 0;

    const run = mapWithConcurrency([0, 1, 2, 3, 4, 5, 6], 3, async (item) => {
      started.push(item);
      inFlight++;
      maxInFlight = Math.max(maxInFlight, inFlight);
      const value = await pending[item].promise;
      inFlight--;
      return value;
    });

    await flush();
    expect(started).toEqual([0, 1, 2]);

    // Finishing one task frees exactly one slot for the next item.
    pending[1].resolve(10);
    await flush();
    expect(started).toEqual([0, 1, 2, 3]);

    // Resolve out of order -- results must still come back in input order.
    for (const index of [6, 5, 4, 3, 2, 0]) {
      pending[index].resolve(index * 10);
      await flush();
    }

    await expect(run).resolves.toEqual([0, 10, 20, 30, 40, 50, 60]);
    expect(maxInFlight).toBe(3);
    expect(started).toHaveLength(7);
  });

  it('passes the item index to the task', async () => {
    await expect(
      mapWithConcurrency(['a', 'b'], 5, async (item, index) => `${item}${index}`),
    ).resolves.toEqual(['a0', 'b1']);
  });

  it('resolves to an empty array without calling the task for no items', async () => {
    let calls = 0;

    await expect(
      mapWithConcurrency([], 2, async () => {
        calls++;
      }),
    ).resolves.toEqual([]);
    expect(calls).toBe(0);
  });

  it('rejects like Promise.all when a task rejects', async () => {
    await expect(
      mapWithConcurrency([1, 2], 1, async (item) => {
        if (item === 2) {
          throw new Error('boom');
        }
        return item;
      }),
    ).rejects.toThrow('boom');
  });

  it.each([0, -1, 1.5, Number.NaN])('rejects a non-positive-integer limit (%s)', async (limit) => {
    await expect(mapWithConcurrency([1], limit, async (item) => item)).rejects.toThrow(RangeError);
  });

  it('names the rejected limit in the error message', async () => {
    await expect(mapWithConcurrency([1], 0, async (item) => item)).rejects.toThrow(
      'mapWithConcurrency limit must be a positive integer, got 0',
    );
  });
});
