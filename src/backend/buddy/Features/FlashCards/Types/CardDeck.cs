namespace buddy.Features.FlashCards;

public static class CardDeck
{
    // Exactly count items drawn from the pool: shuffle, take in order, reshuffle and continue when
    // the pool runs out (a "Times table of 7" has only 10 cards, so a 20-card game must repeat).
    // The same item never comes twice in a row when the pool has at least two, also across a
    // reshuffle. Deterministic for a seeded Random.
    public static IReadOnlyList<T> Draw<T>(IReadOnlyList<T> pool, int count, Random random)
    {
        ArgumentNullException.ThrowIfNull(pool);
        ArgumentNullException.ThrowIfNull(random);
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        if (pool.Count == 0)
        {
            throw new ArgumentException("Cannot draw a deck from an empty pool.", nameof(pool));
        }

        List<T> deck = new(count);
        var last = -1;

        while (deck.Count < count)
        {
            var order = Enumerable.Range(0, pool.Count).ToArray();
            random.Shuffle(order);

            if (pool.Count > 1 && order[0] == last)
            {
                (order[0], order[1]) = (order[1], order[0]);
            }

            foreach (var index in order)
            {
                if (deck.Count == count)
                {
                    break;
                }

                deck.Add(pool[index]);
                last = index;
            }
        }

        return deck;
    }
}
