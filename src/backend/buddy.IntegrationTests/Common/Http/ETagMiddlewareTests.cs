using System.Globalization;

using Alba;

using buddy.Features.Groups;
using buddy.Features.Mealplans;
using buddy.IntegrationTests.Features.Groups;
using buddy.IntegrationTests.Fixtures;

using Xunit;

namespace buddy.IntegrationTests.Common.Http;

// Drives ETagMiddleware through a real opted-in endpoint (GetGroup, in the /groups route group),
// so the tests cover the group-level .WithETag() wiring as well as the middleware itself.
[Collection(BuddyApiCollection.Name)]
public sealed class ETagMiddlewareTests(BuddyApiFixture fixture)
{
    [Fact]
    public async Task A_successful_get_carries_a_strong_etag_and_revalidate_cache_control()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var groupId = await GroupTestHelpers.CreateGroupAsync(fixture, token, "Team");

        var response = await GetGroupAsync(token, groupId, ifNoneMatch: null, expectedStatus: 200);

        var etag = response.Context.Response.Headers.ETag.ToString();
        Assert.Matches("^\"[A-Za-z0-9_-]{43}\"$", etag);
        Assert.Equal("private, no-cache", response.Context.Response.Headers.CacheControl.ToString());
    }

    [Fact]
    public async Task Sending_the_etag_back_returns_304_with_no_body()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var groupId = await GroupTestHelpers.CreateGroupAsync(fixture, token, "Team");
        var etag = await FetchETagAsync(token, groupId);

        var response = await GetGroupAsync(token, groupId, ifNoneMatch: etag, expectedStatus: 304);

        Assert.Equal(string.Empty, response.ReadAsText());
        Assert.Equal(etag, response.Context.Response.Headers.ETag.ToString());
        Assert.Equal("private, no-cache", response.Context.Response.Headers.CacheControl.ToString());
    }

    [Theory]
    [InlineData("W/{0}")]
    [InlineData("\"stale\", {0}")]
    [InlineData("*")]
    public async Task If_none_match_uses_weak_comparison_over_a_list(string headerFormat)
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var groupId = await GroupTestHelpers.CreateGroupAsync(fixture, token, "Team");
        var etag = await FetchETagAsync(token, groupId);

        await GetGroupAsync(token, groupId, ifNoneMatch: string.Format(CultureInfo.InvariantCulture, headerFormat, etag), expectedStatus: 304);
    }

    [Fact]
    public async Task A_stale_etag_returns_the_full_body()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var groupId = await GroupTestHelpers.CreateGroupAsync(fixture, token, "Team");

        var response = await GetGroupAsync(token, groupId, ifNoneMatch: "\"stale\"", expectedStatus: 200);

        Assert.Contains("Team", response.ReadAsText());
    }

    [Fact]
    public async Task A_write_between_two_gets_changes_the_etag()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var groupId = await GroupTestHelpers.CreateGroupAsync(fixture, token, "Team");
        var before = await FetchETagAsync(token, groupId);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Put.Json(new
            {
                Policy = new Dictionary<GroupRole, MealplanAccessTier>
                {
                    [GroupRole.Owner] = MealplanAccessTier.Manage,
                    [GroupRole.Admin] = MealplanAccessTier.Manage,
                    [GroupRole.Member] = MealplanAccessTier.Manage,
                },
            }).ToUrl($"/groups/{groupId}/mealplan-permission-policy");
            _.StatusCodeShouldBe(204);
        });

        var response = await GetGroupAsync(token, groupId, ifNoneMatch: before, expectedStatus: 200);

        Assert.NotEqual(before, response.Context.Response.Headers.ETag.ToString());
    }

    [Fact]
    public async Task A_not_found_response_has_no_etag()
    {
        var (_, ownerToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var (_, outsiderToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var groupId = await GroupTestHelpers.CreateGroupAsync(fixture, ownerToken, "Team");

        var response = await GetGroupAsync(outsiderToken, groupId, ifNoneMatch: "*", expectedStatus: 404);

        Assert.False(response.Context.Response.Headers.ContainsKey("ETag"));
    }

    [Fact]
    public async Task A_post_in_an_opted_in_group_has_no_etag()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Post.Json(new { Name = "Team" }).ToUrl("/groups/");
            _.StatusCodeShouldBeOk();
        });

        Assert.False(response.Context.Response.Headers.ContainsKey("ETag"));
    }

    private async Task<string> FetchETagAsync(string token, Guid groupId)
    {
        var response = await GetGroupAsync(token, groupId, ifNoneMatch: null, expectedStatus: 200);
        return response.Context.Response.Headers.ETag.ToString();
    }

    private Task<IScenarioResult> GetGroupAsync(string token, Guid groupId, string? ifNoneMatch, int expectedStatus) =>
        fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            if (ifNoneMatch is not null)
            {
                _.WithRequestHeader("If-None-Match", ifNoneMatch);
            }

            _.Get.Url($"/groups/{groupId}");
            _.StatusCodeShouldBe(expectedStatus);
        });
}
