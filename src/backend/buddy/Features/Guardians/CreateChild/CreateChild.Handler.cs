using buddy.Features.Users;

namespace buddy.Features.Guardians;

public static class CreateChildHandler
{
    public static async Task<CreateChildOutcome> Handle(
        CreateChild command,
        IKeycloakAdminClient keycloak,
        IGuardianLinkEventStore guardianLinks,
        CancellationToken cancellationToken)
    {
        if (command.GuardianId is not { } guardianId)
        {
            return new CreateChildOutcome.Unauthenticated();
        }

        var provisioning = await keycloak.CreateChildUserAsync(
            command.GivenName,
            command.FamilyName,
            command.Username,
            cancellationToken);

        if (provisioning is KeycloakCreateUserResult.UsernameUnavailable)
        {
            return new CreateChildOutcome.UsernameUnavailable();
        }

        var provisioned = provisioning switch
        {
            KeycloakCreateUserResult.Success(var user) => user,
            KeycloakCreateUserResult.UsernameUnavailable => throw new InvalidOperationException("Username availability changed while creating the Keycloak user."),
        };

        var now = DateTimeOffset.UtcNow;
        var childId = UserId.New();
        var linkId = GuardianLinkId.New();

        // Email.Unverified("") reuses the existing "no email" convention GetOrCreateUserHandler
        // already produces for any OIDC principal with no email claim -- no schema change needed.
        // Fully qualified: unqualified "Email" here would resolve to the sibling "buddy.Email"
        // namespace (enclosing-namespace lookup wins over the using for buddy.Features.Users).
        var userCreated = new UserCreated(childId, provisioned.Subject, buddy.Features.Users.Email.Unverified(""), provisioned.Username, Name.New(command.GivenName, command.FamilyName), now);
        var guardianLinked = new GuardianLinked(linkId, childId, guardianId, command.Kind, now);

        var (userEvents, guardianEvents) = await guardianLinks.CreateChildAndLinkAsync(
            provisioned.Subject, childId, [userCreated], linkId, [guardianLinked], cancellationToken);

        var child = User.Rehydrate(userEvents)!;
        var link = GuardianLink.Rehydrate(guardianEvents)!;

        return new CreateChildOutcome.Success(child, link, provisioned.Username, provisioned.TemporaryPassword);
    }
}
