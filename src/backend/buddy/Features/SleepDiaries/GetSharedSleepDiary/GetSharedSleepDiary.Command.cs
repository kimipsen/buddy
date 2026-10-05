namespace buddy.Features.SleepDiaries;

// Unauthenticated by design -- a clinician has no Buddy account. The token in the URL is the
// authentication, exactly like GetIcalFeed.
public sealed record GetSharedSleepDiary(string Token, DateOnly From, DateOnly To)
{
    // The paper form this mirrors covers 14 days; that's the default window, ending today.
    public const int DefaultDays = 14;

    public static GetSharedSleepDiary FromRequest(string token, DateOnly? from, DateOnly? to)
    {
        var end = to ?? from?.AddDays(DefaultDays - 1) ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var start = from ?? end.AddDays(-(DefaultDays - 1));

        return new GetSharedSleepDiary(token, start, end);
    }
}
