using buddy.Common;
using buddy.Features.Guardians;

namespace buddy.Features.FlashCards;

public static class DisableFlashCardSetHandler
{
    // Disabling a set that is not enabled succeeds without a new event. The set itself is not
    // looked up, so a deleted set can still be taken off a child.
    public static async Task<Result<Unit>> Handle(
        DisableFlashCardSet command,
        IFlashCardPracticeEventStore practices,
        IGuardianLinkEventStore guardians,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        var access = await FlashCardAuthorization.CheckManageChild(command.ChildId, command.UserId, guardians, cancellationToken);

        if (access != FlashCardAccess.Allowed)
        {
            return access.ToDeniedResult<Unit>();
        }

        var id = FlashCardPracticeId.ForChild(command.ChildId);

        // The version first, so the append below is an expected-version append.
        var version = await practices.ReadVersionAsync(id, cancellationToken);
        var practice = version is null ? null : await practices.FindSnapshotAsync(id, cancellationToken);

        if (practice?.FindEnabledSet(command.SetId) is not null)
        {
            await practices.AppendAsync(id, [new FlashCardSetDisabled(id, command.SetId, command.UserId, time.GetUtcNow())], cancellationToken);
        }

        return new Result<Unit>.Success(Unit.Value);
    }
}
