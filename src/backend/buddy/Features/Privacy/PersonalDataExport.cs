using buddy.Common.Erasure;
using buddy.Features.Guardians;
using buddy.Features.Users;

namespace buddy.Features.Privacy;

// GET /users/me/export: one JSON document with what Buddy holds about the caller, one section per
// feature (each feature's IPersonalDataExporter). A guardian's export covers the children they are
// an active guardian of; a child's covers only its own account. See
// docs/backend/analysis/gdpr-data-protection.md, Question 5.
public sealed class PersonalDataExport(
    IUserEventStore users,
    IGuardianLinkEventStore guardians,
    IEnumerable<IPersonalDataExporter> exporters,
    ILogger<PersonalDataExport> logger)
{
    public async Task<PersonalDataExportDocument> ExportAsync(UserId userId, CancellationToken cancellationToken)
    {
        var user = await users.FindSnapshotAsync(userId, cancellationToken)
            ?? throw new InvalidOperationException($"User {userId.Value} has no snapshot; ProvisionedUserMiddleware should have refused the request.");

        var isChild = await guardians.IsChildAsync(userId, cancellationToken);
        var subject = isChild
            ? new ExportSubject(userId, Email: null, Children: [])
            : new ExportSubject(
                userId,
                UserErasure.EmailOf(user),
                [.. (await guardians.ListForGuardianAsync(userId, cancellationToken)).Select(l => new UserId(l.ChildId)).Distinct()]);

        var sections = new Dictionary<string, object?>();

        foreach (var exporter in exporters.Where(e => !isChild || e is UsersPersonalDataExporter))
        {
            sections[exporter.Section] = await exporter.ExportAsync(subject, cancellationToken);
        }

        logger.PersonalDataExported(userId.Value, subject.Children.Count);

        return new PersonalDataExportDocument(DateTimeOffset.UtcNow, userId.Value, sections);
    }
}

public sealed record PersonalDataExportDocument(DateTimeOffset ExportedAt, Guid UserId, IReadOnlyDictionary<string, object?> Sections);
