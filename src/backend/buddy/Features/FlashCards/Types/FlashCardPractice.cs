using System.Collections.Immutable;

using buddy.Common.Aggregates;
using buddy.Features.Users;

namespace buddy.Features.FlashCards;

public sealed record EnabledFlashCardSet(FlashCardSetId SetId, string SetName, ImmutableList<FlashCardTier> Tiers)
{
    public ImmutableList<FlashCardTier> Tiers { get; init; } = Tiers ?? [];

    public FlashCardTier? FindTier(FlashCardTierId tierId) => Tiers.FirstOrDefault(t => t.Id == tierId);
}

// One finished game as the history shows it. The per-card results stay in the event only.
public sealed record FlashCardGameSummary(
    Guid GameId,
    DateOnly FinishedOn,
    FlashCardSetId SetId,
    string SetName,
    int CardCount,
    int KnownCount,
    int Stars);

// One stream per child: which sets are enabled with which tiers, and the finished games. Not part of
// ChildProgress, which is read on every dashboard and snapshotted with every star.
public sealed record FlashCardPractice(
    FlashCardPracticeId Id,
    UserId ChildId,
    ImmutableList<EnabledFlashCardSet> EnabledSets,
    // Oldest first, capped at MaxGames. The events keep the full history.
    ImmutableList<FlashCardGameSummary> Games)
{
    public const int MaxGames = 100;

    // Snapshots deserialised without these members would pass null; read that as empty.
    public ImmutableList<EnabledFlashCardSet> EnabledSets { get; init; } = EnabledSets ?? [];

    public ImmutableList<FlashCardGameSummary> Games { get; init; } = Games ?? [];

    public static FlashCardPractice? Rehydrate(IEnumerable<FlashCardPracticeEvent> events) => EventReplay.Rehydrate(events, Start, Advance);

    public static FlashCardPractice Replay(IEnumerable<FlashCardPracticeEvent> events) => EventReplay.Replay(events, Start, Advance);

    // Not named Apply/Create/Evolve: JasperFx's projection generator scans those names.
    public static FlashCardPractice Start(FlashCardPracticeEvent @event) => @event switch
    {
        FlashCardPracticeStarted started => new FlashCardPractice(started.Id, started.ChildId, [], []),
        _ => throw EventReplay.NotAStartEvent(nameof(FlashCardPractice), @event.EventType)
    };

    public static FlashCardPractice Advance(FlashCardPractice practice, FlashCardPracticeEvent @event) => @event switch
    {
        FlashCardSetEnabled enabled => practice with { EnabledSets = Enable(practice.EnabledSets, enabled) },
        FlashCardSetDisabled disabled => practice with
        {
            EnabledSets = practice.EnabledSets.RemoveAll(s => s.SetId == disabled.SetId)
        },
        FlashCardGameFinished finished => practice with { Games = AddGame(practice.Games, finished) },
        FlashCardPracticeStarted => throw EventReplay.AlreadyStarted(nameof(FlashCardPractice), @event.EventType)
    };

    public EnabledFlashCardSet? FindEnabledSet(FlashCardSetId setId) => EnabledSets.FirstOrDefault(s => s.SetId == setId);

    public FlashCardGameSummary? FindGame(Guid gameId) => Games.FirstOrDefault(g => g.GameId == gameId);

    // Replacing keeps the set's position in the list.
    private static ImmutableList<EnabledFlashCardSet> Enable(ImmutableList<EnabledFlashCardSet> sets, FlashCardSetEnabled enabled)
    {
        var entry = new EnabledFlashCardSet(enabled.SetId, enabled.SetName, [.. enabled.Tiers]);
        var index = sets.FindIndex(s => s.SetId == enabled.SetId);

        return index >= 0 ? sets.SetItem(index, entry) : sets.Add(entry);
    }

    private static ImmutableList<FlashCardGameSummary> AddGame(ImmutableList<FlashCardGameSummary> games, FlashCardGameFinished finished)
    {
        var added = games.Add(new FlashCardGameSummary(
            finished.GameId,
            finished.FinishedOn,
            finished.SetId,
            finished.SetName,
            finished.CardCount,
            finished.Results.Count(r => r.KnewFirstTime),
            finished.Stars));

        return added.Count > MaxGames ? added.RemoveRange(0, added.Count - MaxGames) : added;
    }
}
