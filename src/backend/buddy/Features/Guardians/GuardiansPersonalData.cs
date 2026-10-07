using buddy.Common.Erasure;
using buddy.Features.Users;

using Marten;

namespace buddy.Features.Guardians;

// Guardian links and invites (their streams live in the Users store). Erasing a person revokes
// their links -- the ids that remain are pseudonyms once the user stream is masked -- and revokes
// and masks every invite they sent, received, or that names an erased child: an invite holds the
// invitee's email and the child's first name. See gdpr-data-protection.md.
public sealed class GuardiansPersonalDataEraser(
    IUsersStore store,
    IGuardianLinkEventStore links,
    IGuardianInviteEventStore invites) : IPersonalDataEraser
{
    public Type Store => typeof(IUsersStore);

    public static void ConfigureMasking(StoreOptions options) =>
        options.Events.AddMaskingRuleForProtectedInformation<GuardianInviteCreated>(e => e with
        {
            ChildGivenName = Erased.Text,
            InvitedEmail = Erased.Text,
        });

    public async Task EraseGuardianAsync(ErasureSubject guardian, CancellationToken cancellationToken)
    {
        foreach (var link in await links.ListForGuardianAsync(guardian.UserId, cancellationToken))
        {
            await RevokeLinkAsync(link, cancellationToken);
        }

        var email = guardian.Email is null ? null : GuardianInviteDocument.NormalizeEmail(guardian.Email);
        await EraseInvitesAsync(
            guardian.UserId,
            d => d.InvitedBy == guardian.UserId.Value || (email != null && d.InvitedEmail == email),
            cancellationToken);
    }

    public async Task EraseChildAsync(ErasureSubject child, UserId? heir, CancellationToken cancellationToken)
    {
        foreach (var link in await links.ListForChildAsync(child.UserId, cancellationToken))
        {
            await RevokeLinkAsync(link, cancellationToken);
        }

        await EraseInvitesAsync(child.UserId, d => d.ChildId == child.UserId.Value, cancellationToken);
    }

    private Task RevokeLinkAsync(GuardianLinkDocument link, CancellationToken cancellationToken)
    {
        var linkId = new GuardianLinkId(link.GuardianLinkId);
        return links.AppendAsync(linkId, [new GuardianRevoked(linkId, DateTimeOffset.UtcNow)], cancellationToken);
    }

    private async Task EraseInvitesAsync(
        UserId erasedBy,
        System.Linq.Expressions.Expression<Func<GuardianInviteDocument, bool>> belongsToSubject,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<GuardianInviteDocument> documents;

        await using (var session = store.QuerySession())
        {
            documents = await session.Query<GuardianInviteDocument>().Where(belongsToSubject).ToListAsync(cancellationToken);
        }

        foreach (var document in documents.Where(d => d.InvitedEmail != Erased.Text))
        {
            var inviteId = new GuardianInviteId(document.Id);

            if (document.Status == GuardianInviteStatus.Pending)
            {
                await invites.AppendAsync(inviteId, [new GuardianInviteRevoked(inviteId, erasedBy, DateTimeOffset.UtcNow)], cancellationToken);
            }

            await store.MaskStreamAsync(document.Id, cancellationToken);

            await using var session = store.LightweightSession();
            if (await session.LoadAsync<GuardianInviteDocument>(document.Id, cancellationToken) is { } current)
            {
                session.Store(current with { ChildGivenName = Erased.Text, InvitedEmail = Erased.Text });
                await session.SaveChangesAsync(cancellationToken);
            }
        }
    }
}
