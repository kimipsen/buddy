using System.Security.Claims;

namespace buddy.Features.Users;

public sealed class UserService(IUserEventStore events)
{
    public async Task<User> GetOrCreateFromClaimsAsync(ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        var subject = GetKeycloakSubject(principal);

        var userId = await events.FindUserIdAsync(subject, cancellationToken);

        if (userId is not null)
        {
            var existingEvents = await events.ReadAsync(userId, cancellationToken);
            return Rehydrate(existingEvents)!;
        }

        var emailIsVerified = principal.FindFirstValue(Claims.EmailVerified) is { } emailVerifiedClaim
            && bool.TryParse(emailVerifiedClaim, out var emailVerified)
            && emailVerified;

        var emailClaim = principal.FindFirstValue(ClaimTypes.Email) ?? principal.FindFirstValue(Claims.Email);

        var created = new UserCreated(
            UserId.New(),
            subject,
            emailIsVerified
                ? Email.Verified(emailClaim ?? "")
                : Email.Unverified(emailClaim ?? ""),
            principal.FindFirstValue(Claims.PreferredUsername),
            Name.New(principal.FindFirstValue(Claims.GivenName) ?? "", principal.FindFirstValue(Claims.FamilyName) ?? ""),
            DateTimeOffset.UtcNow);

        var resultEvents = await events.CreateAsync(subject, created.UserId, [created], cancellationToken);

        return Rehydrate(resultEvents)!;
    }

    public async Task<IReadOnlyCollection<UserEvent>> GetEventsFromClaimsAsync(ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        var subject = GetKeycloakSubject(principal);

        var userId = await events.FindUserIdAsync(subject, cancellationToken);

        return userId is null
            ? []
            : await events.ReadAsync(userId, cancellationToken);
    }

    public async Task DeleteFromClaimsAsync(ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        var subject = GetKeycloakSubject(principal);

        var userId = await events.FindUserIdAsync(subject, cancellationToken);

        if (userId is null)
        {
            return;
        }

        var existingEvents = await events.ReadAsync(userId, cancellationToken);
        var user = Rehydrate(existingEvents);

        if (user is null || user.IsDeleted)
        {
            return;
        }

        await events.AppendAsync(userId, [new UserDeleted(userId, DateTimeOffset.UtcNow)], cancellationToken);
    }

    private static KeycloakSubject GetKeycloakSubject(ClaimsPrincipal principal)
    {
        var keycloakSubject = principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? principal.FindFirstValue(Claims.KeycloakSubject)
            ?? throw new UnauthorizedAccessException("Authenticated user is missing the Keycloak subject claim.");

        return KeycloakSubject.New(keycloakSubject);
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
                UserDeleted => user! with { IsDeleted = true },
                _ => user
            };
        }

        return user;
    }
}
