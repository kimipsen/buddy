using System.Security.Claims;

using buddy.Common;
using buddy.Features.Users;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.VisitSummaries;

public static class CreateVisitSummaryEndpoint
{
    public static RouteGroupBuilder MapCreateVisitSummary(this RouteGroupBuilder visitSummaries)
    {
        visitSummaries.MapPost("/children/{childId:guid}", async Task<Results<Ok<VisitSummaryResponse>, NotFound, BadRequest<ErrorEnvelope>>> (
            ClaimsPrincipal principal,
            Guid childId,
            VisitSummaryRequest request,
            IMessageBus bus,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var command = CreateVisitSummary.FromClaims(principal, new UserId(childId), request);
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
        .WithName("CreateVisitSummary");

        return visitSummaries;
    }
}
