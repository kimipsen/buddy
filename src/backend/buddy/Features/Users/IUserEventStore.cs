namespace buddy.Features.Users;

public interface IUserEventStore
{
    // Includes deleted users (Deleted = true), so callers decide what a deleted identity means.
    Task<KeycloakIdentity?> FindIdentityAsync(KeycloakSubject keycloakSubject, CancellationToken cancellationToken);

    // Appends the deletion events and marks the identity Deleted in one transaction, so the user is
    // locked out as soon as the deletion is stored.
    Task DeleteAsync(UserId userId, KeycloakSubject keycloakSubject, IReadOnlyCollection<UserEvent> events, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<UserEvent>> ReadAsync(UserId userId, CancellationToken cancellationToken);

    Task<User?> FindSnapshotAsync(UserId userId, CancellationToken cancellationToken);

    // Both return entries in ascending version order.
    Task<IReadOnlyCollection<UserEventEntry>> ReadForwardAsync(UserId userId, long afterVersion, int take, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<UserEventEntry>> ReadBackwardAsync(UserId userId, long beforeVersion, int take, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<UserEvent>> CreateAsync(KeycloakSubject keycloakSubject, UserId userId, IReadOnlyCollection<UserEvent> events, CancellationToken cancellationToken);

    Task AppendAsync(UserId userId, IReadOnlyCollection<UserEvent> events, CancellationToken cancellationToken);
}
