namespace buddy.Features.Users;

public sealed record User(
    UserId Id,
    KeycloakSubject KeycloakSubject,
    Email Email,
    string? UserName,
    Name Name,
    bool IsDeleted = false)
{
    public static User? Rehydrate(IEnumerable<UserEvent> events)
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
