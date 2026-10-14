using Alba;

using buddy.Common;
using buddy.Features.Guardians;
using buddy.Features.Users;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace buddy.IntegrationTests.Features.Guardians.UpdateChildHomeworkChunks;

[Collection(BuddyApiCollection.Name)]
public sealed class UpdateChildHomeworkChunksTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("UpdateChildHomeworkChunks")]
    public async Task Sets_the_childs_focus_and_break_lengths()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");

        var response = await PatchAsync(guardianToken, child.Id, new { FocusMinutes = 10, BreakMinutes = 2 }, expectedStatus: 200);

        Assert.Equal(new HomeworkChunksDto(10, 2), response.ReadAsJson<ChildSummaryDto>().HomeworkChunks);
        Assert.Equal(new HomeworkChunksDto(10, 2), Assert.Single(await ListChildrenAsync(guardianToken)).HomeworkChunks);

        var events = await ReadChildEventsAsync(child.Id);
        var updated = Assert.Single(events.Select(e => e.Value).OfType<HomeworkChunksUpdated>());
        Assert.Equal(HomeworkChunks.Default, updated.Before);
        Assert.Equal(new HomeworkChunks(10, 2), updated.After);
    }

    [Fact]
    public async Task A_new_child_gets_fifteen_minutes_of_focus_and_five_of_break()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");

        Assert.Equal(new HomeworkChunksDto(15, 5), Assert.Single(await ListChildrenAsync(guardianToken)).HomeworkChunks);
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

        var response = await PatchAsync(secondToken, child.Id, new { FocusMinutes = 20, BreakMinutes = 4 }, expectedStatus: 200);

        var summary = response.ReadAsJson<ChildSummaryDto>();
        Assert.Equal(GuardianKind.Guardian, summary.Kind);
        Assert.Equal(new HomeworkChunksDto(20, 4), summary.HomeworkChunks);
        // The lengths belong to the child, so the first guardian sees the co-guardian's change.
        Assert.Equal(new HomeworkChunksDto(20, 4), Assert.Single(await ListChildrenAsync(firstToken)).HomeworkChunks);
    }

    [Fact]
    public async Task Setting_the_current_value_appends_no_event()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var before = (await ReadChildEventsAsync(child.Id)).Count;

        var response = await PatchAsync(guardianToken, child.Id, new { FocusMinutes = 15, BreakMinutes = 5 }, expectedStatus: 200);

        Assert.Equal(new HomeworkChunksDto(15, 5), response.ReadAsJson<ChildSummaryDto>().HomeworkChunks);
        Assert.Equal(before, (await ReadChildEventsAsync(child.Id)).Count);
    }

    [Theory]
    [InlineData(5, 1)]
    [InlineData(45, 15)]
    public async Task The_range_edges_are_accepted(int focusMinutes, int breakMinutes)
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");

        var response = await PatchAsync(guardianToken, child.Id, new { FocusMinutes = focusMinutes, BreakMinutes = breakMinutes }, expectedStatus: 200);

        Assert.Equal(new HomeworkChunksDto(focusMinutes, breakMinutes), response.ReadAsJson<ChildSummaryDto>().HomeworkChunks);
    }

    [Theory]
    [InlineData(4, 5, "focusMinutes")]
    [InlineData(46, 5, "focusMinutes")]
    [InlineData(15, 0, "breakMinutes")]
    [InlineData(15, 16, "breakMinutes")]
    public async Task A_length_outside_its_range_is_a_bad_request(int focusMinutes, int breakMinutes, string field)
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");

        var response = await PatchAsync(guardianToken, child.Id, new { FocusMinutes = focusMinutes, BreakMinutes = breakMinutes }, expectedStatus: 400);

        var envelope = response.ReadAsJson<ErrorEnvelope>();
        Assert.Equal("validation_error", envelope.Code);
        Assert.Contains(field, await response.ReadAsTextAsync(), StringComparison.Ordinal);
        Assert.Equal(new HomeworkChunksDto(15, 5), Assert.Single(await ListChildrenAsync(guardianToken)).HomeworkChunks);
    }

    [Fact]
    public async Task A_missing_value_is_a_bad_request()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");

        // A missing length binds to 0, which the validator rejects as out of range.
        var response = await PatchAsync(guardianToken, child.Id, new { BreakMinutes = 2 }, expectedStatus: 400);

        Assert.Equal("validation_error", response.ReadAsJson<ErrorEnvelope>().Code);
        Assert.Equal(new HomeworkChunksDto(15, 5), Assert.Single(await ListChildrenAsync(guardianToken)).HomeworkChunks);
    }

    [Fact]
    public async Task A_non_integer_value_is_a_bad_request()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");

        var response = await PatchAsync(guardianToken, child.Id, new { FocusMinutes = "ten", BreakMinutes = 2 }, expectedStatus: 400);

        Assert.Equal("validation_error", response.ReadAsJson<ErrorEnvelope>().Code);
    }

    [Fact]
    public async Task Returns_not_found_for_a_child_the_caller_does_not_guard()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var (_, otherGuardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var otherChild = await GuardianTestHelpers.CreateChildAsync(fixture, otherGuardianToken, "Sam");

        await PatchAsync(guardianToken, otherChild.Id, new { FocusMinutes = 10, BreakMinutes = 2 }, expectedStatus: 404);

        Assert.Equal(new HomeworkChunksDto(15, 5), Assert.Single(await ListChildrenAsync(otherGuardianToken)).HomeworkChunks);
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

        await PatchAsync(guardianToken, child.Id, new { FocusMinutes = 10, BreakMinutes = 2 }, expectedStatus: 404);
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

        await PatchAsync(guardianToken, child.Id, new { FocusMinutes = 10, BreakMinutes = 2 }, expectedStatus: 404);
    }

    [Fact]
    public async Task Requires_authentication()
    {
        await fixture.Host.Scenario(_ =>
        {
            _.Patch.Json(new { FocusMinutes = 10, BreakMinutes = 2 }).ToUrl($"/users/me/children/{Guid.NewGuid()}/homework-chunks");
            _.StatusCodeShouldBe(401);
        });
    }

    private Task<IScenarioResult> PatchAsync(string token, Guid childId, object body, int expectedStatus) =>
        fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Patch.Json(body).ToUrl($"/users/me/children/{childId}/homework-chunks");
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
