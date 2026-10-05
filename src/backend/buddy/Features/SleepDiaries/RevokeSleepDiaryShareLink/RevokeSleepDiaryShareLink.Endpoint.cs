using System.Security.Claims;

using buddy.Common;
using buddy.Features.Users;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.SleepDiaries;

public static class RevokeSleepDiaryShareLinkEndpoint
{
    public static RouteGroupBuilder MapRevokeSleepDiaryShareLink(this RouteGroupBuilder sleepDiary)
    {
        sleepDiary.MapDelete("/children/{childId:guid}/share-links/{shareLinkId:guid}", async Task<Results<NoContent, NotFound>> (
            ClaimsPrincipal principal,
            Guid childId,
            Guid shareLinkId,
            IMessageBus bus,
            CancellationToken cancellationToken) =>
        {
            var command = RevokeSleepDiaryShareLink.FromClaims(principal, new UserId(childId), new SleepDiaryShareTokenId(shareLinkId));
            var result = await bus.InvokeAsync<Result<Unit>>(command, cancellationToken);

            return result switch
            {
                Result<Unit>.Success => TypedResults.NoContent(),
                Result<Unit>.NotFound => TypedResults.NotFound(),
                // No Forbidden tier and no validation rules -- both unreachable.
                Result<Unit>.Forbidden => TypedResults.NotFound(),
                Result<Unit>.Validation => TypedResults.NotFound(),
            };
        })
        .WithName("RevokeSleepDiaryShareLink");

        return sleepDiary;
    }
}
