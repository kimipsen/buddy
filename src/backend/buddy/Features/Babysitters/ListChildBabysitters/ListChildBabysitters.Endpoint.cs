using System.Security.Claims;

using buddy.Common;
using buddy.Features.Users;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Babysitters;

public static class ListChildBabysittersEndpoint
{
    public static RouteGroupBuilder MapListChildBabysitters(this RouteGroupBuilder babysitters)
    {
        babysitters.MapGet("/children/{childId:guid}", async Task<Results<Ok<IReadOnlyCollection<ChildBabysitter>>, NotFound>> (
            ClaimsPrincipal principal,
            Guid childId,
            IMessageBus bus,
            CancellationToken cancellationToken) =>
        {
            var query = ListChildBabysitters.FromClaims(principal, new UserId(childId));
            var result = await bus.InvokeAsync<Result<IReadOnlyCollection<ChildBabysitter>>>(query, cancellationToken);

            return result switch
            {
                Result<IReadOnlyCollection<ChildBabysitter>>.Success(var list) => TypedResults.Ok(list),
                Result<IReadOnlyCollection<ChildBabysitter>>.NotFound => TypedResults.NotFound(),
                // CheckPick never returns Forbidden and there's no input to validate -- neither is in
                // this route's declared results, so both collapse to NotFound.
                Result<IReadOnlyCollection<ChildBabysitter>>.Forbidden => TypedResults.NotFound(),
                Result<IReadOnlyCollection<ChildBabysitter>>.Validation => TypedResults.NotFound(),
            };
        })
        .WithName("ListChildBabysitters");

        return babysitters;
    }
}
