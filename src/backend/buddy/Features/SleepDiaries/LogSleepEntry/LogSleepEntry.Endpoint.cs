using System.Security.Claims;

using buddy.Common;
using buddy.Features.Users;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.SleepDiaries;

public static class LogSleepEntryEndpoint
{
    public static RouteGroupBuilder MapLogSleepEntry(this RouteGroupBuilder sleepDiary)
    {
        sleepDiary.MapPut("/children/{childId:guid}/entries/{date}", async Task<Results<Ok<SleepEntryResponse>, NotFound, BadRequest<ErrorEnvelope>>> (
            ClaimsPrincipal principal,
            Guid childId,
            DateOnly date,
            LogSleepEntryRequest request,
            IMessageBus bus,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var command = LogSleepEntry.FromClaims(principal, new UserId(childId), date, request);
            var result = await bus.InvokeAsync<Result<SleepEntryResponse>>(command, cancellationToken);

            return result switch
            {
                Result<SleepEntryResponse>.Success(var entry) => TypedResults.Ok(entry),
                Result<SleepEntryResponse>.Validation(var problem) => TypedResults.BadRequest(problem.ToEnvelope(httpContext)),
                Result<SleepEntryResponse>.NotFound => TypedResults.NotFound(),
                // SleepDiaryAuthorization has no Forbidden outcome (guardian-only, no lesser tier),
                // so this is unreachable; collapsed to NotFound rather than widening Results<...>.
                Result<SleepEntryResponse>.Forbidden => TypedResults.NotFound(),
            };
        })
        .WithName("LogSleepEntry");

        return sleepDiary;
    }
}

public sealed record LogSleepEntryRequest(
    TimeOnly? RoutineStartTime = null,
    TimeOnly? RitualStartTime = null,
    TimeOnly? RitualEndTime = null,
    TimeOnly? BedTime = null,
    TimeOnly? FellAsleepTime = null,
    IReadOnlyList<SleepIntervalDto>? NightWakeUps = null,
    TimeOnly? MorningWakeTime = null,
    bool IsTired = false,
    IReadOnlyList<SleepIntervalDto>? Naps = null,
    int? TotalSleepMinutes = null,
    string? Remarks = null);
