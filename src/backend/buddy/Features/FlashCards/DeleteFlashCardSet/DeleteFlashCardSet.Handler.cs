using buddy.Common;
using buddy.Common.Validation;
using buddy.Features.Guardians;

namespace buddy.Features.FlashCards;

public static class DeleteFlashCardSetHandler
{
    // Soft delete. Deleting an already deleted set succeeds without a new event.
    public static async Task<Result<Unit>> Handle(
        DeleteFlashCardSet command,
        IFlashCardSetEventStore sets,
        IGuardianLinkEventStore guardians,
        IBuiltInCatalogue catalogue,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        // Order: validator, library access (a child is Forbidden), the catalogue check, then the read,
        // which finds no stream for a catalogue id.
        var libraryAccess = await FlashCardAuthorization.CheckManageLibrary(command.UserId, guardians, cancellationToken);

        if (libraryAccess != FlashCardAccess.Allowed)
        {
            return libraryAccess.ToDeniedResult<Unit>();
        }

        if (catalogue.TryFind(command.SetId) is not null)
        {
            return new Result<Unit>.Validation(ValidationProblem.Of("Built-in sets are read-only."));
        }

        var set = FlashCardSet.Rehydrate(await sets.ReadAsync(command.SetId, cancellationToken));

        var access = await FlashCardAuthorization.CheckManageSet(set, command.UserId, guardians, cancellationToken);

        if (access != FlashCardAccess.Allowed)
        {
            return access.ToDeniedResult<Unit>();
        }

        if (set is { IsDeleted: false })
        {
            await sets.AppendAsync(command.SetId, [new FlashCardSetDeleted(command.SetId, command.UserId, time.GetUtcNow())], cancellationToken);
        }

        return new Result<Unit>.Success(Unit.Value);
    }
}
