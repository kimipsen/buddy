using buddy.Common;
using buddy.Common.Validation;
using buddy.Features.Users;

using FluentValidation;

namespace buddy.Features.Guardians;

public static class UpdateChildHomeworkChunksHandler
{
    public static async Task<Result<ChildSummary>> Handle(
        UpdateChildHomeworkChunks command,
        IValidator<UpdateChildHomeworkChunks> validator,
        IGuardianLinkEventStore guardianLinks,
        IUserEventStore users,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        if (await validator.ValidateCommandAsync(command, cancellationToken) is { } problem)
        {
            return new Result<ChildSummary>.Validation(problem);
        }

        // Collapsed to NotFound rather than Forbidden -- same "can't distinguish no-such-child from
        // not-your-child" precedent UpdateChildVisualTimersHandler follows for this exact lookup.
        var link = await guardianLinks.FindActiveLinkAsync(command.ChildId, command.GuardianId, cancellationToken);

        if (link is null)
        {
            return new Result<ChildSummary>.NotFound();
        }

        var existingEvents = await users.ReadAsync(command.ChildId, cancellationToken);
        var child = User.Rehydrate(existingEvents);

        if (child is null || child.IsDeleted)
        {
            return new Result<ChildSummary>.NotFound();
        }

        if (child.HomeworkChunks != command.HomeworkChunks)
        {
            var updated = new HomeworkChunksUpdated(command.ChildId, child.HomeworkChunks, command.HomeworkChunks, time.GetUtcNow());
            await users.AppendAsync(command.ChildId, [updated], cancellationToken);
            child = child with { HomeworkChunks = command.HomeworkChunks };
        }

        return new Result<ChildSummary>.Success(new ChildSummary(
            child.Id, child.Name, new GuardianLinkId(link.GuardianLinkId), link.Kind, child.Language, child.TimeZoneId, child.ShowVisualTimers, child.HomeworkChunks));
    }
}
