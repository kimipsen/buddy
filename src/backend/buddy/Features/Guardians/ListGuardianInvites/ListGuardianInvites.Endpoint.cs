using System.Security.Claims;

using buddy.Common;
using buddy.Features.Users;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Guardians;

public static class ListGuardianInvitesEndpoint
{
    public static RouteGroupBuilder MapListGuardianInvites(this RouteGroupBuilder children)
    {
        children.MapGet("/{childId:guid}/guardian-invites", async Task<Results<Ok<IReadOnlyCollection<GuardianInviteResponse>>, NotFound>> (
            ClaimsPrincipal principal,
            Guid childId,
            IMessageBus bus,
            CancellationToken cancellationToken) =>
        {
            var query = ListGuardianInvites.FromClaims(principal, new UserId(childId));
            var result = await bus.InvokeAsync<Result<IReadOnlyCollection<GuardianInviteSummary>>>(query, cancellationToken);

            return result switch
            {
                Result<IReadOnlyCollection<GuardianInviteSummary>>.Success(var invites) =>
                    TypedResults.Ok<IReadOnlyCollection<GuardianInviteResponse>>([.. invites.Select(GuardianInviteResponse.FromSummary)]),
                Result<IReadOnlyCollection<GuardianInviteSummary>>.NotFound => TypedResults.NotFound(),
                // ListGuardianInvitesHandler never produces Forbidden/Validation -- collapsed to
                // NotFound since this route declares no other status for them.
                Result<IReadOnlyCollection<GuardianInviteSummary>>.Forbidden => TypedResults.NotFound(),
                Result<IReadOnlyCollection<GuardianInviteSummary>>.Validation => TypedResults.NotFound(),
            };
        })
        .WithName("ListGuardianInvites");

        return children;
    }
}
