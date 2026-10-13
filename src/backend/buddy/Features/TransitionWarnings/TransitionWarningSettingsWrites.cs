using buddy.Features.Users;

namespace buddy.Features.TransitionWarnings;

public static class TransitionWarningSettingsWrites
{
    // The settings as the handler decides on them: the stream folded, or the initial settings when
    // no guardian has saved anything yet (Exists = false).
    public static async Task<(TransitionWarningSettings Settings, bool Exists)> LoadForWriteAsync(
        this ITransitionWarningSettingsEventStore store, UserId childId, CancellationToken cancellationToken)
    {
        var current = TransitionWarningSettings.Rehydrate(await store.ReadAsync(TransitionWarningSettingsId.ForChild(childId), cancellationToken));

        return current is null ? (TransitionWarningSettings.ForChild(childId), false) : (current, true);
    }

    // Appends the change, starting the stream lazily on the first write (Question 2).
    public static Task SaveAsync(
        this ITransitionWarningSettingsEventStore store,
        TransitionWarningSettings settings,
        bool exists,
        TransitionWarningEvent change,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        exists
            ? store.AppendAsync(settings.Id, [change], cancellationToken)
            : store.CreateAsync(settings.Id, [new TransitionWarningsStarted(settings.Id, settings.ChildId, now), change], cancellationToken);
}
