namespace buddy.Features.Users;

public interface IUserEventStore
{
    Task<UserId?> FindUserIdAsync(KeycloakSubject keycloakSubject, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<UserEvent>> ReadAsync(UserId userId, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<UserEventEntry>> ReadPageAsync(UserId userId, long afterVersion, int pageSize, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<UserEvent>> CreateAsync(KeycloakSubject keycloakSubject, UserId userId, IReadOnlyCollection<UserEvent> events, CancellationToken cancellationToken);

    Task AppendAsync(UserId userId, IReadOnlyCollection<UserEvent> events, CancellationToken cancellationToken);
}
