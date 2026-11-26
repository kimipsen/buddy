using buddy.Common;
using buddy.Features.Guardians;

namespace buddy.Features.FlashCards;

public static class GetChildFlashCardsHandler
{
    // A child with no practice stream yet has nothing enabled: an empty practice, not NotFound.
    public static async Task<Result<FlashCardPractice>> Handle(
        GetChildFlashCards query,
        IFlashCardPracticeEventStore practices,
        IGuardianLinkEventStore guardians,
        CancellationToken cancellationToken)
    {
        var access = await FlashCardAuthorization.CheckViewChild(query.ChildId, query.UserId, guardians, cancellationToken);

        if (access != FlashCardAccess.Allowed)
        {
            return access.ToDeniedResult<FlashCardPractice>();
        }

        var id = FlashCardPracticeId.ForChild(query.ChildId);
        var practice = await practices.FindSnapshotAsync(id, cancellationToken) ?? new FlashCardPractice(id, query.ChildId, [], []);

        return new Result<FlashCardPractice>.Success(practice);
    }
}
