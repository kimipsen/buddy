using buddy.Common;
using buddy.Common.Validation;
using buddy.Features.Guardians;

using FluentValidation;

namespace buddy.Features.FlashCards;

public static class CreateFlashCardSetHandler
{
    public static async Task<Result<FlashCardSet>> Handle(
        CreateFlashCardSet command,
        IValidator<CreateFlashCardSet> validator,
        IFlashCardSetEventStore sets,
        IGuardianLinkEventStore guardians,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        if (await validator.ValidateCommandAsync(command, cancellationToken) is { } problem)
        {
            return new Result<FlashCardSet>.Validation(problem);
        }

        var access = await FlashCardAuthorization.CheckManageLibrary(command.UserId, guardians, cancellationToken);

        if (access != FlashCardAccess.Allowed)
        {
            return access.ToDeniedResult<FlashCardSet>();
        }

        var id = FlashCardSetId.New();
        var created = new FlashCardSetCreated(id, command.UserId, command.Name, command.Kind, command.Math, time.GetUtcNow());

        await sets.CreateAsync(id, [created], cancellationToken);

        return new Result<FlashCardSet>.Success(FlashCardSet.Replay([created]));
    }
}
