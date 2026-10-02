// Runs `task` over every item with at most `limit` calls in flight at once, and resolves with the
// results in input order (like Promise.all, which it replaces for per-item fan-outs).
//
// Why: a dashboard widget that fires one request per child via Promise.all(children.map(...))
// sends N requests at the same instant. A guardian with dozens of children turned that into a
// burst big enough to exhaust the API's Postgres connection pools ("53300: sorry, too many
// clients already" -> 500s across the whole dashboard). Bounding the fan-out keeps the request
// count the same but caps how many hit the API concurrently. It rejects like Promise.all if a
// task rejects, so callers that want best-effort semantics catch inside `task`.
// Default cap for per-item request fan-outs (one request per child/group/calendar). Matches
// children-overview's PROGRESS_REQUEST_CONCURRENCY: small enough that a few widgets loading at
// once stay well under the API's per-store Postgres pool, large enough that a typical family
// (a handful of children) still loads in a single round.
export const PER_ITEM_REQUEST_CONCURRENCY = 4;

export async function mapWithConcurrency<T, R>(
  items: readonly T[],
  limit: number,
  task: (item: T, index: number) => Promise<R>,
): Promise<R[]> {
  if (!Number.isInteger(limit) || limit < 1) {
    throw new RangeError(`mapWithConcurrency limit must be a positive integer, got ${limit}`);
  }

  // Stryker disable next-line ArrayDeclaration: every index is assigned before results is returned, so a pre-sized and a growing array end up identical
  const results = new Array<R>(items.length);
  let next = 0;

  async function worker(): Promise<void> {
    while (next < items.length) {
      const index = next++;
      results[index] = await task(items[index], index);
    }
  }

  await Promise.all(Array.from({ length: Math.min(limit, items.length) }, () => worker()));

  return results;
}
