using System.Security.Claims;

using buddy.Common;
using buddy.Features.Users;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.VisitSummaries;

public static class UpdateVisitSummaryEndpoint
{
    public static RouteGroupBuilder MapUpdateVisitSummary(this RouteGroupBuilder visitSummaries)
    {
        visitSummaries.MapPut("/children/{childId:guid}/{summaryId:guid}", async Task<Results<Ok<VisitSummaryResponse>, NotFound, BadRequest<ErrorEnvelope>>> (
            ClaimsPrincipal principal,
            Guid childId,
            Guid summaryId,
            VisitSummaryRequest request,
            IMessageBus bus,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var command = UpdateVisitSummary.FromClaims(principal, new UserId(childId), new VisitSummaryId(summaryId), request);
            var result = await bus.InvokeAsync<Result<VisitSummaryResponse>>(command, cancellationToken);

            return result switch
            {
                Result<VisitSummaryResponse>.Success(var summary) => TypedResults.Ok(summary),
                Result<VisitSummaryResponse>.Validation(var problem) => TypedResults.BadRequest(problem.ToEnvelope(httpContext)),
                Result<VisitSummaryResponse>.NotFound => TypedResults.NotFound(),
                // Guardian-only feature: no Forbidden outcome exists.
                Result<VisitSummaryResponse>.Forbidden => TypedResults.NotFound(),
            };
        })
        .WithName("UpdateVisitSummary");

        return visitSummaries;
    }
}
