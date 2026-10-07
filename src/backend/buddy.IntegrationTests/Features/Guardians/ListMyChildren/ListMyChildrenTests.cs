using Alba;

using buddy.Features.Guardians;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

namespace buddy.IntegrationTests.Features.Guardians.ListMyChildren;

[Collection(BuddyApiCollection.Name)]
public sealed class ListMyChildrenTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("ListMyChildren")]
    public async Task Lists_children_linked_to_the_calling_guardian()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Get.Url("/users/me/children/");
            _.StatusCodeShouldBeOk();
        });

        var children = response.ReadAsJson<ChildSummaryDto[]>();
        var listed = Assert.Single(children);
        Assert.Equal(child.Id, listed.Id);
        Assert.Equal(child.GuardianLinkId, listed.GuardianLinkId);
    }

    [Fact]
    public async Task Lists_multiple_children_in_a_stable_chronological_order()
    {
        // Regression test: ListForGuardianAsync used to query without an ORDER BY, so Postgres gave
        // no row-order guarantee at all -- repeating this same query with no data changed in
        // between could return a different order. Frontend code (GuardianMealplan.load) picks the
        // first child in this list as a single "family" scope, so an unstable order meant that pick
        // could silently point at a different child between two page loads.
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();

        var first = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var second = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Blair");
        var third = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Casey");

        async Task<Guid[]> ListChildIdsAsync()
        {
            var response = await fixture.Host.Scenario(_ =>
            {
                _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
                _.Get.Url("/users/me/children/");
                _.StatusCodeShouldBeOk();
            });

            return [.. response.ReadAsJson<ChildSummaryDto[]>().Select(c => c.Id)];
        }

        var expected = new[] { first.Id, second.Id, third.Id };
        var firstCall = await ListChildIdsAsync();
        var secondCall = await ListChildIdsAsync();

        Assert.Equal(expected, firstCall);
        Assert.Equal(expected, secondCall);
    }

    [Fact]
    public async Task Children_are_ordered_by_when_the_guardian_was_linked_not_by_name_or_child_id()
    {
        // The order key is the link's creation time (GuardianLinkDocument.CreatedAt), with ChildId
        // only as a tie-breaker. Here the child linked last has both the alphabetically first name
        // and the older (smaller UUIDv7) id, so ordering by name or by id would put it first.
        var (_, otherGuardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var (guardian, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();

        var olderChild = await GuardianTestHelpers.CreateChildAsync(fixture, otherGuardianToken, "Aaron");
        var ownChild = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Zoe");

        await GuardianTestHelpers.InviteGuardianAsync(fixture, otherGuardianToken, olderChild.Id, guardian.Email, GuardianKind.Parent);
        var inviteToken = await GuardianTestHelpers.ReadGuardianInviteTokenAsync(fixture, guardian.Email);
        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Post.Url($"/guardian-invites/{inviteToken}/accept");
            _.StatusCodeShouldBe(204);
        });

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Get.Url("/users/me/children/");
            _.StatusCodeShouldBeOk();
        });

        Assert.True(olderChild.Id.CompareTo(ownChild.Id) < 0, "Precondition: the later-linked child has the smaller id.");
        Assert.Equal(new[] { ownChild.Id, olderChild.Id }, response.ReadAsJson<ChildSummaryDto[]>().Select(c => c.Id));
    }

    [Fact]
    public async Task Requires_authentication()
    {
        await fixture.Host.Scenario(_ =>
        {
            _.Get.Url("/users/me/children/");
            _.StatusCodeShouldBe(401);
        });
    }
}
