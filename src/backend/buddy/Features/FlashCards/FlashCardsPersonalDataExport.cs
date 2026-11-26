using buddy.Common.Erasure;

namespace buddy.Features.FlashCards;

// The "flashCards" section: each child's enabled sets with tiers and finished games.
public sealed class FlashCardsPersonalDataExporter(IFlashCardPracticeEventStore practices) : IPersonalDataExporter
{
    public Type Store => typeof(IFlashCardsStore);

    public string Section => "flashCards";

    public async Task<object?> ExportAsync(ExportSubject subject, CancellationToken cancellationToken)
    {
        List<ExportedFlashCardPractice> children = [];

        foreach (var childId in subject.Children)
        {
            var practice = await practices.FindSnapshotAsync(FlashCardPracticeId.ForChild(childId), cancellationToken);

            if (practice is null)
            {
                continue;
            }

            children.Add(new ExportedFlashCardPractice(
                childId.Value,
                [.. practice.EnabledSets.Select(s => new ExportedEnabledSet(
                    s.SetId.Value,
                    s.SetName,
                    [.. s.Tiers.Select(t => new ExportedTier(t.Id.Value, t.CardCount, t.Stars))]))],
                [.. practice.Games.Select(g => new ExportedGame(g.GameId, g.FinishedOn, g.SetId.Value, g.SetName, g.CardCount, g.KnownCount, g.Stars))]));
        }

        return children;
    }
}

public sealed record ExportedFlashCardPractice(Guid ChildId, IReadOnlyList<ExportedEnabledSet> EnabledSets, IReadOnlyList<ExportedGame> Games);

public sealed record ExportedEnabledSet(Guid SetId, string SetName, IReadOnlyList<ExportedTier> Tiers);

public sealed record ExportedTier(Guid TierId, int CardCount, int Stars);

public sealed record ExportedGame(Guid GameId, DateOnly FinishedOn, Guid SetId, string SetName, int CardCount, int KnownCount, int Stars);
