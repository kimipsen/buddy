using buddy.Common.Erasure;

namespace buddy.Features.FlashCards;

// The "flashCards" section: each child's enabled sets with tiers, and every finished game with its
// per-card results. Read from the stream, not the snapshot: the snapshot caps the games at
// FlashCardPractice.MaxGames and keeps no per-card results.
public sealed class FlashCardsPersonalDataExporter(IFlashCardPracticeEventStore practices) : IPersonalDataExporter
{
    public Type Store => typeof(IFlashCardsStore);

    public string Section => "flashCards";

    public async Task<object?> ExportAsync(ExportSubject subject, CancellationToken cancellationToken)
    {
        List<ExportedFlashCardPractice> children = [];

        foreach (var childId in subject.Children)
        {
            var events = await practices.ReadAsync(FlashCardPracticeId.ForChild(childId), cancellationToken);

            if (FlashCardPractice.Rehydrate(events) is not { } practice)
            {
                continue;
            }

            children.Add(new ExportedFlashCardPractice(
                childId.Value,
                [.. practice.EnabledSets.Select(s => new ExportedEnabledSet(
                    s.SetId.Value,
                    s.SetName,
                    [.. s.Tiers.Select(t => new ExportedTier(t.Id.Value, t.CardCount, t.Stars))]))],
                [.. events.Select(e => e.Value).OfType<FlashCardGameFinished>().Select(g => new ExportedGame(
                    g.GameId,
                    g.FinishedOn,
                    g.SetId.Value,
                    g.SetName,
                    g.TierId.Value,
                    g.CardCount,
                    g.Stars,
                    [.. g.Results.Select(r => new ExportedCardResult(r.Slot, r.Question, r.Answer, r.KnewFirstTime, r.TimesShown))]))]));
        }

        return children;
    }
}

public sealed record ExportedFlashCardPractice(Guid ChildId, IReadOnlyList<ExportedEnabledSet> EnabledSets, IReadOnlyList<ExportedGame> Games);

public sealed record ExportedEnabledSet(Guid SetId, string SetName, IReadOnlyList<ExportedTier> Tiers);

public sealed record ExportedTier(Guid TierId, int CardCount, int Stars);

public sealed record ExportedGame(
    Guid GameId,
    DateOnly FinishedOn,
    Guid SetId,
    string SetName,
    Guid TierId,
    int CardCount,
    int Stars,
    IReadOnlyList<ExportedCardResult> Results);

// CardKey is internal bookkeeping and left out.
public sealed record ExportedCardResult(int Slot, string Question, string Answer, bool KnewFirstTime, int TimesShown);
