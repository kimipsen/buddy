using buddy.IntegrationTests.Fixtures;

namespace buddy.IntegrationTests.Features.FirstThen;

// Response shapes matching FirstThenBoardResponse / GuardianFirstThenResponse (Features/FirstThen).
internal sealed record FirstThenCardDto(string Title, string Icon);

internal sealed record FirstThenBoardDto(
    int Revision,
    string State,
    FirstThenCardDto? First,
    FirstThenCardDto? Then,
    int? ThenMinutes,
    DateTimeOffset? UnlockedAt,
    DateTimeOffset? LastModifiedAt,
    string? LastModifiedByName);

internal sealed record GuardianFirstThenDto(FirstThenBoardDto Board, List<FirstThenCardDto> RecentFirsts, List<FirstThenCardDto> RecentThens);

internal static class FirstThenTestHelpers
{
    public static object Board(int revision, string first = "Homework", string then = "iPad", int? thenMinutes = 20, string firstIcon = "📚", string thenIcon = "📱") => new
    {
        Revision = revision,
        First = new { Title = first, Icon = firstIcon },
        Then = new { Title = then, Icon = thenIcon },
        ThenMinutes = thenMinutes,
    };

    public static async Task<FirstThenBoardDto?> SetAsync(BuddyApiFixture fixture, string token, Guid childId, object body, int expectedStatus = 200)
    {
        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Put.Json(body).ToUrl($"/first-then/children/{childId}");
            _.StatusCodeShouldBe(expectedStatus);
        });

        return expectedStatus == 200 ? response.ReadAsJson<FirstThenBoardDto>() : null;
    }

    public static async Task<GuardianFirstThenDto?> GetAsync(BuddyApiFixture fixture, string token, Guid childId, int expectedStatus = 200)
    {
        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Get.Url($"/first-then/children/{childId}");
            _.StatusCodeShouldBe(expectedStatus);
        });

        return expectedStatus == 200 ? response.ReadAsJson<GuardianFirstThenDto>() : null;
    }

    public static async Task<FirstThenBoardDto?> GetMineAsync(BuddyApiFixture fixture, string token, int expectedStatus = 200)
    {
        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Get.Url("/first-then/me");
            _.StatusCodeShouldBe(expectedStatus);
        });

        return expectedStatus == 200 ? response.ReadAsJson<FirstThenBoardDto>() : null;
    }

    // action: "first/complete", "first/reopen" or "end". childId null -> the caller's own /me route.
    public static async Task<FirstThenBoardDto?> ActAsync(BuddyApiFixture fixture, string token, Guid? childId, string action, int revision, int expectedStatus = 200)
    {
        var url = childId is { } id ? $"/first-then/children/{id}/{action}" : $"/first-then/me/{action}";
        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Post.Json(new { Revision = revision }).ToUrl(url);
            _.StatusCodeShouldBe(expectedStatus);
        });

        return expectedStatus == 200 ? response.ReadAsJson<FirstThenBoardDto>() : null;
    }

    public const string Complete = "first/complete";
    public const string Reopen = "first/reopen";
    public const string End = "end";
}
