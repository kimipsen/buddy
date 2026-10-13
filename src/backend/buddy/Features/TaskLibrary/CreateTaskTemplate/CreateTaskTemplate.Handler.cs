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

        var userId = command.UserId;

        var access = await TaskLibraryAuthorization.CheckManage(command.ChildId, userId, guardians, cancellationToken);

        if (access != TaskLibraryAccess.Allowed)
        {
            return access.ToDeniedResult<TaskTemplate>();
        }

        var templateId = TaskTemplateId.New();
        var now = DateTimeOffset.UtcNow;

        var created = new TaskTemplateCreated(templateId, command.ChildId, userId, command.Name, command.Icon, command.Color, now, command.Kind);
        var options = new ChecklistOptions(command.EveningBeforeFrom);

        TaskTemplateEvent[] newEvents = options == ChecklistOptions.None
            ? [created]
            : [created, new ChecklistOptionsChanged(templateId, ChecklistOptions.None, options, userId, now)];

        var events = await templates.CreateAsync(templateId, newEvents, cancellationToken);

        return new Result<TaskTemplate>.Success(TaskTemplate.Replay(events));
    }
}
