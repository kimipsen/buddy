using System.Collections.Immutable;

using buddy.Common.Validation;
using buddy.Features.Guardians;
using buddy.Features.Progress;
using buddy.Features.Users;

using FluentValidation;

using Wolverine;

namespace buddy.Features.FlashCards;

public static class FinishFlashCardGameHandler
{
    // The order is fixed (docs: implementation plan, D2 "Replay contract"):
    //   authenticate the child -> validate shape -> practice version, then snapshot -> replay check
    //   (snapshot, then typed event query) -> progress GameId recovery lookup -> tier lookup ->
    //   nested AwardFlashCardStars -> append FlashCardGameFinished.
    // Two writes, no transaction: the GameId recovery lookup closes the window between them.
    public static async Task<FinishFlashCardGameOutcome> Handle(
        FinishFlashCardGame command,
        IValidator<FinishFlashCardGame> validator,
        IFlashCardPracticeEventStore practices,
        IFlashCardSetEventStore sets,
        IProgressEventStore progress,
        IGuardianLinkEventStore guardians,
        IUserEventStore users,
        IMessageBus bus,
        TimeProvider time,
        FlashCardsMetrics metrics,
        ILogger<FinishFlashCardGame> logger,
        CancellationToken cancellationToken)
    {
        if (await FlashCardAuthorization.CheckPlay(command.ChildId, guardians, cancellationToken) != FlashCardAccess.Allowed)
        {
            return new FinishFlashCardGameOutcome.NotFound();
        }

        if (await validator.ValidateCommandAsync(command, cancellationToken) is { } problem)
        {
            return new FinishFlashCardGameOutcome.Validation(problem);
        }

        var id = FlashCardPracticeId.ForChild(command.ChildId);
        var progressId = ProgressId.ForChild(command.ChildId);

        // The practice VERSION first, then the snapshot -- never the reverse. The snapshot is at
        // that version or newer. If newer (a concurrent finish or a guardian's config change landed
        // between the two reads), the append at the observed version throws ConcurrencyException and
        // the client retries (409). Reading the snapshot first would let this decide on version N
        // data and still append at N+1: a second finish for one GameId, or a game on a tier that was
        // just removed.
        var version = await practices.ReadVersionAsync(id, cancellationToken);

        // No practice stream: nothing was ever enabled, so no award can exist either.
        if (version is null || await practices.FindSnapshotAsync(id, cancellationToken) is not { } practice)
        {
            return new FinishFlashCardGameOutcome.GameNoLongerAvailable();
        }

        // Replay: decided by the stored game, never by the day, the config or the request's results.
        var stored = practice.FindGame(command.GameId)
            ?? (await practices.FindFinishedGameAsync(id, command.GameId, cancellationToken) is { } older ? FlashCardGameSummary.From(older) : null);

        if (stored is not null)
        {
            if (stored.SetId != command.SetId || stored.TierId != command.TierId)
            {
                return new FinishFlashCardGameOutcome.GameIdReused();
            }

            const string replay = "replay";
            metrics.Finished(FlashCardFinishOutcome.Replay);
            logger.GameFinished(command.ChildId.Value, command.GameId, command.SetId.Value, command.TierId.Value, stored.Stars, replay);

            return new FinishFlashCardGameOutcome.Success(new FinishedFlashCardGame(stored.Stars, await TotalStarsAsync(progress, progressId, cancellationToken), stored));
        }

        var day = await UserLocalDate.TodayAsync(command.ChildId, users, time, cancellationToken);

        if (day is null)
        {
            return new FinishFlashCardGameOutcome.NotFound();
        }

        var set = await sets.FindSnapshotAsync(command.SetId, cancellationToken);

        // Recovery lookup BEFORE the tier check: stars already granted stay (a disabled set or
        // removed tier must not strand them without a history entry).
        var award = await progress.FindFlashCardAwardAsync(progressId, command.GameId, cancellationToken);

        int stars;
        FlashCardFinishOutcome outcome;
        ImmutableArray<FlashCardResult> results;

        if (award is not null)
        {
            if (award.SetId != command.SetId.Value)
            {
                return new FinishFlashCardGameOutcome.GameIdReused();
            }

            if (set is null)
            {
                return new FinishFlashCardGameOutcome.GameNoLongerAvailable();
            }

            if (BuildResults(command, set, out results) is { } resultsProblem)
            {
                return new FinishFlashCardGameOutcome.Validation(resultsProblem);
            }

            stars = award.Stars;
            outcome = FlashCardFinishOutcome.Recovered;
        }
        else
        {
            var tier = practice.FindEnabledSet(command.SetId)?.FindTier(command.TierId);

            // The one case that strands a game: the config the stars come from is gone and the
            // server never trusts a client-sent star value. A soft-deleted set is still playable
            // to the end (R8), so IsDeleted is not checked.
            if (tier is null || set is null)
            {
                return new FinishFlashCardGameOutcome.GameNoLongerAvailable();
            }

            if (command.Results.Count != tier.CardCount)
            {
                return new FinishFlashCardGameOutcome.Validation(ValidationProblem.Of($"The tier has {tier.CardCount} cards; send one result for each."));
            }

            if (BuildResults(command, set, out results) is { } resultsProblem)
            {
                return new FinishFlashCardGameOutcome.Validation(resultsProblem);
            }

            // Not swallowed: the stars are the point of this request. A lost race surfaces as a
            // ConcurrencyException (409) and the retry takes the replay or recovery branch.
            var awarded = await bus.InvokeAsync<AwardFlashCardStarsResult>(
                new AwardFlashCardStars(command.ChildId, command.SetId.Value, day.Value, tier.Stars, command.GameId),
                cancellationToken);

            stars = awarded.Awarded;
            outcome = stars > 0 ? FlashCardFinishOutcome.Awarded : FlashCardFinishOutcome.Zero;
        }

        var finished = new FlashCardGameFinished(
            id,
            command.GameId,
            command.SetId,
            set.Name,
            command.TierId,
            day.Value,
            results.Length,
            results,
            stars,
            time.GetUtcNow());

        // Expected-version append (the version observed above).
        await practices.AppendAsync(id, [finished], cancellationToken);

        metrics.Finished(outcome);

        if (outcome == FlashCardFinishOutcome.Recovered)
        {
            logger.GameRecovered(command.ChildId.Value, command.GameId);
        }

        var outcomeTag = outcome.Tag();
        logger.GameFinished(command.ChildId.Value, command.GameId, command.SetId.Value, command.TierId.Value, stars, outcomeTag);

        return new FinishFlashCardGameOutcome.Success(new FinishedFlashCardGame(
            stars,
            await TotalStarsAsync(progress, progressId, cancellationToken),
            FlashCardGameSummary.From(finished)));
    }

