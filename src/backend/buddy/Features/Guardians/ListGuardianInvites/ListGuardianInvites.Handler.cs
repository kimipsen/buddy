using buddy.Common;

namespace buddy.Features.Guardians;

public static class ListGuardianInvitesHandler
{
    public static async Task<Result<IReadOnlyCollection<GuardianInviteSummary>>> Handle(
        ListGuardianInvites query, IGuardianLinkEventStore guardianLinks, IGuardianInviteEventStore invites, CancellationToken cancellationToken)
    {
        var userId = query.UserId;

        var link = await guardianLinks.FindActiveLinkAsync(query.ChildId, userId, cancellationToken);

        if (link is null)
        {
            return new Result<IReadOnlyCollection<GuardianInviteSummary>>.NotFound();
        }

        var documents = await invites.ListPendingInvitesAsync(query.ChildId, cancellationToken);

        return new Result<IReadOnlyCollection<GuardianInviteSummary>>.Success([.. documents.Select(GuardianInviteSummary.FromDocument)]);
    }
}
