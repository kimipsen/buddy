using buddy.Common;
using buddy.Common.Validation;
using buddy.Features.Guardians;

using FluentValidation;

namespace buddy.Features.FlashCards;

public static class UpdateFlashCardSetHandler
{
    public static async Task<Result<FlashCardSet>> Handle(
        UpdateFlashCardSet command,
        IValidator<UpdateFlashCardSet> validator,
        IFlashCardSetEventStore sets,
        IGuardianLinkEventStore guardians,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        if (await validator.ValidateCommandAsync(command, cancellationToken) is { } problem)
        {
            return new Result<FlashCardSet>.Validation(problem);
        }

        var set = FlashCardSet.Rehydrate(await sets.ReadAsync(command.SetId, cancellationToken));

        var access = await FlashCardAuthorization.CheckManageSet(set, command.UserId, guardians, cancellationToken);

        if (access != FlashCardAccess.Allowed)
        {
            return access.ToDeniedResult<FlashCardSet>();
        }

        if (set is not { IsDeleted: false })
        {
            return new Result<FlashCardSet>.NotFound();
        }

        if (command.Math is not null && set.Kind != FlashCardSetKind.Math)
        {
            return new Result<FlashCardSet>.Validation(ValidationProblem.Of("Only a math set has a math rule."));
        }

        // Only a real change is recorded, so repeating the same PUT is a no-op.
        if (command.Name == set.Name && (command.Math is null || command.Math == set.Math))
        {
            return new Result<FlashCardSet>.Success(set);
        }

        var updated = new FlashCardSetUpdated(command.SetId, command.Name, command.Math, command.UserId, time.GetUtcNow());

        await sets.AppendAsync(command.SetId, [updated], cancellationToken);

        return new Result<FlashCardSet>.Success(FlashCardSet.Advance(set, updated));
    }
}
