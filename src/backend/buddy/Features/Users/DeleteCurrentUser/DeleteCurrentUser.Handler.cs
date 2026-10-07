using buddy.Common;
using buddy.Features.Guardians;
using buddy.Features.Privacy;

namespace buddy.Features.Users;

public static class DeleteUserHandler
{
    public static async Task<Result<Unit>> Handle(
        DeleteUser command,
        IUserEventStore events,
        IGuardianLinkEventStore guardians,
        UserErasure erasure,
        ILogger<DeleteUser> logger,
        CancellationToken cancellationToken)
    {
        var userId = command.UserId;

        var user = User.Rehydrate(await events.ReadAsync(userId, cancellationToken));

        if (user is null || user.IsDeleted)
        {
            return new Result<Unit>.Success(Unit.Value);
        }

        // A child's account and data are their guardians' to delete (DeleteChild), not the child's.
        if (await guardians.IsChildAsync(userId, cancellationToken))
        {
            return new Result<Unit>.Forbidden();
        }

        // Locks the user out, then erases them and cascades (gdpr-data-protection.md, Question 2).
        await erasure.DeleteAccountAsync(user, cancellationToken);

        logger.UserDeleted(userId.Value);

        return new Result<Unit>.Success(Unit.Value);
    }
}
