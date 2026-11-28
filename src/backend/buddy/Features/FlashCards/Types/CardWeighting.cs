using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;

namespace buddy.Features.FlashCards;

public sealed record CardWeight(FlashCardSetId SetId, string CardKey, int Weight, string TextHash);

public static class CardWeighting
{
    public const int BaseWeight = 4;
    public const int Boost = 4;
    public const int Cap = 16;
    public const int Floor = 1;
    public const int MaxTrackedCardsPerSet = 400;

    public static ImmutableList<CardWeight> Apply(
        ImmutableList<CardWeight> weights,
        FlashCardSetId setId,
        IEnumerable<FlashCardResult> results)
    {
        var updated = weights;

        foreach (var result in results.OrderBy(result => result.Slot))
        {
            var hash = TextHash(result.Question, result.Answer);
            var index = updated.FindIndex(weight => weight.SetId == setId && weight.CardKey == result.CardKey);
            var current = index >= 0 && updated[index].TextHash == hash
                ? updated[index].Weight
                : BaseWeight;
            var next = result.KnewFirstTime
                ? Math.Max(Floor, current / 2)
                : Math.Min(Cap, current + Boost);
            var entry = new CardWeight(setId, result.CardKey, next, hash);

            updated = index >= 0 ? updated.SetItem(index, entry) : updated.Add(entry);
        }

        var trackedForSet = updated.Where(weight => weight.SetId == setId).ToArray();
        var surplus = trackedForSet.Length - MaxTrackedCardsPerSet;
        if (surplus <= 0)
        {
            return updated;
        }

        var keysToDrop = trackedForSet
            .OrderBy(weight => weight.Weight)
            .ThenBy(weight => weight.CardKey, StringComparer.Ordinal)
            .Take(surplus)
            .Select(weight => weight.CardKey)
            .ToHashSet(StringComparer.Ordinal);

        return updated.RemoveAll(weight => weight.SetId == setId && keysToDrop.Contains(weight.CardKey));
    }

    public static Func<string, string, string, int> WeightOf(FlashCardPractice practice, FlashCardSetId setId)
    {
        var weights = practice.CardWeights
            .Where(weight => weight.SetId == setId)
            .ToDictionary(weight => weight.CardKey, StringComparer.Ordinal);

        return (cardKey, question, answer) =>
            weights.TryGetValue(cardKey, out var stored) && stored.TextHash == TextHash(question, answer)
                ? Math.Clamp(stored.Weight, Floor, Cap)
                : BaseWeight;
    }

    public static string TextHash(string question, string answer)
    {
        var bytes = Encoding.UTF8.GetBytes($"{question}\u001f{answer}");
        var hash = SHA256.HashData(bytes);

        return Convert.ToHexString(hash.AsSpan(0, 8)).ToLowerInvariant();
    }
}
