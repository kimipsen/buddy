using buddy.Common;
using buddy.Features.Guardians;

namespace buddy.Features.Babysitters;

public static class ArchiveBabysitterHandler
{
    public static async Task<Result<Unit>> Handle(
        ArchiveBabysitter command,
        IBabysitterListEventStore store,
        IGuardianLinkEventStore guardians,
        CancellationToken cancellationToken)
    {
        var userId = command.UserId;

        var access = await BabysitterAuthorization.CheckManage(userId, guardians, cancellationToken);

        if (access != BabysitterAccess.Allowed)
        {
            return access.ToDeniedResult<Unit>();
        }

        var loaded = await BabysitterListWriter.LoadAsync(store, userId, cancellationToken);

        if (loaded.List.Find(command.BabysitterId) is not { } babysitter)
        {
            return new Result<Unit>.NotFound();
        }

        // Pickup slots that reference the babysitter are left alone and keep resolving their name
        // (see docs/backend/analysis/babysitters.md, Question 3).
        if (!babysitter.IsArchived)
        {
            var now = DateTimeOffset.UtcNow;
            await BabysitterListWriter.SaveAsync(store, loaded, [new BabysitterArchived(loaded.List.Id, babysitter.Id, now)], now, cancellationToken);
        }

        return new Result<Unit>.Success(Unit.Value);
    }
}
