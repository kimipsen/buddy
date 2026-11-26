using buddy.Common;
using buddy.Features.Guardians;

namespace buddy.Features.FlashCards;

public static class RemoveFlashCardHandler
{
    public static async Task<Result<Unit>> Handle(
        RemoveFlashCard command,
        IFlashCardSetEventStore sets,
        IGuardianLinkEventStore guardians,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        var set = FlashCardSet.Rehydrate(await sets.ReadAsync(command.SetId, cancellationToken));

        var access = await FlashCardAuthorization.CheckManageSet(set, command.UserId, guardians, cancellationToken);

        if (access != FlashCardAccess.Allowed)
        {
            return access.ToDeniedResult<Unit>();
        }

        if (set is not { IsDeleted: false } || set.FindCard(command.CardId) is null)
        {
            return new Result<Unit>.NotFound();
        }

        await sets.AppendAsync(command.SetId, [new FlashCardRemoved(command.SetId, command.CardId, command.UserId, time.GetUtcNow())], cancellationToken);

        return new Result<Unit>.Success(Unit.Value);
    }
}
