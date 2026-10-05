using buddy.Common;
using buddy.Common.Validation;
using buddy.Features.Guardians;

using FluentValidation;

namespace buddy.Features.Babysitters;

public static class AddBabysitterHandler
{
    public static async Task<Result<BabysitterSummary>> Handle(
        AddBabysitter command,
        IValidator<AddBabysitter> validator,
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
        var name = command.Name.Trim();

        if (loaded.List.ActiveBabysitters.Count() >= BabysitterRules.MaxActiveBabysitters)
        {
            return new Result<BabysitterSummary>.Validation(ValidationProblem.Of($"A guardian can have at most {BabysitterRules.MaxActiveBabysitters} babysitters."));
        }

        if (BabysitterRules.NameIsTaken(loaded.List, name))
        {
            return new Result<BabysitterSummary>.Validation(ValidationProblem.Of("A babysitter with this name already exists."));
        }

        var now = DateTimeOffset.UtcNow;
        var babysitterId = BabysitterId.New();
        var added = new BabysitterAdded(loaded.List.Id, babysitterId, name, FreeText.Normalize(command.ContactInfo), now);
        var list = await BabysitterListWriter.SaveAsync(store, loaded, [added], now, cancellationToken);

        return new Result<BabysitterSummary>.Success(BabysitterSummary.From(list.Find(babysitterId)!));
    }
}
