using buddy.Common;
using buddy.Features.Guardians;
using buddy.Features.Users;

namespace buddy.Features.FirstThen;

public static class GetFirstThenBoardHandler
{
    public static async Task<Result<GuardianFirstThenResponse>> Handle(
        GetFirstThenBoard query,
        IFirstThenBoardEventStore boards,
        IGuardianLinkEventStore guardians,
        IUserEventStore users,
        CancellationToken cancellationToken)
    {
        var access = await FirstThenAuthorization.CheckManage(query.ChildId, query.UserId, guardians, cancellationToken);

        if (access != FirstThenAccess.Allowed)
        {
            return access.ToDeniedResult<GuardianFirstThenResponse>();
        }

        var board = await boards.FindSnapshotAsync(FirstThenBoardId.ForChild(query.ChildId), cancellationToken);

        return new Result<GuardianFirstThenResponse>.Success(new GuardianFirstThenResponse(
            await FirstThenBoardResponse.ForGuardianAsync(board, users, cancellationToken),
            [.. (board?.RecentFirsts ?? []).Select(FirstThenCardResponse.From)],
            [.. (board?.RecentThens ?? []).Select(FirstThenCardResponse.From)]));
    }
}
