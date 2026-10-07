using buddy.Common.Erasure;

namespace buddy.Features.TaskLibrary;

// The "taskLibrary" section: each child's task templates, archived ones included.
public sealed class TaskLibraryPersonalDataExporter(ITaskTemplateEventStore templates) : IPersonalDataExporter
{
    public Type Store => typeof(ITaskLibraryStore);

    public string Section => "taskLibrary";

    public async Task<object?> ExportAsync(ExportSubject subject, CancellationToken cancellationToken)
    {
        List<ExportedTaskTemplates> children = [];

        foreach (var childId in subject.Children)
        {
            List<TaskTemplateResponse> exported = [];

            foreach (var id in await templates.ListIdsForChildAsync(childId, cancellationToken))
            {
                if (await templates.FindSnapshotAsync(id, cancellationToken) is { } template)
                {
                    exported.Add(TaskTemplateResponse.FromTaskTemplate(template));
                }
            }

            children.Add(new ExportedTaskTemplates(childId.Value, exported));
        }

        return children;
    }
}

public sealed record ExportedTaskTemplates(Guid ChildId, IReadOnlyList<TaskTemplateResponse> Templates);
