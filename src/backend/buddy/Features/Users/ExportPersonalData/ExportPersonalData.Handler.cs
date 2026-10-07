using buddy.Features.Privacy;

namespace buddy.Features.Users;

public static class ExportPersonalDataHandler
{
    public static Task<PersonalDataExportDocument> Handle(ExportPersonalData query, PersonalDataExport export, CancellationToken cancellationToken) =>
        export.ExportAsync(query.UserId, cancellationToken);
}
