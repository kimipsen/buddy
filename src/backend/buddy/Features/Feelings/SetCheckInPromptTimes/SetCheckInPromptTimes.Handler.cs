using buddy.Common;
using buddy.Common.Validation;
using buddy.Features.Guardians;

using FluentValidation;

namespace buddy.Features.Feelings;

public static class SetCheckInPromptTimesHandler
{
    public static async Task<Result<PromptTimesResponse>> Handle(
        SetCheckInPromptTimes command,
        IValidator<SetCheckInPromptTimes> validator,
        IFeelingsJournalEventStore journals,
        IGuardianLinkEventStore guardians,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        if (await validator.ValidateCommandAsync(command, cancellationToken) is { } problem)
        {
            return new Result<PromptTimesResponse>.Validation(problem);
        }

        var access = await FeelingsAuthorization.CheckManage(command.ChildId, command.UserId, guardians, cancellationToken);

        if (access != FeelingsAccess.Allowed)
        {
            return access.ToDeniedResult<PromptTimesResponse>();
        }

        var id = FeelingsJournalId.ForChild(command.ChildId);
        var journal = FeelingsJournal.Rehydrate(await journals.ReadAsync(id, cancellationToken));
        IReadOnlyList<TimeOnly> before = journal?.PromptTimes ?? [];
        IReadOnlyList<TimeOnly> after = [.. command.Times.Order()];

        // The same times (in any order) append nothing, which keeps the PUT idempotent.
        if (before.SequenceEqual(after))
        {
            return new Result<PromptTimesResponse>.Success(new PromptTimesResponse(before));
        }

        var now = time.GetUtcNow();

        await FeelingsJournalWrites.WriteAsync(
            journals,
            id,
            journal,
            new FeelingsJournalStarted(id, command.ChildId, now),
            new CheckInPromptTimesSet(id, before, after, command.UserId, now),
            cancellationToken);

        return new Result<PromptTimesResponse>.Success(new PromptTimesResponse(after));
    }
}
