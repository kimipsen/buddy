using buddy.Common;
using buddy.Features.Guardians;

namespace buddy.Features.FlashCards;

public static class DeleteFlashCardSetHandler
{
    // Soft delete. Deleting an already deleted set succeeds without a new event.
    public static async Task<Result<Unit>> Handle(
        DeleteFlashCardSet command,
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

        if (set is { IsDeleted: false })
        {
            await sets.AppendAsync(command.SetId, [new FlashCardSetDeleted(command.SetId, command.UserId, time.GetUtcNow())], cancellationToken);
        }

        return new Result<Unit>.Success(Unit.Value);
    }
}
