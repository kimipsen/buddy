using System.Security.Claims;

using buddy.Common;
using buddy.Features.Users;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Feelings;

public static class CreateFeelingsShareLinkEndpoint
{
    public static RouteGroupBuilder MapCreateFeelingsShareLink(this RouteGroupBuilder feelings)
    {
        feelings.MapPost("/children/{childId:guid}/share-links", async Task<Results<Ok<FeelingsShareLinkResponse>, NotFound, ForbidHttpResult, BadRequest<ErrorEnvelope>>> (
            ClaimsPrincipal principal,
            Guid childId,
            CreateFeelingsShareLinkRequest request,
            IMessageBus bus,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var command = CreateFeelingsShareLink.FromClaims(principal, new UserId(childId), request.ExpiresAt);
            var result = await bus.InvokeAsync<Result<IssuedFeelingsShareLink>>(command, cancellationToken);

            return result switch
            {
                Result<IssuedFeelingsShareLink>.Success(var issued) => TypedResults.Ok(new FeelingsShareLinkResponse(
                    issued.Id.Value,
                    issued.Token,
                    issued.CreatedAt,
                    issued.ExpiresAt)),
                Result<IssuedFeelingsShareLink>.Validation(var problem) => TypedResults.BadRequest(problem.ToEnvelope(httpContext)),
                Result<IssuedFeelingsShareLink>.NotFound => TypedResults.NotFound(),
                Result<IssuedFeelingsShareLink>.Forbidden => TypedResults.Forbid(),
            };
        })
        .WithName("CreateFeelingsShareLink");

        return feelings;
    }
}

public sealed record CreateFeelingsShareLinkRequest(DateTimeOffset? ExpiresAt = null);

// Token is the plaintext secret -- returned exactly once, here, and never retrievable again. The
// frontend turns it into its public share page URL.
public sealed record FeelingsShareLinkResponse(Guid Id, string Token, DateTimeOffset CreatedAt, DateTimeOffset? ExpiresAt);
