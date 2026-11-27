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
        IBuiltInCatalogue catalogue,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        if (await validator.ValidateCommandAsync(command, cancellationToken) is { } problem)
        {
            return new Result<FlashCardSet>.Validation(problem);
        }

        // Order: validator, library access (a child is Forbidden), the catalogue check, then the read,
        // which finds no stream for a catalogue id.
        var libraryAccess = await FlashCardAuthorization.CheckManageLibrary(command.UserId, guardians, cancellationToken);

        if (libraryAccess != FlashCardAccess.Allowed)
        {
            return libraryAccess.ToDeniedResult<FlashCardSet>();
        }

        if (catalogue.TryFind(command.SetId) is not null)
        {
            return new Result<FlashCardSet>.Validation(ValidationProblem.Of("Built-in sets are read-only."));
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
