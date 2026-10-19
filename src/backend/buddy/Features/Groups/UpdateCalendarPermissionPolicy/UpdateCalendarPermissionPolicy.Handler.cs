using buddy.Common;
using buddy.Common.Validation;

using FluentValidation;

namespace buddy.Features.Groups;

public static class UpdateCalendarPermissionPolicyHandler
{
    public static async Task<Result<Unit>> Handle(UpdateCalendarPermissionPolicy command, IValidator<UpdateCalendarPermissionPolicy> validator, IGroupEventStore groups, TimeProvider time, CancellationToken cancellationToken)
    {
        if (await validator.ValidateCommandAsync(command, cancellationToken) is { } problem)
        {
            return new Result<Unit>.Validation(problem);
        }

        var userId = command.UserId;

        var events = await groups.ReadAsync(command.GroupId, cancellationToken);
        var group = Group.Rehydrate(events);

        if (group is null)
        {
            return new Result<Unit>.NotFound();
        }

        var access = GroupAuthorization.CheckManage(group, userId);

        if (access != GroupAccess.Allowed)
        {
            return access.ToDeniedResult<Unit>();
        }

        await groups.AppendAsync(
            command.GroupId,
            [new GroupCalendarPolicyUpdated(command.GroupId, command.Policy, userId, time.GetUtcNow())],
            cancellationToken);

        return new Result<Unit>.Success(Unit.Value);
    }
}
