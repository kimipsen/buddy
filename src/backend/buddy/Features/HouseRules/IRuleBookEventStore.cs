namespace buddy.Features.HouseRules;

public interface IRuleBookEventStore
{
    // Empty for a scope nobody has written a rule for yet -- the stream is created lazily.
    Task<IReadOnlyCollection<RuleBookEvent>> ReadAsync(RuleBookId id, CancellationToken cancellationToken);

    Task<RuleBook?> FindSnapshotAsync(RuleBookId id, CancellationToken cancellationToken);

    Task CreateAsync(RuleBookId id, IReadOnlyCollection<RuleBookEvent> events, CancellationToken cancellationToken);

    Task AppendAsync(RuleBookId id, IReadOnlyCollection<RuleBookEvent> events, CancellationToken cancellationToken);
}
