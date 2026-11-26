namespace buddy.Features.Progress;

public static class AwardFlashCardStarsHandler
{
    public static async Task<AwardFlashCardStarsResult> Handle(AwardFlashCardStars command, IProgressEventStore progress, TimeProvider time, CancellationToken cancellationToken)
    {
        var id = ProgressId.ForChild(command.ChildId);

        // Tracked read: the append below is an expected-version append, so two finishes that both
        // read version N cannot both award (the loser gets a ConcurrencyException -> 409).
        var existing = ChildProgress.Rehydrate(await progress.ReadAsync(id, cancellationToken));
        var current = existing ?? ChildProgress.Initial(id, command.ChildId);

        var already = current.FlashCardStarsOn(command.SetId, command.Day);
        var award = Math.Max(0, command.TierStars - already);

        // Same or lower tier than already paid today: nothing changes, so append nothing.
        if (award == 0)
        {
            return new AwardFlashCardStarsResult(0);
        }

        var now = time.GetUtcNow();
        var events = new List<ProgressEvent>();

        if (existing is null)
        {
            events.Add(new ProgressStarted(id, command.ChildId, now));
        }

        events.Add(new FlashCardStarsAwarded(id, command.SetId, command.Day, award, command.GameId, now));

        // One award can cross several goal posts; unlock each one not unlocked before.
        foreach (var crossed in GoalPostResolver.CrossedBetween(current.GoalPosts, current.TotalStars, current.TotalStars + award))
        {
            if (!current.UnlockedMilestones.Contains(crossed.Threshold))
            {
                events.Add(new MilestoneUnlocked(id, crossed.Threshold, now));
            }
        }

        if (existing is null)
        {
            // A missing stream is not covered by the version tracker; CreateAsync turns a lost
            // creation race into a ConcurrencyException (409), not a 500.
            await progress.CreateAsync(id, events, cancellationToken);
        }
        else
        {
            await progress.AppendAsync(id, events, cancellationToken);
        }

        return new AwardFlashCardStarsResult(award);
    }
}
