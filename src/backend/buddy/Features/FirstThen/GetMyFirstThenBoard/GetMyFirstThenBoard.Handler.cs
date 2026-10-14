using buddy.Common;
using buddy.Features.Guardians;

namespace buddy.Features.FirstThen;

public static class GetMyFirstThenBoardHandler
{
    public static async Task<Result<FirstThenBoardResponse>> Handle(
        GetMyFirstThenBoard query,
        IFirstThenBoardEventStore boards,
        IGuardianLinkEventStore guardians,
        CancellationToken cancellationToken)
    {
        var access = await FirstThenAuthorization.CheckOwn(query.UserId, guardians, cancellationToken);

        if (access != FirstThenAccess.Allowed)
        {
            return access.ToDeniedResult<FirstThenBoardResponse>();
        }

        // The board only: the recent cards are the guardian form's.
        var board = await boards.FindSnapshotAsync(FirstThenBoardId.ForChild(query.UserId), cancellationToken);

        return new Result<FirstThenBoardResponse>.Success(FirstThenBoardResponse.From(board));
    }
}
