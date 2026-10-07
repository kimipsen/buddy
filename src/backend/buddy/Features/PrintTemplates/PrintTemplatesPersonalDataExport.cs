using buddy.Common.Erasure;

namespace buddy.Features.PrintTemplates;

// The "printTemplates" section: the print templates the caller owns personally. Group-owned
// templates belong to the group.
public sealed class PrintTemplatesPersonalDataExporter(IPrintTemplateEventStore templates) : IPersonalDataExporter
{
    public Type Store => typeof(IPrintTemplatesStore);

    public string Section => "printTemplates";

    public async Task<object?> ExportAsync(ExportSubject subject, CancellationToken cancellationToken)
    {
        List<PrintTemplateResponse> exported = [];

        foreach (var index in await templates.ListAsync(subject.UserId, [], cancellationToken))
        {
            if (index.OwnerUserId == subject.UserId.Value
                && await templates.FindSnapshotAsync(new PrintTemplateId(index.Id), cancellationToken) is { } template)
            {
                exported.Add(PrintTemplateResponse.From(template));
            }
        }

        return exported;
    }
}