    private static async Task<int> TotalStarsAsync(IProgressEventStore progress, ProgressId id, CancellationToken cancellationToken) =>
        (await progress.FindSnapshotAsync(id, cancellationToken))?.TotalStars ?? 0;

    // Question and answer come from the server, by CardKey, never from the client. A card that no
    // longer exists (removed or a changed math rule) falls back to the echoed text for that slot;
    // without an echo the key is unknown. Returns the problem, or null with the results set.
    private static ValidationProblem? BuildResults(FinishFlashCardGame command, FlashCardSet set, out ImmutableArray<FlashCardResult> results)
    {
        var built = ImmutableArray.CreateBuilder<FlashCardResult>(command.Results.Count);

        foreach (var result in command.Results.OrderBy(r => r.Slot))
        {
            var text = Lookup(set, result.CardKey) ?? (result.Question is { } q && result.Answer is { } a ? (q, a) : null);

            if (text is not var (question, answer))
            {
                results = [];
                return new ValidationProblem(new Dictionary<string, string[]>
                {
                    [$"results[{result.Slot}].cardKey"] = ["The card key does not belong to this set."]
                });
            }

            built.Add(new FlashCardResult(result.Slot, result.CardKey, question, answer, result.KnewFirstTime, result.KnewFirstTime ? 1 : 2));
        }

        results = built.MoveToImmutable();
        return null;
    }

    private static (string Question, string Answer)? Lookup(FlashCardSet set, string cardKey)
    {
        if (set.Math is { } rule)
        {
            return MathDeck.TryGetCard(rule, cardKey, out var card) ? (card.Question, card.Answer) : null;
        }

        return Guid.TryParse(cardKey, out var guid) && set.FindCard(new FlashCardId(guid)) is { } found
            ? (found.Question, found.Answer)
            : null;
    }
}
