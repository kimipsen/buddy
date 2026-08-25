using System.Security.Claims;

using buddy.Common;
using buddy.Features.Users;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Guardians;

public static class ListChildGuardiansEndpoint
{
    public static RouteGroupBuilder MapListChildGuardians(this RouteGroupBuilder children)
    {
        children.MapGet("/{childId:guid}/guardians", async Task<Results<Ok<IReadOnlyCollection<GuardianSummary>>, NotFound>> (
            ClaimsPrincipal principal,
            Guid childId,
            IMessageBus bus,
            CancellationToken cancellationToken) =>
        {
            var result = await bus.InvokeAsync<Result<IReadOnlyCollection<GuardianSummary>>>(
                ListChildGuardians.FromClaims(principal, new UserId(childId)), cancellationToken);

            return result switch
            {
                Result<IReadOnlyCollection<GuardianSummary>>.Success(var summaries) => TypedResults.Ok(summaries),
                Result<IReadOnlyCollection<GuardianSummary>>.NotFound => TypedResults.NotFound(),
                // The handler never produces these -- there's no ForbidHttpResult/BadRequest in
                // this route's declared results, so both collapse to NotFound like other routes
                // in this codebase do.
                Result<IReadOnlyCollection<GuardianSummary>>.Forbidden => TypedResults.NotFound(),
                Result<IReadOnlyCollection<GuardianSummary>>.Validation => TypedResults.NotFound(),
            };
        })
        .WithName("ListChildGuardians");

        return children;
    }
}
