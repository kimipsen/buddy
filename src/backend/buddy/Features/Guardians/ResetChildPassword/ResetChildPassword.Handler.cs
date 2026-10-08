using buddy.Features.Users;

namespace buddy.Features.Guardians;

public static class ResetChildPasswordHandler
{
    public static async Task<ResetChildPasswordOutcome> Handle(
        ResetChildPassword command,
        IGuardianLinkEventStore guardianLinks,
        IUserEventStore users,
        IKeycloakAdminClient keycloak,
        ILogger<ResetChildPassword> logger,
        CancellationToken cancellationToken)
    {
        // No active link (a stranger, a former guardian, the child itself) is NotFound, like DeleteChild.
        if (await guardianLinks.FindActiveLinkAsync(command.ChildId, command.GuardianId, cancellationToken) is null
            || await users.FindSnapshotAsync(command.ChildId, cancellationToken) is not { IsDeleted: false } child
            || await keycloak.ResetPasswordAsync(child.KeycloakSubject, cancellationToken) is not { } temporaryPassword)
        {
            return new ResetChildPasswordOutcome.NotFound();
        }

        logger.ChildPasswordReset(command.ChildId.Value, command.GuardianId.Value);

        return new ResetChildPasswordOutcome.Success(child.UserName, temporaryPassword);
    }
}
