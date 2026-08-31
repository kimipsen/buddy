using System.Collections.Immutable;

using buddy.Common;
using buddy.Common.Validation;
using buddy.Features.Calendars;
using buddy.Features.Guardians;

using FluentValidation;

namespace buddy.Features.Progress;

public static class ConfigureGoalPostsHandler
{
    public static async Task<Result<ProgressSummary>> Handle(
        ConfigureGoalPosts command,
        IValidator<ConfigureGoalPosts> validator,
        IProgressEventStore progress,
        IGuardianLinkEventStore guardians,
        CancellationToken cancellationToken)
    {
        if (await validator.ValidateCommandAsync(command, cancellationToken) is { } problem)
        {
            return new Result<ProgressSummary>.Validation(problem);
        }

        if (command.UserId is not { } userId)
        {
            return new Result<ProgressSummary>.NotFound();
        }

        var access = await ProgressAuthorization.CheckManage(command.ChildId, userId, guardians, cancellationToken);

        if (access != ProgressAccess.Allowed)
        {
            return access.ToDeniedResult<ProgressSummary>();
        }

        var id = ProgressId.ForChild(command.ChildId);
        var existingEvents = await progress.ReadAsync(id, cancellationToken);
        var current = ChildProgress.Rehydrate(existingEvents);

        if (current is not null && current.GoalPosts.SequenceEqual(command.GoalPosts))
        {
            // Idempotent, same rationale as UpdateCalendarIconHandler's already-there check.
            return new Result<ProgressSummary>.Success(ProgressSummary.From(current));
        }

        var now = DateTimeOffset.UtcNow;
        var configured = new GoalPostsConfigured(id, command.GoalPosts, now);

        if (current is null)
        {
            await progress.CreateAsync(id, [new ProgressStarted(id, command.ChildId, now), configured], cancellationToken);

            current = new ChildProgress(
                id,
                command.ChildId,
                0,
                ImmutableHashSet<(CalendarItemId, DateOnly, Guid?)>.Empty,
                ImmutableHashSet<int>.Empty,
                ImmutableArray<GoalPost>.Empty);
        }
        else
        {
            await progress.AppendAsync(id, [configured], cancellationToken);
        }

        return new Result<ProgressSummary>.Success(ProgressSummary.From(current with { GoalPosts = command.GoalPosts }));
    }
}
