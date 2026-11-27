using buddy.Common;
using buddy.Common.Validation;
using buddy.Features.Guardians;

using FluentValidation;

namespace buddy.Features.FlashCards;

public static class UpdateFlashCardHandler
{
    public static async Task<Result<FlashCardSet>> Handle(
        UpdateFlashCard command,
        IValidator<UpdateFlashCard> validator,
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

        if (set is not { IsDeleted: false } || set.FindCard(command.CardId) is not { } card)
        {
            return new Result<FlashCardSet>.NotFound();
        }

        // Only a real change is recorded, so repeating the same PUT is a no-op.
        if (card.Question == command.Question && card.Answer == command.Answer)
        {
            return new Result<FlashCardSet>.Success(set);
        }

        var updated = new FlashCardUpdated(command.SetId, command.CardId, command.Question, command.Answer, command.UserId, time.GetUtcNow());

        await sets.AppendAsync(command.SetId, [updated], cancellationToken);

        return new Result<FlashCardSet>.Success(FlashCardSet.Advance(set, updated));
    }
}
