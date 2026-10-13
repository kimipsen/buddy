using buddy.Common;
using buddy.Common.Validation;
using buddy.Features.Guardians;

using FluentValidation;

namespace buddy.Features.Feelings;

public static class CreateFeelingsShareLinkHandler
{
    public static async Task<Result<IssuedFeelingsShareLink>> Handle(
        CreateFeelingsShareLink command,
        IValidator<CreateFeelingsShareLink> validator,
        IFeelingsShareTokenEventStore shareTokens,
        IGuardianLinkEventStore guardians,
        TimeProvider time,
        ILogger<CreateFeelingsShareLink> logger,
        CancellationToken cancellationToken)
    {
        if (await validator.ValidateCommandAsync(command, cancellationToken) is { } problem)
        {
            return new Result<IssuedFeelingsShareLink>.Validation(problem);
        }

        var access = await FeelingsAuthorization.CheckManage(command.ChildId, command.UserId, guardians, cancellationToken);

        if (access != FeelingsAccess.Allowed)
        {
            return access.ToDeniedResult<IssuedFeelingsShareLink>();
        }

        // Works before any check-in exists: the shared view reads current data on every request.
        var (token, hash) = FeelingsShareSecret.Generate();
        var id = FeelingsShareTokenId.New();
        var now = time.GetUtcNow();

        await shareTokens.CreateAsync(
            id,
            [new FeelingsShareTokenCreated(id, command.ChildId, hash, command.UserId, command.ExpiresAt, now)],
            cancellationToken);

        logger.ShareLinkCreated(id.Value, command.ChildId.Value, command.UserId.Value, command.ExpiresAt);

        return new Result<IssuedFeelingsShareLink>.Success(new IssuedFeelingsShareLink(id, token, now, command.ExpiresAt));
    }
}
