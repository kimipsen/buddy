using buddy.Common;
using buddy.Features.Guardians;

namespace buddy.Features.TransitionWarnings;

public static class ClearItemWarningOverrideHandler
{
    // Idempotent: clearing an override that isn't there appends nothing. The item itself isn't
    // checked, so an exception for a since-deleted item can still be cleared.
    public static async Task<Result<Unit>> Handle(
        ClearItemWarningOverride command,
        ITransitionWarningSettingsEventStore settings,
        IGuardianLinkEventStore guardians,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        var access = await TransitionWarningsAuthorization.CheckManage(command.ChildId, command.UserId, guardians, cancellationToken);

        if (access != TransitionWarningsAccess.Allowed)
        {
            return access.ToDeniedResult<Unit>();
        }

        var (current, exists) = await settings.LoadForWriteAsync(command.ChildId, cancellationToken);

        if (current.ItemOverrides.GetValueOrDefault(command.ItemId) is { } before)
        {
            var now = time.GetUtcNow();
            await settings.SaveAsync(
                current, exists, new ItemWarningOverrideCleared(current.Id, command.ItemId, before, command.UserId, now), now, cancellationToken);
        }

        return new Result<Unit>.Success(Unit.Value);
    }
}
