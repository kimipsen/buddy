using System.Security.Claims;

namespace buddy.Features.Users;

public sealed class UserService(IUserEventStore events)
{
    private readonly SemaphoreSlim _creationGate = new(1, 1);

    public async Task<User> GetOrCreateFromClaimsAsync(ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        var keycloakSubject = principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? principal.FindFirstValue(Claims.KeycloakSubject)
            ?? throw new UnauthorizedAccessException("Authenticated user is missing the Keycloak subject claim.");

        await _creationGate.WaitAsync(cancellationToken);
        try
        {
            var existingEvents = await events.ReadAsync(keycloakSubject, cancellationToken);
            var existingUser = Rehydrate(existingEvents);

            if (existingUser is not null)
            {
                return existingUser;
            }

            var emailIsVerified = principal.FindFirstValue(Claims.EmailVerified) is { } emailVerifiedClaim
                && bool.TryParse(emailVerifiedClaim, out var emailVerified)
                && emailVerified;

            var emailClaim = principal.FindFirstValue(ClaimTypes.Email) ?? principal.FindFirstValue(Claims.Email);

            var created = new UserCreated(
                UserId.New(),
                keycloakSubject,
                emailIsVerified
                    ? Email.Verified(emailClaim ?? "")
                    : Email.Unverified(emailClaim ?? ""),
                principal.FindFirstValue(Claims.PreferredUsername),
                Name.New(principal.FindFirstValue(Claims.GivenName) ?? "", principal.FindFirstValue(Claims.FamilyName) ?? ""),
                DateTimeOffset.UtcNow);

            await events.AppendAsync(keycloakSubject, [created], cancellationToken);

            return Rehydrate([created])!;
        }
        finally
        {
            _creationGate.Release();
        }
    }

    private static User? Rehydrate(IEnumerable<UserEvent> events)
    {
        User? user = null;

        foreach (var @event in events)
        {
            user = @event switch
            {
                UserCreated created => new User(
                    created.UserId,
                    created.KeycloakSubject,
                    created.Email,
                    created.UserName,
                    created.Name),
                _ => user
            };
        }

        return user;
    }
}
