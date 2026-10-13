using buddy.Common;
using buddy.Features.Guardians;
using buddy.Features.Users;

namespace buddy.Features.TaskLibrary;

// Result of a successful TaskTemplateAccess.ResolveForManageAsync call -- the already-loaded
// events and rehydrated aggregate, plus the owning child resolved along the way, so a caller can
// fold in its own new event without a second read.
internal sealed record ResolvedTaskTemplate(IReadOnlyCollection<TaskTemplateEvent> Events, TaskTemplate Template, UserId ChildId);

// Shared "load current state, resolve the owning child, authorize Manage" preamble every
// TaskLibrary write use-case past creation needs. AddSubtask/UpdateSubtask/RemoveSubtask/
// ReorderSubtasks/UpdateTaskTemplate/ArchiveTaskTemplate carry only a TemplateId, not a ChildId
// (unlike CreateTaskTemplate/ListTaskTemplates), so the owning child has to be reverse-resolved
// from the template's own index row (ITaskTemplateEventStore.FindChildIdForTemplateAsync) before
// TaskLibraryAuthorization.CheckManage can run against it. Callers re-raise a failed Result via
// ResultExtensions.Reraise, the same pattern its doc comment in Common/Result.cs describes this
// kind of shared prerequisite helper for.
internal static class TaskTemplateAccess
{
    public static Task<Result<ResolvedTaskTemplate>> ResolveForManageAsync(
        TaskTemplateId templateId, UserId userId, ITaskTemplateEventStore templates, IGuardianLinkEventStore guardians, CancellationToken cancellationToken) =>
        ResolveAsync(templateId, userId, templates, guardians, TaskLibraryAuthorization.CheckManage, cancellationToken);

    // The child-only suggestion slices: the template's owning child is allowed, a guardian of the
    // child Forbidden, anyone else NotFound (docs/backend/analysis/packing-checklists.md, Question 8).
    public static Task<Result<ResolvedTaskTemplate>> ResolveForOwnerAsync(
        TaskTemplateId templateId, UserId userId, ITaskTemplateEventStore templates, IGuardianLinkEventStore guardians, CancellationToken cancellationToken) =>
        ResolveAsync(templateId, userId, templates, guardians, TaskLibraryAuthorization.CheckOwner, cancellationToken);

    private static async Task<Result<ResolvedTaskTemplate>> ResolveAsync(
        TaskTemplateId templateId,
        UserId userId,
        ITaskTemplateEventStore templates,
        IGuardianLinkEventStore guardians,
        Func<UserId, UserId, IGuardianLinkEventStore, CancellationToken, Task<TaskLibraryAccess>> check,
        CancellationToken cancellationToken)
    {
        var events = await templates.ReadAsync(templateId, cancellationToken);
        var template = TaskTemplate.Rehydrate(events);

        if (template is null || template.IsArchived)
        {
            return new Result<ResolvedTaskTemplate>.NotFound();
        }

        if (await templates.FindChildIdForTemplateAsync(templateId, cancellationToken) is not { } childId)
        {
            return new Result<ResolvedTaskTemplate>.NotFound();
        }

        var access = await check(childId, userId, guardians, cancellationToken);

        if (access != TaskLibraryAccess.Allowed)
        {
            return access.ToDeniedResult<ResolvedTaskTemplate>();
        }

        return new Result<ResolvedTaskTemplate>.Success(new ResolvedTaskTemplate(events, template, childId));
    }
}
