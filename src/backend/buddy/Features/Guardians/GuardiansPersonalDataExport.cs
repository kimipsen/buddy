using buddy.Common.Erasure;
using buddy.Features.Calendars;
using buddy.Features.Users;

using Marten;

namespace buddy.Features.Guardians;

// The "children" section: each child the caller guards with their profile, all their guardians and
// the guardian invites sent for them, plus the invites addressed to the caller. No invite token
// hashes.
public sealed class GuardiansPersonalDataExporter(
    IUsersStore store,
    IGuardianLinkEventStore links,
    IUserEventStore users) : IPersonalDataExporter
{
    public Type Store => typeof(IUsersStore);

    public string Section => "children";

    public async Task<object?> ExportAsync(ExportSubject subject, CancellationToken cancellationToken)
    {
        var email = subject.Email is null ? null : GuardianInviteDocument.NormalizeEmail(subject.Email);
        var childIds = subject.Children.Select(c => c.Value).ToArray();

        List<GuardianInviteDocument> invites;

        await using (var session = store.QuerySession())
        {
            invites = [.. await session.Query<GuardianInviteDocument>()
                .Where(d => childIds.Contains(d.ChildId) || (email != null && d.InvitedEmail == email))
                .OrderBy(d => d.CreatedAt)
                .ToListAsync(cancellationToken)];
        }

        List<ExportedChild> children = [];

        foreach (var childId in subject.Children)
        {
            if (await users.FindSnapshotAsync(childId, cancellationToken) is not { } child)
            {
                continue;
            }

            List<ExportedGuardian> guardians = [];

            foreach (var link in await links.ListForChildAsync(childId, cancellationToken))
            {
                var guardian = await users.FindSnapshotAsync(new UserId(link.GuardianId), cancellationToken);
                guardians.Add(new ExportedGuardian(link.GuardianId, guardian?.Name, link.Kind, link.CreatedAt));
            }

            children.Add(new ExportedChild(
                childId.Value,
                child.UserName,
                child.Name,
                child.TimeZoneId,
                child.Language,
                guardians,
                [.. invites.Where(i => i.ChildId == childId.Value).Select(ExportedGuardianInvite.From)]));
        }

        return new GuardiansExport(
            children,
            [.. invites.Where(i => email != null && i.InvitedEmail == email).Select(ExportedGuardianInvite.From)]);
    }
}

public sealed record GuardiansExport(IReadOnlyList<ExportedChild> Children, IReadOnlyList<ExportedGuardianInvite> InvitesReceived);

public sealed record ExportedChild(
    Guid Id,
    string UserName,
    Name Name,
    TimeZoneId TimeZoneId,
    Language Language,
    IReadOnlyList<ExportedGuardian> Guardians,
    IReadOnlyList<ExportedGuardianInvite> Invites);

public sealed record ExportedGuardian(Guid UserId, Name? Name, GuardianKind Kind, DateTimeOffset LinkedAt);

public sealed record ExportedGuardianInvite(
    Guid Id,
    Guid ChildId,
    string ChildGivenName,
    string InvitedEmail,
    GuardianKind Kind,
    Guid InvitedBy,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt,
    GuardianInviteStatus Status)
{
    public static ExportedGuardianInvite From(GuardianInviteDocument d) =>
        new(d.Id, d.ChildId, d.ChildGivenName, d.InvitedEmail, d.Kind, d.InvitedBy, d.CreatedAt, d.ExpiresAt, d.Status);
}
