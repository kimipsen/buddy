using System.Security.Claims;

using buddy.Common;
using buddy.Features.Users;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.SleepDiaries;

public static class ListSleepDiaryEntriesEndpoint
{
    public static RouteGroupBuilder MapListSleepDiaryEntries(this RouteGroupBuilder sleepDiary)
    {
        sleepDiary.MapGet("/children/{childId:guid}/entries", async Task<Results<Ok<SleepDiaryEntriesResponse>, NotFound, BadRequest<ErrorEnvelope>>> (
            ClaimsPrincipal principal,
            Guid childId,
            DateOnly from,
            DateOnly to,
            IMessageBus bus,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var query = ListSleepDiaryEntries.FromClaims(principal, new UserId(childId), from, to);
            var result = await bus.InvokeAsync<Result<SleepDiaryRange>>(query, cancellationToken);

            return result switch
            {
                Result<SleepDiaryRange>.Success(var range) => TypedResults.Ok(SleepDiaryEntriesResponse.From(range)),
                Result<SleepDiaryRange>.Validation(var problem) => TypedResults.BadRequest(problem.ToEnvelope(httpContext)),
                Result<SleepDiaryRange>.NotFound => TypedResults.NotFound(),
                // Guardian-only feature: no Forbidden outcome exists.
                Result<SleepDiaryRange>.Forbidden => TypedResults.NotFound(),
            };
        })
        .WithName("ListSleepDiaryEntries");

        return sleepDiary;
    }
}

public sealed record SleepDiaryEntriesResponse(string SleepHygieneNotes, IReadOnlyList<SleepEntryResponse> Entries)
{
    public static SleepDiaryEntriesResponse From(SleepDiaryRange range) =>
        new(range.SleepHygieneNotes, [.. range.Entries.Select(SleepEntryResponse.From)]);
}
