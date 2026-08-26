namespace buddy.Features.Progress;

public static class RecordStarChangeHandler
{
    // Deliberately a fixed list for this sketch -- no per-child configuration yet.
    private static readonly int[] MilestoneThresholds = [5, 10, 25, 50, 100];

    public static async Task Handle(RecordStarChange command, IProgressEventStore progress, CancellationToken cancellationToken)
    {
        var id = ProgressId.ForChild(command.ChildId);
        var existingEvents = await progress.ReadAsync(id, cancellationToken);
        var current = ChildProgress.Rehydrate(existingEvents);

        var occurrence = (command.ItemId, command.OccurrenceDate);
        var alreadyAwarded = current?.AwardedOccurrences.Contains(occurrence) ?? false;

        // Mirrors SetTaskCompletionHandler's own before == after guard -- nothing changed for
        // Progress either, so append nothing.
        if (command.IsCompleted == alreadyAwarded)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var newEvents = new List<ProgressEvent>();

        if (current is null)
        {
            newEvents.Add(new ProgressStarted(id, command.ChildId, now));
        }

        if (command.IsCompleted)
        {
            newEvents.Add(new StarAwarded(id, command.ItemId, command.OccurrenceDate, now));

            var totalAfter = (current?.TotalStars ?? 0) + 1;
            var alreadyUnlocked = current?.UnlockedMilestones ?? System.Collections.Immutable.ImmutableHashSet<int>.Empty;
            var crossed = Array.Find(MilestoneThresholds, t => t == totalAfter && !alreadyUnlocked.Contains(t));

            if (crossed != default)
            {
                newEvents.Add(new MilestoneUnlocked(id, crossed, now));
            }
        }
        else
        {
            newEvents.Add(new StarRevoked(id, command.ItemId, command.OccurrenceDate, now));
        }

        if (current is null)
        {
            await progress.CreateAsync(id, newEvents, cancellationToken);
        }
        else
        {
            await progress.AppendAsync(id, newEvents, cancellationToken);
        }
    }
}
