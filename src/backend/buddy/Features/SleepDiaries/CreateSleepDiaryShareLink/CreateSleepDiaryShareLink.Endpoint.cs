using System.Security.Claims;

using buddy.Common;
using buddy.Features.Users;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.SleepDiaries;

public static class CreateSleepDiaryShareLinkEndpoint
{
    public static RouteGroupBuilder MapCreateSleepDiaryShareLink(this RouteGroupBuilder sleepDiary)
    {
        sleepDiary.MapPost("/children/{childId:guid}/share-links", async Task<Results<Ok<SleepDiaryShareLinkResponse>, NotFound, BadRequest<ErrorEnvelope>>> (
            ClaimsPrincipal principal,
            Guid childId,
            CreateSleepDiaryShareLinkRequest request,
            IMessageBus bus,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var command = CreateSleepDiaryShareLink.FromClaims(principal, new UserId(childId), request.ExpiresAt);
            var result = await bus.InvokeAsync<Result<IssuedSleepDiaryShareLink>>(command, cancellationToken);

            return result switch
            {
                Result<IssuedSleepDiaryShareLink>.Success(var issued) => TypedResults.Ok(new SleepDiaryShareLinkResponse(
                    issued.Id.Value,
                    issued.Token,
                    issued.CreatedAt,
                    issued.ExpiresAt)),
                Result<IssuedSleepDiaryShareLink>.Validation(var problem) => TypedResults.BadRequest(problem.ToEnvelope(httpContext)),
                Result<IssuedSleepDiaryShareLink>.NotFound => TypedResults.NotFound(),
                // Guardian-only feature: no Forbidden outcome exists.
                Result<IssuedSleepDiaryShareLink>.Forbidden => TypedResults.NotFound(),
            };
        })
        .WithName("CreateSleepDiaryShareLink");

        return sleepDiary;
    }
}

public sealed record CreateSleepDiaryShareLinkRequest(DateTimeOffset? ExpiresAt = null);

// Token is the plaintext secret -- returned exactly once, here, and never retrievable again. The
// frontend turns it into its public share page URL.
public sealed record SleepDiaryShareLinkResponse(Guid Id, string Token, DateTimeOffset CreatedAt, DateTimeOffset? ExpiresAt);
