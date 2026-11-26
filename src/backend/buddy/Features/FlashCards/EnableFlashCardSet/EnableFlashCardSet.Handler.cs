using buddy.Common;
using buddy.Common.Validation;
using buddy.Features.Guardians;

using FluentValidation;

namespace buddy.Features.FlashCards;

public static class EnableFlashCardSetHandler
{
    public static async Task<Result<EnabledFlashCardSet>> Handle(
        EnableFlashCardSet command,
        IValidator<EnableFlashCardSet> validator,
        IFlashCardSetEventStore sets,
        IFlashCardPracticeEventStore practices,
        IGuardianLinkEventStore guardians,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        if (await validator.ValidateCommandAsync(command, cancellationToken) is { } problem)
        {
            return new Result<EnabledFlashCardSet>.Validation(problem);
        }

        var childAccess = await FlashCardAuthorization.CheckManageChild(command.ChildId, command.UserId, guardians, cancellationToken);

        if (childAccess != FlashCardAccess.Allowed)
        {
            return childAccess.ToDeniedResult<EnabledFlashCardSet>();
        }

        // The guardian can only hand out sets they can see, and never a deleted one.
        var set = await sets.FindSnapshotAsync(command.SetId, cancellationToken);
        var setAccess = await FlashCardAuthorization.CheckManageSet(set, command.UserId, guardians, cancellationToken);

        if (setAccess != FlashCardAccess.Allowed)
        {
            return setAccess.ToDeniedResult<EnabledFlashCardSet>();
        }

        if (set is not { IsDeleted: false })
        {
            return new Result<EnabledFlashCardSet>.NotFound();
        }

        if (set.UniqueCardCount < 1)
        {
            return new Result<EnabledFlashCardSet>.Validation(ValidationProblem.Of("The set needs at least one card before it can be enabled."));
        }

        var id = FlashCardPracticeId.ForChild(command.ChildId);

        // The version first, so the append below is an expected-version append.
        var version = await practices.ReadVersionAsync(id, cancellationToken);
        var practice = version is null ? null : await practices.FindSnapshotAsync(id, cancellationToken);
        var existing = practice?.FindEnabledSet(command.SetId);

        if (ResolveTiers(command, existing) is not { } tiers)
        {
            return new Result<EnabledFlashCardSet>.Validation(ValidationProblem.Of("A tier id does not belong to this set's current tiers."));
        }

        var enabled = new EnabledFlashCardSet(command.SetId, set.Name, [.. tiers]);

        // Same name and tiers as already enabled: nothing to record.
        if (existing is not null && existing.SetName == enabled.SetName && existing.Tiers.SequenceEqual(enabled.Tiers))
        {
            return new Result<EnabledFlashCardSet>.Success(existing);
        }

        var now = time.GetUtcNow();
        var enabledEvent = new FlashCardSetEnabled(id, command.SetId, enabled.SetName, [.. enabled.Tiers], command.UserId, now);

        if (version is null)
        {
            await practices.CreateAsync(id, [new FlashCardPracticeStarted(id, command.ChildId, now), enabledEvent], cancellationToken);
        }
        else
        {
            await practices.AppendAsync(id, [enabledEvent], cancellationToken);
        }

        return new Result<EnabledFlashCardSet>.Success(enabled);
    }

    // Keeps the id of every tier the guardian names; null when a named id is not one of the
    // set's existing tiers.
    private static List<FlashCardTier>? ResolveTiers(EnableFlashCardSet command, EnabledFlashCardSet? existing)
    {
        List<FlashCardTier> tiers = [];

        foreach (var input in command.Tiers)
        {
            if (input.Id is null)
            {
                tiers.Add(new FlashCardTier(FlashCardTierId.New(), input.CardCount, input.Stars));
            }
            else if (existing?.FindTier(input.Id) is not null)
            {
                tiers.Add(new FlashCardTier(input.Id, input.CardCount, input.Stars));
            }
            else
            {
                return null;
            }
        }

        return tiers;
    }
}
