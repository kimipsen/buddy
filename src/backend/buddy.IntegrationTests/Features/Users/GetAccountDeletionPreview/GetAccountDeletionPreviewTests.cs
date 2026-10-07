using buddy.Features.Groups;
using buddy.Features.Privacy;
using buddy.IntegrationTests.Features.Groups;
using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Features.WorkLocations;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

namespace buddy.IntegrationTests.Features.Users.GetAccountDeletionPreview;

[Collection(BuddyApiCollection.Name)]
public sealed class GetAccountDeletionPreviewTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("GetAccountDeletionPreview")]
    public async Task The_preview_lists_the_children_erased_and_the_groups_handed_over_or_deleted()
    {
        var family = await WorkLocationTestHelpers.CreateCoGuardiansAsync(fixture);
        var orphan = await GuardianTestHelpers.CreateChildAsync(fixture, family.FirstToken, "Only", "Theirs");

        var (admin, adminToken, adminId) = await fixture.CreateAuthenticatedUserAsync();
        var sharedGroup = await GroupTestHelpers.CreateGroupAsync(fixture, family.FirstToken, "Shared group");
        await GroupTestHelpers.AddMemberAsync(fixture, family.FirstToken, sharedGroup, adminToken, admin.Email, GroupRole.Admin);
        var soloGroup = await GroupTestHelpers.CreateGroupAsync(fixture, family.FirstToken, "Solo group");

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {family.FirstToken}");
            _.Get.Url("/users/me/deletion-preview");
            _.StatusCodeShouldBe(200);
        });
        var preview = response.ReadAsJson<AccountDeletionPreview>();

        // The co-guarded child stays with its other guardian.
        var erased = Assert.Single(preview.ChildrenErased);
        Assert.Equal((orphan.Id, "Only", "Theirs"), (erased.Id, erased.GivenName, erased.FamilyName));

        var handover = Assert.Single(preview.GroupsHandedOver);
        Assert.Equal((sharedGroup, adminId), (handover.Id, handover.NewOwner.Id));
        Assert.Equal(soloGroup, Assert.Single(preview.GroupsDeleted).Id);

        // Only a preview: the child is still there.
        Assert.True(await fixture.KeycloakUserExistsAsync(orphan.Username));
    }
}
