namespace buddy.Features.Progress;

public static class RecordStarChangeHandler
{
    public static async Task Handle(RecordStarChange command, IProgressEventStore progress, CancellationToken cancellationToken)
    {
        var id = ProgressId.ForChild(command.ChildId);
        var existingEvents = await progress.ReadAsync(id, cancellationToken);
        var existing = ChildProgress.Rehydrate(existingEvents);
        var current = existing ?? ChildProgress.Initial(id, command.ChildId);

        var occurrence = new OccurrenceKey(command.ItemId, command.OccurrenceDate, command.Target);
        var alreadyAwarded = current.AwardedOccurrences.Contains(occurrence);

        // Mirrors SetTaskCompletionHandler's own before == after guard -- nothing changed for
        // Progress either, so append nothing.
        if (command.IsCompleted == alreadyAwarded)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var newEvents = new List<ProgressEvent>();

        if (existing is null)
        {
            newEvents.Add(new ProgressStarted(id, command.ChildId, now));
        }

        if (command.IsCompleted)
        {
            newEvents.Add(new StarAwarded(id, command.ItemId, command.OccurrenceDate, now, command.Target));

            var crossed = GoalPostResolver.AtThreshold(current.GoalPosts, current.TotalStars + 1);

            if (crossed is not null && !current.UnlockedMilestones.Contains(crossed.Threshold))
            {
                newEvents.Add(new MilestoneUnlocked(id, crossed.Threshold, now));
            }
        }
        else
        {
            newEvents.Add(new StarRevoked(id, command.ItemId, command.OccurrenceDate, now, command.Target));
        }

        if (existing is null)
        {
            await progress.CreateAsync(id, newEvents, cancellationToken);
        }
        else
        {
            await progress.AppendAsync(id, newEvents, cancellationToken);
        }
    }
}
