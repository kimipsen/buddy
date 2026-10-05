using buddy.Common;
using buddy.Features.Guardians;

namespace buddy.Features.SleepDiaries;

public static class RevokeSleepDiaryShareLinkHandler
{
    public static async Task<Result<Unit>> Handle(
        RevokeSleepDiaryShareLink command,
        ISleepDiaryShareTokenEventStore shareTokens,
        IGuardianLinkEventStore guardians,
        CancellationToken cancellationToken)
    {
        var access = await SleepDiaryAuthorization.CheckManage(command.ChildId, command.UserId, guardians, cancellationToken);

        if (access != SleepDiaryAccess.Allowed)
        {
            return access.ToDeniedResult<Unit>();
        }

        var token = SleepDiaryShareToken.Rehydrate(await shareTokens.ReadAsync(command.ShareLinkId, cancellationToken));

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
            [new SleepDiaryShareTokenRevoked(command.ShareLinkId, command.UserId, DateTimeOffset.UtcNow)],
            cancellationToken);

        return new Result<Unit>.Success(Unit.Value);
    }
}
