using buddy.Common;
using buddy.Common.Validation;
using buddy.Features.Guardians;

using FluentValidation;

namespace buddy.Features.Babysitters;

public static class UpdateBabysitterHandler
{
    public static async Task<Result<BabysitterSummary>> Handle(
        UpdateBabysitter command,
        IValidator<UpdateBabysitter> validator,
        IBabysitterListEventStore store,
        IGuardianLinkEventStore guardians,
        CancellationToken cancellationToken)
    {
        if (await validator.ValidateCommandAsync(command, cancellationToken) is { } problem)
        {
            return new Result<BabysitterSummary>.Validation(problem);
        }

        var userId = command.UserId;

        var access = await BabysitterAuthorization.CheckManage(userId, guardians, cancellationToken);

        if (access != BabysitterAccess.Allowed)
        {
            return access.ToDeniedResult<BabysitterSummary>();
        }

        var loaded = await BabysitterListWriter.LoadAsync(store, userId, cancellationToken);

        // Archived babysitters can't be edited -- they only exist so old pickup slots keep resolving.
        if (loaded.List.FindActive(command.BabysitterId) is not { } babysitter)
        {
            return new Result<BabysitterSummary>.NotFound();
        }

        var after = new BabysitterDetails(command.Name.Trim(), FreeText.Normalize(command.ContactInfo));

        if (after == babysitter.Details)
        {
            return new Result<BabysitterSummary>.Success(BabysitterSummary.From(babysitter));
        }

        if (BabysitterRules.NameIsTaken(loaded.List, after.Name, excluding: babysitter.Id))
        {
            return new Result<BabysitterSummary>.Validation(ValidationProblem.Of("A babysitter with this name already exists."));
        }

        var now = DateTimeOffset.UtcNow;
        var changed = new BabysitterDetailsChanged(loaded.List.Id, babysitter.Id, babysitter.Details, after, now);
        var list = await BabysitterListWriter.SaveAsync(store, loaded, [changed], now, cancellationToken);

        return new Result<BabysitterSummary>.Success(BabysitterSummary.From(list.Find(babysitter.Id)!));
    }
}
