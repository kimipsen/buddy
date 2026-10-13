using buddy.Common;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Feelings;

public static class GetSharedFeelingsEndpoint
{
    public static RouteGroupBuilder MapGetSharedFeelings(this RouteGroupBuilder feelings)
    {
        feelings.MapGet("/shared/{token}", async Task<Results<Ok<SharedFeelingsResponse>, NotFound, BadRequest<ErrorEnvelope>>> (
            string token,
            DateOnly? from,
            DateOnly? to,
            IMessageBus bus,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await bus.InvokeAsync<Result<SharedFeelings>>(new GetSharedFeelings(token, from, to), cancellationToken);

            return result switch
            {
                Result<SharedFeelings>.Success(var shared) => TypedResults.Ok(SharedFeelingsResponse.FromShared(shared)),
                Result<SharedFeelings>.Validation(var problem) => TypedResults.BadRequest(problem.ToEnvelope(httpContext)),
                Result<SharedFeelings>.NotFound => TypedResults.NotFound(),
                // No access tiers on a token-gated read -- unreachable.
                Result<SharedFeelings>.Forbidden => TypedResults.NotFound(),
            };
        })
        .AllowAnonymous()
        .WithName("GetSharedFeelings");

        return feelings;
    }
}

// Built for an external reader: the child's name to identify the record, the range shown and the
// link's expiry. Notes are included -- the share dialog says so.
public sealed record SharedFeelingsResponse(
    string ChildGivenName,
    string ChildFamilyName,
    DateOnly From,
    DateOnly To,
    DateTimeOffset? ExpiresAt,
    IReadOnlyList<FeelingCheckInResponse> CheckIns)
{
    public static SharedFeelingsResponse FromShared(SharedFeelings shared) => new(
        shared.ChildName.GivenName,
        shared.ChildName.FamilyName,
        shared.From,
        shared.To,
        shared.ExpiresAt,
        shared.CheckIns);
}
