using buddy.Features.Users;

namespace buddy.Features.Feelings;

public interface IFeelingsShareTokenEventStore
{
    Task<IReadOnlyCollection<FeelingsShareTokenEvent>> ReadAsync(FeelingsShareTokenId id, CancellationToken cancellationToken);

    Task<FeelingsShareToken?> FindSnapshotAsync(FeelingsShareTokenId id, CancellationToken cancellationToken);

    Task CreateAsync(FeelingsShareTokenId id, IReadOnlyCollection<FeelingsShareTokenEvent> events, CancellationToken cancellationToken);

    Task AppendAsync(FeelingsShareTokenId id, IReadOnlyCollection<FeelingsShareTokenEvent> events, CancellationToken cancellationToken);

    Task<FeelingsShareTokenDocument?> FindByHashAsync(string tokenHash, CancellationToken cancellationToken);

    // Newest first, revoked ones included -- the caller decides what to show.
    Task<IReadOnlyCollection<FeelingsShareTokenDocument>> ListForChildAsync(UserId childId, CancellationToken cancellationToken);
}
