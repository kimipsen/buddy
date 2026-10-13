using Alba;

using buddy.Common;
using buddy.Features.Guardians;
using buddy.Features.Users;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace buddy.IntegrationTests.Features.Guardians.UpdateChildVisualTimers;

[Collection(BuddyApiCollection.Name)]
public sealed class UpdateChildVisualTimersTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("UpdateChildVisualTimers")]
    public async Task Turns_the_childs_timers_off_and_back_on()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");

        var off = await PatchAsync(guardianToken, child.Id, new { ShowVisualTimers = false }, expectedStatus: 200);
        Assert.False(off.ReadAsJson<ChildSummaryDto>().ShowVisualTimers);
        Assert.False(Assert.Single(await ListChildrenAsync(guardianToken)).ShowVisualTimers);

        var on = await PatchAsync(guardianToken, child.Id, new { ShowVisualTimers = true }, expectedStatus: 200);
        Assert.True(on.ReadAsJson<ChildSummaryDto>().ShowVisualTimers);
        Assert.True(Assert.Single(await ListChildrenAsync(guardianToken)).ShowVisualTimers);

        var events = await ReadChildEventsAsync(child.Id);
        Assert.Equal(
            new[] { (true, false), (false, true) },
            events.Select(e => e.Value).OfType<ShowVisualTimersUpdated>().Select(e => (e.Before, e.After)));
    }

    [Fact]
    public async Task A_new_child_shows_timers_by_default()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");

        Assert.True(Assert.Single(await ListChildrenAsync(guardianToken)).ShowVisualTimers);
    }

    [Fact]
    public async Task A_co_guardian_of_kind_guardian_can_change_it_too()
    {
        var (_, firstToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, firstToken, "Alex");
        var (second, secondToken, _) = await fixture.CreateAuthenticatedUserAsync();
        await GuardianTestHelpers.InviteGuardianAsync(fixture, firstToken, child.Id, second.Email, GuardianKind.Guardian);
        var inviteToken = await GuardianTestHelpers.ReadGuardianInviteTokenAsync(fixture, second.Email);
        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {secondToken}");
            _.Post.Url($"/guardian-invites/{inviteToken}/accept");
            _.StatusCodeShouldBe(204);
        });

        var response = await PatchAsync(secondToken, child.Id, new { ShowVisualTimers = false }, expectedStatus: 200);

        var summary = response.ReadAsJson<ChildSummaryDto>();
        Assert.Equal(GuardianKind.Guardian, summary.Kind);
        Assert.False(summary.ShowVisualTimers);
        // Last write wins for every guardian: the first guardian sees the co-guardian's change.
        Assert.False(Assert.Single(await ListChildrenAsync(firstToken)).ShowVisualTimers);
    }

    [Fact]
    public async Task Setting_the_current_value_appends_no_event()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var before = (await ReadChildEventsAsync(child.Id)).Count;

        var response = await PatchAsync(guardianToken, child.Id, new { ShowVisualTimers = true }, expectedStatus: 200);

        Assert.True(response.ReadAsJson<ChildSummaryDto>().ShowVisualTimers);
        Assert.Equal(before, (await ReadChildEventsAsync(child.Id)).Count);
    }

    [Fact]
    public async Task A_missing_value_is_a_bad_request()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");

        var response = await PatchAsync(guardianToken, child.Id, new { }, expectedStatus: 400);

        Assert.Equal("validation_error", response.ReadAsJson<ErrorEnvelope>().Code);
    }

    [Fact]
    public async Task A_non_boolean_value_is_a_bad_request()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");

        var response = await PatchAsync(guardianToken, child.Id, new { ShowVisualTimers = "nope" }, expectedStatus: 400);

        Assert.Equal("validation_error", response.ReadAsJson<ErrorEnvelope>().Code);
        Assert.True(Assert.Single(await ListChildrenAsync(guardianToken)).ShowVisualTimers);
    }

    [Fact]
    public async Task Returns_not_found_for_a_child_the_caller_does_not_guard()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var (_, otherGuardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var otherChild = await GuardianTestHelpers.CreateChildAsync(fixture, otherGuardianToken, "Sam");

        await PatchAsync(guardianToken, otherChild.Id, new { ShowVisualTimers = false }, expectedStatus: 404);

        Assert.True(Assert.Single(await ListChildrenAsync(otherGuardianToken)).ShowVisualTimers);
    }

    [Fact]
    public async Task Returns_not_found_after_the_guardian_link_is_revoked()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Delete.Url($"/users/me/children/{child.Id}/guardian-link");
            _.StatusCodeShouldBe(204);
        });

        await PatchAsync(guardianToken, child.Id, new { ShowVisualTimers = false }, expectedStatus: 404);
    }

    [Fact]
    public async Task Returns_not_found_for_a_deleted_child()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Delete.Url($"/users/me/children/{child.Id}");
            _.StatusCodeShouldBe(204);
        });

        await PatchAsync(guardianToken, child.Id, new { ShowVisualTimers = false }, expectedStatus: 404);
    }

    [Fact]
    public async Task Requires_authentication()
    {
        await fixture.Host.Scenario(_ =>
        {
            _.Patch.Json(new { ShowVisualTimers = false }).ToUrl($"/users/me/children/{Guid.NewGuid()}/visual-timers");
            _.StatusCodeShouldBe(401);
        });
    }

    private Task<IScenarioResult> PatchAsync(string token, Guid childId, object body, int expectedStatus) =>
        fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Patch.Json(body).ToUrl($"/users/me/children/{childId}/visual-timers");
            _.StatusCodeShouldBe(expectedStatus);
        });

    private async Task<ChildSummaryDto[]> ListChildrenAsync(string token)
    {
        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Get.Url("/users/me/children/");
            _.StatusCodeShouldBeOk();
        });

        return response.ReadAsJson<ChildSummaryDto[]>();
    }

    private async Task<IReadOnlyCollection<UserEvent>> ReadChildEventsAsync(Guid childId) =>
        await fixture.Host.Services.GetRequiredService<IUserEventStore>().ReadAsync(new UserId(childId), CancellationToken.None);
}
