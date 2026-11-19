using buddy.Common;

namespace buddy.Features.Users;

public static class GetMeHandler
{
    public static async Task<Result<User>> Handle(GetMe query, IUserEventStore users, CancellationToken cancellationToken)
    {
        var user = await users.FindSnapshotAsync(query.UserId, cancellationToken);

        return user is null or { IsDeleted: true } ? new Result<User>.NotFound() : new Result<User>.Success(user);
    }
}
