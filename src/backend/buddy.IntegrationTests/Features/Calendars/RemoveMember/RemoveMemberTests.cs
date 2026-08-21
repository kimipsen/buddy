using Alba;

using buddy.Features.Calendars;
using buddy.IntegrationTests.Features.Calendars;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

namespace buddy.IntegrationTests.Features.Calendars.RemoveMember;

[Collection(BuddyApiCollection.Name)]
public sealed class RemoveMemberTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("RemoveCalendarMember")]
    public async Task The_owner_can_remove_an_existing_member()
    {
        var (_, ownerToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var calendarId = await CalendarTestHelpers.CreateCalendarAsync(fixture, ownerToken, "Shared");
        var (_, _, memberId) = await fixture.CreateAuthenticatedUserAsync();

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {ownerToken}");
            _.Put.Json(new { Role = CalendarRole.Viewer }).ToUrl($"/calendars/{calendarId}/members/{memberId}");
            _.StatusCodeShouldBe(204);
        });

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {ownerToken}");
            _.Delete.Url($"/calendars/{calendarId}/members/{memberId}");
            _.StatusCodeShouldBe(204);
        });

        var calendar = await CalendarTestHelpers.GetCalendarAsync(fixture, ownerToken, calendarId);
        Assert.DoesNotContain(calendar.Members, m => m.UserId == memberId);
    }

    [Fact]
    public async Task The_owner_cannot_remove_themselves()
    {
        var (_, ownerToken, ownerId) = await fixture.CreateAuthenticatedUserAsync();
        var calendarId = await CalendarTestHelpers.CreateCalendarAsync(fixture, ownerToken, "Shared");

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {ownerToken}");
            _.Delete.Url($"/calendars/{calendarId}/members/{ownerId}");
            _.StatusCodeShouldBe(403);
        });
    }
}
