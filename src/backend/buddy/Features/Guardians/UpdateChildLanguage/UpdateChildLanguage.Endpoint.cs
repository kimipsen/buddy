using System.Security.Claims;

using buddy.Common;
using buddy.Features.Users;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Guardians;

public static class UpdateChildLanguageEndpoint
{
    public static RouteGroupBuilder MapUpdateChildLanguage(this RouteGroupBuilder children)
    {
        children.MapPatch("/{childId:guid}/language", async Task<Results<Ok<ChildSummary>, BadRequest<ErrorEnvelope>, NotFound>> (
            ClaimsPrincipal principal,
            Guid childId,
            UpdateChildLanguageRequest request,
            IMessageBus bus,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var command = UpdateChildLanguage.FromClaims(principal, new UserId(childId), new Language(request.Language));
            var result = await bus.InvokeAsync<Result<ChildSummary>>(command, cancellationToken);

            return result switch
            {
                Result<ChildSummary>.Success(var summary) => TypedResults.Ok(summary),
                Result<ChildSummary>.NotFound => TypedResults.NotFound(),
                Result<ChildSummary>.Validation(var problem) => TypedResults.BadRequest(problem.ToEnvelope(httpContext)),
                // UpdateChildLanguageHandler never produces Forbidden -- collapsed to NotFound since
                // this route declares no other status for it.
                Result<ChildSummary>.Forbidden => TypedResults.NotFound(),
            };
        })
        .WithName("UpdateChildLanguage");

        return children;
    }
}

public sealed record UpdateChildLanguageRequest(string Language);
