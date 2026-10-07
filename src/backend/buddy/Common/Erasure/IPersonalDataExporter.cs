using buddy.Features.Users;

namespace buddy.Common.Erasure;

// Whose data an export covers: the caller, plus the children they are an active guardian of (a
// guardian exercises a child's rights under parental responsibility). Email is the caller's
// normalized address, so invites sent to them can be found; null for a child.
public sealed record ExportSubject(UserId UserId, string? Email, IReadOnlyCollection<UserId> Children);

// One per feature: the section of GET /users/me/export that holds what the feature stores about the
// subject (see docs/backend/analysis/gdpr-data-protection.md, Question 5). Data, not events: the
// current state through the feature's response DTOs, never token hashes or encrypted keys.
// PersonalDataExporterCoverageTests checks that every store has one.
public interface IPersonalDataExporter
{
    // The feature's Marten store.
    Type Store { get; }

    // The JSON property the section appears under.
    string Section { get; }

    Task<object?> ExportAsync(ExportSubject subject, CancellationToken cancellationToken);
}
