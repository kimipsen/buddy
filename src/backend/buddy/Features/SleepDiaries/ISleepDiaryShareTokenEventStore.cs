using buddy.Features.Users;

namespace buddy.Features.SleepDiaries;

public interface ISleepDiaryShareTokenEventStore
{
    Task<IReadOnlyCollection<SleepDiaryShareTokenEvent>> ReadAsync(SleepDiaryShareTokenId id, CancellationToken cancellationToken);

    Task<SleepDiaryShareToken?> FindSnapshotAsync(SleepDiaryShareTokenId id, CancellationToken cancellationToken);

    Task CreateAsync(SleepDiaryShareTokenId id, IReadOnlyCollection<SleepDiaryShareTokenEvent> events, CancellationToken cancellationToken);

    Task AppendAsync(SleepDiaryShareTokenId id, IReadOnlyCollection<SleepDiaryShareTokenEvent> events, CancellationToken cancellationToken);

    Task<SleepDiaryShareTokenDocument?> FindByHashAsync(string tokenHash, CancellationToken cancellationToken);

    // Newest first, revoked ones included -- the caller decides what to show.
    Task<IReadOnlyCollection<SleepDiaryShareTokenDocument>> ListForChildAsync(UserId childId, CancellationToken cancellationToken);
}
