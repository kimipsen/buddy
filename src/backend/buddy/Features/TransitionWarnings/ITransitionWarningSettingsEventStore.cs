namespace buddy.Features.TransitionWarnings;

public interface ITransitionWarningSettingsEventStore
{
    Task<IReadOnlyCollection<TransitionWarningEvent>> ReadAsync(TransitionWarningSettingsId id, CancellationToken cancellationToken);

    Task<TransitionWarningSettings?> FindSnapshotAsync(TransitionWarningSettingsId id, CancellationToken cancellationToken);

    Task CreateAsync(TransitionWarningSettingsId id, IReadOnlyCollection<TransitionWarningEvent> events, CancellationToken cancellationToken);

    Task AppendAsync(TransitionWarningSettingsId id, IReadOnlyCollection<TransitionWarningEvent> events, CancellationToken cancellationToken);
}
