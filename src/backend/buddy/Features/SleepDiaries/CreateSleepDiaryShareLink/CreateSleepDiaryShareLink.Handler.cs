using buddy.Common;
using buddy.Common.Validation;
using buddy.Features.Guardians;

using FluentValidation;

namespace buddy.Features.SleepDiaries;

public static class CreateSleepDiaryShareLinkHandler
{
    public static async Task<Result<IssuedSleepDiaryShareLink>> Handle(
        CreateSleepDiaryShareLink command,
        IValidator<CreateSleepDiaryShareLink> validator,
        ISleepDiaryShareTokenEventStore shareTokens,
        IGuardianLinkEventStore guardians,
        CancellationToken cancellationToken)
    {
        if (await validator.ValidateCommandAsync(command, cancellationToken) is { } problem)
        {
            return new Result<IssuedSleepDiaryShareLink>.Validation(problem);
        }

        var access = await SleepDiaryAuthorization.CheckManage(command.ChildId, command.UserId, guardians, cancellationToken);

        if (access != SleepDiaryAccess.Allowed)
        {
            return access.ToDeniedResult<IssuedSleepDiaryShareLink>();
        }

        // Works before any night is logged: the shared view simply shows an empty diary until the
        // guardian fills it in, and always reflects current data (no snapshot at share time).
        var (token, hash) = SleepDiaryShareSecret.Generate();
        var id = SleepDiaryShareTokenId.New();
        var now = DateTimeOffset.UtcNow;

        await shareTokens.CreateAsync(
            id,
            [new SleepDiaryShareTokenCreated(id, command.ChildId, hash, command.UserId, command.ExpiresAt, now)],
            cancellationToken);

        return new Result<IssuedSleepDiaryShareLink>.Success(new IssuedSleepDiaryShareLink(id, token, now, command.ExpiresAt));
    }
}
