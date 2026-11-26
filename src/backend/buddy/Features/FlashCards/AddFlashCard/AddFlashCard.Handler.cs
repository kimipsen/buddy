using buddy.Common;
using buddy.Common.Validation;
using buddy.Features.Guardians;

using FluentValidation;

namespace buddy.Features.FlashCards;

public static class AddFlashCardHandler
{
    public static async Task<Result<FlashCardSet>> Handle(
        AddFlashCard command,
        IValidator<AddFlashCard> validator,
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

        if (set.Kind != FlashCardSetKind.Text)
        {
            return new Result<FlashCardSet>.Validation(ValidationProblem.Of("Cards can only be added to a text set."));
        }

        if (set.Cards.Count >= FlashCardLimits.MaxCardsPerSet)
        {
            return new Result<FlashCardSet>.Validation(ValidationProblem.Of($"A set holds at most {FlashCardLimits.MaxCardsPerSet} cards."));
        }

        var added = new FlashCardAdded(
            command.SetId,
            new FlashCard(FlashCardId.New(), command.Question, command.Answer),
            command.UserId,
            time.GetUtcNow());

        await sets.AppendAsync(command.SetId, [added], cancellationToken);

        return new Result<FlashCardSet>.Success(FlashCardSet.Advance(set, added));
    }
}
