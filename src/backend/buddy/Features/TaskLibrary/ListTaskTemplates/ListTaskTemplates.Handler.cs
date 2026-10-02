using buddy.Common;
using buddy.Features.Guardians;

namespace buddy.Features.TaskLibrary;

public static class ListTaskTemplatesHandler
{
    public static async Task<Result<IReadOnlyCollection<TaskTemplate>>> Handle(
        ListTaskTemplates query,
        ITaskTemplateEventStore templates,
        IGuardianLinkEventStore guardians,
        CancellationToken cancellationToken)
    {
        var userId = query.UserId;

        var access = await TaskLibraryAuthorization.CheckView(query.ChildId, userId, guardians, cancellationToken);

        if (access != TaskLibraryAccess.Allowed)
        {
            return access.ToDeniedResult<IReadOnlyCollection<TaskTemplate>>();
        }

        var templateIds = await templates.ListIdsForChildAsync(query.ChildId, cancellationToken);
        var loaded = new List<TaskTemplate>(templateIds.Count);

        foreach (var templateId in templateIds)
        {
            // Deliberately includes archived templates -- a guardian's library of a child's
            // templates, including retired ones, not just what's currently assignable. Same
            // contract as ListMealsHandler.LoadFamilyMealsAsync.
            if (await templates.FindSnapshotAsync(templateId, cancellationToken) is { } template)
            {
                loaded.Add(template);
            }
        }

        loaded.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));

        return new Result<IReadOnlyCollection<TaskTemplate>>.Success(loaded);
    }
}
