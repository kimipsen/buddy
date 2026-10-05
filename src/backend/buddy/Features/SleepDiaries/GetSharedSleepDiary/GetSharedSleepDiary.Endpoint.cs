using buddy.Common;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.SleepDiaries;

public static class GetSharedSleepDiaryEndpoint
{
    public static RouteGroupBuilder MapGetSharedSleepDiary(this RouteGroupBuilder sleepDiary)
    {
        sleepDiary.MapGet("/shared/{token}", async Task<Results<Ok<SharedSleepDiaryResponse>, NotFound, BadRequest<ErrorEnvelope>>> (
            string token,
            DateOnly? from,
            DateOnly? to,
            IMessageBus bus,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var query = GetSharedSleepDiary.FromRequest(token, from, to);
            var result = await bus.InvokeAsync<Result<SharedSleepDiary>>(query, cancellationToken);

            return result switch
            {
                Result<SharedSleepDiary>.Success(var shared) => TypedResults.Ok(SharedSleepDiaryResponse.FromShared(shared)),
                Result<SharedSleepDiary>.Validation(var problem) => TypedResults.BadRequest(problem.ToEnvelope(httpContext)),
                Result<SharedSleepDiary>.NotFound => TypedResults.NotFound(),
                // No access tiers on a token-gated read -- unreachable.
                Result<SharedSleepDiary>.Forbidden => TypedResults.NotFound(),
            };
        })
        .AllowAnonymous()
        .WithName("GetSharedSleepDiary");

        return sleepDiary;
    }
}

// Built for an external reader: the child's name to identify the record, the range shown and the
// link's expiry -- no internal ids beyond each entry's LoggedBy.
public sealed record SharedSleepDiaryResponse(
    string ChildGivenName,
    string ChildFamilyName,
    DateOnly From,
    DateOnly To,
    DateTimeOffset? ExpiresAt,
    string SleepHygieneNotes,
    IReadOnlyList<SleepEntryResponse> Entries)
{
    public static SharedSleepDiaryResponse FromShared(SharedSleepDiary shared) => new(
        shared.ChildName.GivenName,
        shared.ChildName.FamilyName,
        shared.From,
        shared.To,
        shared.ExpiresAt,
        shared.Diary.SleepHygieneNotes,
        [.. shared.Diary.Entries.Select(SleepEntryResponse.From)]);
}
