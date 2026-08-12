namespace buddy.Features.Users;

public interface IUserEventStore
{
    Task<IReadOnlyCollection<UserEvent>> ReadAsync(string keycloakSubject, CancellationToken cancellationToken);

    Task AppendAsync(string keycloakSubject, IReadOnlyCollection<UserEvent> events, CancellationToken cancellationToken);
}
