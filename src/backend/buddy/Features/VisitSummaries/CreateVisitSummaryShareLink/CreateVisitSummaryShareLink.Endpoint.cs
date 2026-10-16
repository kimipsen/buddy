using System.Security.Claims;

using buddy.Common;
using buddy.Features.Users;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.VisitSummaries;

public static class CreateVisitSummaryShareLinkEndpoint
{
    public static RouteGroupBuilder MapCreateVisitSummaryShareLink(this RouteGroupBuilder visitSummaries)
    {
        visitSummaries.MapPost("/children/{childId:guid}/{summaryId:guid}/share-links", async Task<Results<Ok<VisitSummaryShareLinkResponse>, NotFound, BadRequest<ErrorEnvelope>>> (
            ClaimsPrincipal principal,
            Guid childId,
            Guid summaryId,
            CreateVisitSummaryShareLinkRequest request,
            IMessageBus bus,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var command = CreateVisitSummaryShareLink.FromClaims(principal, new UserId(childId), new VisitSummaryId(summaryId), request.ExpiresAt);
            var result = await bus.InvokeAsync<Result<IssuedVisitSummaryShareLink>>(command, cancellationToken);

            return result switch
            {
                Result<IssuedVisitSummaryShareLink>.Success(var issued) => TypedResults.Ok(new VisitSummaryShareLinkResponse(
                    issued.Id.Value,
                    issued.Token,
                    issued.CreatedAt,
                    issued.ExpiresAt)),
                Result<IssuedVisitSummaryShareLink>.Validation(var problem) => TypedResults.BadRequest(problem.ToEnvelope(httpContext)),
                Result<IssuedVisitSummaryShareLink>.NotFound => TypedResults.NotFound(),
                // Guardian-only feature: no Forbidden outcome exists.
                Result<IssuedVisitSummaryShareLink>.Forbidden => TypedResults.NotFound(),
            };
        })
        .WithName("CreateVisitSummaryShareLink");

        return visitSummaries;
    }
}

public sealed record CreateVisitSummaryShareLinkRequest(DateTimeOffset? ExpiresAt = null);

// Token is the plaintext secret -- returned exactly once, here. The frontend turns it into its
// public page URL.
public sealed record VisitSummaryShareLinkResponse(Guid Id, string Token, DateTimeOffset CreatedAt, DateTimeOffset? ExpiresAt);
