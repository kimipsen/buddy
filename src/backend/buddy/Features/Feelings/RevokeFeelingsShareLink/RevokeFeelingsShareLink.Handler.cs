using buddy.Common;
using buddy.Features.Guardians;

namespace buddy.Features.Feelings;

public static class RevokeFeelingsShareLinkHandler
{
    public static async Task<Result<Unit>> Handle(
        RevokeFeelingsShareLink command,
        IFeelingsShareTokenEventStore shareTokens,
        IGuardianLinkEventStore guardians,
        TimeProvider time,
        ILogger<RevokeFeelingsShareLink> logger,
        CancellationToken cancellationToken)
    {
        var access = await FeelingsAuthorization.CheckManage(command.ChildId, command.UserId, guardians, cancellationToken);

        if (access != FeelingsAccess.Allowed)
        {
            return access.ToDeniedResult<Unit>();
        }

        var token = FeelingsShareToken.Rehydrate(await shareTokens.ReadAsync(command.ShareLinkId, cancellationToken));

        // A link belonging to another child is as invisible as one that doesn't exist.
        if (token is null || token.ChildId != command.ChildId)
        {
            return new Result<Unit>.NotFound();
        }

        // Revoking twice is an idempotent no-op.
        if (token.IsRevoked)
        {
            return new Result<Unit>.Success(Unit.Value);
        }

        await shareTokens.AppendAsync(
            command.ShareLinkId,
            [new FeelingsShareTokenRevoked(command.ShareLinkId, command.UserId, time.GetUtcNow())],
            cancellationToken);

        logger.ShareLinkRevoked(command.ShareLinkId.Value, command.ChildId.Value, command.UserId.Value);

        return new Result<Unit>.Success(Unit.Value);
    }
}
