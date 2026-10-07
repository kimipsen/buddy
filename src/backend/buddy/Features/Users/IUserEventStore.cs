namespace buddy.Features.Users;

public interface IUserEventStore
{
    // Includes deleted users (Deleted = true), so callers decide what a deleted identity means.
    Task<KeycloakIdentity?> FindIdentityAsync(KeycloakSubject keycloakSubject, CancellationToken cancellationToken);

    // Appends the deletion events and marks the identity Deleted in one transaction, so the user is
    // locked out as soon as the deletion is stored.
    Task DeleteAsync(UserId userId, KeycloakSubject keycloakSubject, IReadOnlyCollection<UserEvent> events, CancellationToken cancellationToken);

    // Masks the user's personal fields (UsersPersonalData's rules), rebuilds the snapshot, then
    // appends UserErased -- in that order, so a failure leaves the user not yet IsErased and the
    // erasure is retried. A snapshot with no stream behind it is deleted instead (StreamErasure).
    // See gdpr-data-protection.md.
    Task EraseAsync(UserId userId, CancellationToken cancellationToken);

    // Adds the user to the erasure ledger (ErasureLedgerEntry, schema "erasure"), which a database
    // restore re-imports -- see gdpr-data-protection.md, Backups.
    Task RecordErasureAsync(UserId userId, CancellationToken cancellationToken);

    // Users whose erasure isn't finished: deleted but not yet erased, or on the ledger but not erased
    // in this database (restored from a backup taken before the erasure). UserErasureService.
    Task<IReadOnlyCollection<UserId>> ListUnfinishedErasuresAsync(CancellationToken cancellationToken);

    Task<IReadOnlyCollection<UserEvent>> ReadAsync(UserId userId, CancellationToken cancellationToken);

    Task<User?> FindSnapshotAsync(UserId userId, CancellationToken cancellationToken);

    // Both return entries in ascending version order.
    Task<IReadOnlyCollection<UserEventEntry>> ReadForwardAsync(UserId userId, long afterVersion, int take, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<UserEventEntry>> ReadBackwardAsync(UserId userId, long beforeVersion, int take, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<UserEvent>> CreateAsync(KeycloakSubject keycloakSubject, UserId userId, IReadOnlyCollection<UserEvent> events, CancellationToken cancellationToken);

    Task AppendAsync(UserId userId, IReadOnlyCollection<UserEvent> events, CancellationToken cancellationToken);
}
