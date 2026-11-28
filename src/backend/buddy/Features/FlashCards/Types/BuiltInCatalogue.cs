using System.Collections.Frozen;

using buddy.Features.Users;

namespace buddy.Features.FlashCards;

// The shipped sets: code, not stored events. Injected as a singleton (registered in
// FlashCardsFeature) so a test host can replace the content; production code outside DI never
// reads BuiltInCatalogue.Default.
public interface IBuiltInCatalogue
{
    // Every built-in set, synthesised, in catalogue order.
    IReadOnlyList<FlashCardSet> All { get; }

    FlashCardSet? TryFind(FlashCardSetId id);

    BuiltInCardLookup TryGetCard(FlashCardSet set, string cardKey);

    // Exactly count cards, repeats allowed when the set is smaller (CardDeck.Draw).
    IReadOnlyList<BuiltInCard> Draw(
        FlashCardSet set,
        int count,
        Random random,
        Func<BuiltInCard, int>? weightOf = null);
}

// A built-in set before synthesis. The id is a literal: .NET has no UUIDv5, so the ids were
// generated once (UUIDv5 of "builtin:<subject>:<level>") and are pinned by a test.
public sealed record BuiltInSetDescriptor(FlashCardSetId Id, string Key, string Name, IReadOnlyList<BuiltInCard> Cards);

public sealed class BuiltInCatalogue : IBuiltInCatalogue
{
    // Owner of every synthesised set. Never a real user, never indexed as an owner and never sent
    // over the API (FlashCardSetResponse.OwnerId is null for a built-in set).
    public static readonly UserId SystemOwnerId = new(new Guid("00000000-0000-7000-8000-00000b017135"));

    // Card keys deliberately removed from a set. Empty at ship; see BuiltInRetiredKeys.
    public static IReadOnlySet<string> BuiltInRetiredKeys { get; } = FrozenSet<string>.Empty;

    public static readonly BuiltInCatalogue Default = new(BuiltInSetDescriptors.All, BuiltInRetiredKeys);

    private readonly Dictionary<Guid, (FlashCardSet Set, IReadOnlyList<BuiltInCard> Cards, FrozenDictionary<string, BuiltInCard> ByKey)> _byId;
    private readonly IReadOnlySet<string> _retired;

    public BuiltInCatalogue(IReadOnlyList<BuiltInSetDescriptor> descriptors, IReadOnlySet<string> retiredKeys)
    {
        _retired = retiredKeys;
        _byId = [];
        List<FlashCardSet> all = [];

        foreach (var descriptor in descriptors)
        {
            var set = Synthesize(descriptor);
            all.Add(set);
            _byId[descriptor.Id.Value] = (set, descriptor.Cards, descriptor.Cards.ToFrozenDictionary(c => c.Key));
        }

        All = all;
    }

    public IReadOnlyList<FlashCardSet> All { get; }

    public FlashCardSet? TryFind(FlashCardSetId id) => _byId.TryGetValue(id.Value, out var entry) ? entry.Set : null;

    public BuiltInCardLookup TryGetCard(FlashCardSet set, string cardKey)
    {
        if (!_byId.TryGetValue(set.Id.Value, out var entry))
        {
            return new BuiltInCardLookup(BuiltInCardLookupKind.Unknown, null);
        }

        if (entry.ByKey.TryGetValue(cardKey, out var card))
        {
            return new BuiltInCardLookup(BuiltInCardLookupKind.Found, card);
        }

        return new BuiltInCardLookup(_retired.Contains(cardKey) ? BuiltInCardLookupKind.Retired : BuiltInCardLookupKind.Unknown, null);
    }

    public IReadOnlyList<BuiltInCard> Draw(
        FlashCardSet set,
        int count,
        Random random,
        Func<BuiltInCard, int>? weightOf = null)
    {
        var cards = _byId[set.Id.Value].Cards;
        return weightOf is null
            ? CardDeck.Draw(cards, count, random)
            : CardDeck.Draw(cards, count, random, weightOf);
    }

    // The one place a built-in FlashCardSet is built: All, TryFind and the event store all share it.
    private static FlashCardSet Synthesize(BuiltInSetDescriptor descriptor) => new(
        descriptor.Id,
        SystemOwnerId,
        descriptor.Name,
        FlashCardSetKind.BuiltIn,
        null,
        [],
        false,
        null)
    {
        BuiltIn = new BuiltInSetRef(descriptor.Key, descriptor.Cards.Count, descriptor.Cards.Count > 0 ? descriptor.Cards[0] : null)
    };
}
