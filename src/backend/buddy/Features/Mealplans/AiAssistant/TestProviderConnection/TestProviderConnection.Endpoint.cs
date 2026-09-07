using System.Security.Claims;

using buddy.Common;
using buddy.Features.Users;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Mealplans;

public static class TestProviderConnectionEndpoint
{
    public static RouteGroupBuilder MapTestProviderConnection(this RouteGroupBuilder mealplans)
    {
        mealplans.MapPost("/children/{childId:guid}/ai/providers/{provider}/test-connection", async Task<Results<Ok<TestProviderConnectionResult>, NotFound, ForbidHttpResult, BadRequest<ErrorEnvelope>>> (
            ClaimsPrincipal principal,
            Guid childId,
            AiProvider provider,
            TestProviderConnectionRequest request,
            IMessageBus bus,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var command = TestProviderConnection.FromClaims(principal, new UserId(childId), provider, request.ApiKey);
            var result = await bus.InvokeAsync<Result<TestProviderConnectionResult>>(command, cancellationToken);

            return result switch
            {
                Result<TestProviderConnectionResult>.Success(var connectionResult) => TypedResults.Ok(connectionResult),
                Result<TestProviderConnectionResult>.Forbidden => TypedResults.Forbid(),
                Result<TestProviderConnectionResult>.Validation(var problem) => TypedResults.BadRequest(problem.ToEnvelope(httpContext)),
                Result<TestProviderConnectionResult>.NotFound => TypedResults.NotFound(),
            };
        })
        .WithName("TestProviderConnection");

        return mealplans;
    }
}

public sealed record TestProviderConnectionRequest(string? ApiKey);
