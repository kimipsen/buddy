using buddy.Common;
using buddy.Common.Validation;
using buddy.Features.Guardians;

using FluentValidation;

namespace buddy.Features.TaskLibrary;

public static class CreateTaskTemplateHandler
{
    public static async Task<Result<TaskTemplate>> Handle(
        CreateTaskTemplate command,
        IValidator<CreateTaskTemplate> validator,
        ITaskTemplateEventStore templates,
        IGuardianLinkEventStore guardians,
        CancellationToken cancellationToken)
    {
        if (await validator.ValidateCommandAsync(command, cancellationToken) is { } problem)
        {
            return new Result<TaskTemplate>.Validation(problem);
        }

        if (command.UserId is not { } userId)
        {
            return new Result<TaskTemplate>.NotFound();
        }

        var access = await TaskLibraryAuthorization.CheckManage(command.ChildId, userId, guardians, cancellationToken);

        if (access != TaskLibraryAccess.Allowed)
        {
            return access.ToDeniedResult<TaskTemplate>();
        }

        var templateId = TaskTemplateId.New();
        var now = DateTimeOffset.UtcNow;

        var created = new TaskTemplateCreated(templateId, command.ChildId, userId, command.Name, command.Icon, command.Color, now);

        var events = await templates.CreateAsync(templateId, [created], cancellationToken);

        return new Result<TaskTemplate>.Success(TaskTemplate.Rehydrate(events)!);
    }
}
