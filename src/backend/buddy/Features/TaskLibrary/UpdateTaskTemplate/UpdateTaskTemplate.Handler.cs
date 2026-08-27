using buddy.Common;
using buddy.Common.Validation;
using buddy.Features.Guardians;

using FluentValidation;

namespace buddy.Features.TaskLibrary;

public static class UpdateTaskTemplateHandler
{
    public static async Task<Result<TaskTemplate>> Handle(
        UpdateTaskTemplate command,
        IValidator<UpdateTaskTemplate> validator,
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

        var resolved = await TaskTemplateAccess.ResolveForManageAsync(command.TemplateId, userId, templates, guardians, cancellationToken);

        if (resolved is not Result<ResolvedTaskTemplate>.Success(var loaded))
        {
            return resolved.Reraise<ResolvedTaskTemplate, TaskTemplate>();
        }

        var before = new TaskTemplateDetails(loaded.Template.Name, loaded.Template.Icon, loaded.Template.Color);
        var after = new TaskTemplateDetails(command.Name, command.Icon, command.Color);

        if (before == after)
        {
            return new Result<TaskTemplate>.Success(loaded.Template);
        }

        var updated = new TaskTemplateDetailsUpdated(command.TemplateId, before, after, userId, DateTimeOffset.UtcNow);

        await templates.AppendAsync(command.TemplateId, [updated], cancellationToken);

        return new Result<TaskTemplate>.Success(TaskTemplate.Rehydrate([.. loaded.Events, updated])!);
    }
}
