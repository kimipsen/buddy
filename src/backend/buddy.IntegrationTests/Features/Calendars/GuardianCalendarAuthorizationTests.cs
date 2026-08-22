using Alba;

using buddy.Features.Calendars;
using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Fixtures;

using Xunit;

namespace buddy.IntegrationTests.Features.Calendars;

// Covers the guardian resolution step added to CalendarAuthorization.ResolveRole (see
// docs/backend/analysis/child-accounts-and-guardian-roles.md): a guardian with no explicit grant
// resolves to CalendarRole.Owner on a linked child's own calendar, an explicit grant still wins
// over that default, and revoking the link removes access on the very next check. Deliberately
// separate from CreateChildTests/ListCalendarsTests, same reasoning as CalendarAuthorizationTests
// for the group case -- this exercises the cross-feature resolution logic directly.
[Collection(BuddyApiCollection.Name)]
public sealed class GuardianCalendarAuthorizationTests(BuddyApiFixture fixture)
{
    [Fact]
    public async Task A_guardian_with_no_explicit_grant_gets_owner_access_to_the_childs_calendar()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var childToken = await GuardianTestHelpers.CompleteChildLoginAsync(fixture, child);

        var calendarId = await CalendarTestHelpers.CreateCalendarAsync(fixture, childToken, "Alex's Calendar");

        // No explicit Calendar.Members grant to the guardian at all -- the guardian link alone
        // resolves CalendarRole.Owner, which is enough to create items.
        var item = await CalendarTestHelpers.CreateEventAsync(fixture, guardianToken, calendarId);
        Assert.NotNull(item);
    }

    [Fact]
    public async Task A_third_party_with_no_guardian_link_has_no_access()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var childToken = await GuardianTestHelpers.CompleteChildLoginAsync(fixture, child);

        var calendarId = await CalendarTestHelpers.CreateCalendarAsync(fixture, childToken, "Alex's Calendar");

        var (_, strangerToken, _) = await fixture.CreateAuthenticatedUserAsync();

        await CalendarTestHelpers.CreateEventAsync(fixture, strangerToken, calendarId, expectedStatus: 404);
    }

    [Fact]
    public async Task An_explicit_grant_wins_over_the_guardian_default()
    {
        var (_, guardianToken, guardianId) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var childToken = await GuardianTestHelpers.CompleteChildLoginAsync(fixture, child);

        var calendarId = await CalendarTestHelpers.CreateCalendarAsync(fixture, childToken, "Alex's Calendar");

        // The child explicitly downgrades their guardian to Viewer on this one calendar -- that
        // explicit grant wins over the guardian-derived Owner default, same precedence rule as the
        // group case.
        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {childToken}");
            _.Put.Json(new { Role = CalendarRole.Viewer }).ToUrl($"/calendars/{calendarId}/members/{guardianId}");
            _.StatusCodeShouldBe(204);
        });

        await CalendarTestHelpers.CreateEventAsync(fixture, guardianToken, calendarId, expectedStatus: 403);
    }

    [Fact]
    public async Task Revoking_the_guardian_link_removes_access_on_the_next_check()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var childToken = await GuardianTestHelpers.CompleteChildLoginAsync(fixture, child);

        var calendarId = await CalendarTestHelpers.CreateCalendarAsync(fixture, childToken, "Alex's Calendar");

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Delete.Url($"/users/me/children/{child.Id}/guardian-link");
            _.StatusCodeShouldBe(204);
        });

        // No GuardianLink and no explicit grant -- collapses to NotFound, same as any other
        // non-member hitting a calendar they can't distinguish from a missing one.
        await CalendarTestHelpers.CreateEventAsync(fixture, guardianToken, calendarId, expectedStatus: 404);
    }

    [Fact]
    public async Task Lists_the_childs_calendar_for_the_guardian()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var childToken = await GuardianTestHelpers.CompleteChildLoginAsync(fixture, child);

        var calendarId = await CalendarTestHelpers.CreateCalendarAsync(fixture, childToken, "Alex's Calendar");

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Get.Url("/calendars/");
            _.StatusCodeShouldBeOk();
        });

        var body = response.ReadAsJson<List<CalendarSummaryDto>>();
        var summary = Assert.Single(body, c => c.Id == calendarId);
        Assert.Equal(CalendarRole.Owner, summary.Role);
    }
}
