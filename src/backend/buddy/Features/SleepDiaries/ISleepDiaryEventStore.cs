namespace buddy.Features.SleepDiaries;

public interface ISleepDiaryEventStore
{
    // Empty for a child whose diary has never been written to -- the stream is created lazily.
    Task<IReadOnlyCollection<SleepDiaryEvent>> ReadAsync(SleepDiaryId id, CancellationToken cancellationToken);

    Task<SleepDiary?> FindSnapshotAsync(SleepDiaryId id, CancellationToken cancellationToken);

    Task CreateAsync(SleepDiaryId id, IReadOnlyCollection<SleepDiaryEvent> events, CancellationToken cancellationToken);

    Task AppendAsync(SleepDiaryId id, IReadOnlyCollection<SleepDiaryEvent> events, CancellationToken cancellationToken);
}
