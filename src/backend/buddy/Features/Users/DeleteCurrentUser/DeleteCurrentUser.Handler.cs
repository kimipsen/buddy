using buddy.Features.Guardians;

namespace buddy.Features.Users;

public static class DeleteUserHandler
{
    public static async Task Handle(
        DeleteUser command,
        IUserEventStore events,
        IKeycloakAdminClient keycloak,
        ILogger<DeleteUser> logger,
        CancellationToken cancellationToken)
    {
        var userId = command.UserId;

        var existingEvents = await events.ReadAsync(userId, cancellationToken);
        var user = User.Rehydrate(existingEvents);

        if (user is null || user.IsDeleted)
        {
            return;
        }

        // First and in one transaction: the event and the identity's Deleted flag, so the user is
        // locked out even if the Keycloak call below fails.
        await events.DeleteAsync(userId, user.KeycloakSubject, [new UserDeleted(userId, DateTimeOffset.UtcNow)], cancellationToken);

        logger.UserDeleted(userId.Value);

        try
        {
            await keycloak.DeleteUserAsync(user.KeycloakSubject, cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            // The deletion itself has happened; the account is locked out of Buddy either way.
            logger.KeycloakAccountDeletionFailed(exception, userId.Value);
        }
    }
}
