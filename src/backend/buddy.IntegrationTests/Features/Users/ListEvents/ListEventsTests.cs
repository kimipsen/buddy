using Alba;

using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

namespace buddy.IntegrationTests.Features.Users.ListEvents;

[Collection(BuddyApiCollection.Name)]
public sealed class ListEventsTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("GetCurrentUserEvents")]
    public async Task Returns_the_users_own_event_stream_in_order()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Patch.Json(new { GivenName = "Renamed", FamilyName = "Person" }).ToUrl("/users/me/name");
            _.StatusCodeShouldBeOk();
        });

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Get.Url("/users/me/events");
            _.StatusCodeShouldBeOk();
        });

        var body = response.ReadAsJson<EventsPageResponse>();

        Assert.Equal(2, body.Items.Count);
        Assert.Equal("UserCreated", body.Items[0].Type);
        Assert.Equal("NameUpdated", body.Items[1].Type);
        Assert.Null(body.PreviousCursor);
        Assert.Null(body.NextCursor);
    }

    [Fact]
    public async Task Rejects_an_invalid_cursor()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Get.Url("/users/me/events?cursor=not-a-real-cursor");
            _.StatusCodeShouldBe(400);
        });
    }

    private sealed record EventsPageResponse(List<EventItem> Items, string? PreviousCursor, string? NextCursor);

    private sealed record EventItem(string Type, object Data);
}
