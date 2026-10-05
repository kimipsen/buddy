using System.Security.Claims;

using buddy.Common;
using buddy.Features.Users;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.SleepDiaries;

public static class ListSleepDiaryShareLinksEndpoint
{
    public static RouteGroupBuilder MapListSleepDiaryShareLinks(this RouteGroupBuilder sleepDiary)
    {
        sleepDiary.MapGet("/children/{childId:guid}/share-links", async Task<Results<Ok<IReadOnlyCollection<SleepDiaryShareLinkSummary>>, NotFound>> (
            ClaimsPrincipal principal,
            Guid childId,
            IMessageBus bus,
            CancellationToken cancellationToken) =>
        {
            var query = ListSleepDiaryShareLinks.FromClaims(principal, new UserId(childId));
            var result = await bus.InvokeAsync<Result<IReadOnlyCollection<SleepDiaryShareLinkSummary>>>(query, cancellationToken);

            return result switch
            {
                Result<IReadOnlyCollection<SleepDiaryShareLinkSummary>>.Success(var links) => TypedResults.Ok(links),
                Result<IReadOnlyCollection<SleepDiaryShareLinkSummary>>.NotFound => TypedResults.NotFound(),
                // No Forbidden tier and no validation rules -- both unreachable.
                Result<IReadOnlyCollection<SleepDiaryShareLinkSummary>>.Forbidden => TypedResults.NotFound(),
                Result<IReadOnlyCollection<SleepDiaryShareLinkSummary>>.Validation => TypedResults.NotFound(),
            };
        })
        .WithName("ListSleepDiaryShareLinks");

        return sleepDiary;
    }
}
