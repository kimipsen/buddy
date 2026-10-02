using Alba;

using buddy.Features.Groups;
using buddy.Features.PrintTemplates;
using buddy.IntegrationTests.Features.Groups;
using buddy.IntegrationTests.Features.PrintTemplates;
using buddy.IntegrationTests.Features.WorkLocations;
using buddy.IntegrationTests.Fixtures;

using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace buddy.IntegrationTests.SnapshotTests;

// Verifies the inline PrintTemplateSnapshotProjection stays consistent with a full replay, for both
// owner kinds (the snapshot is the only place PrintTemplateOwner is serialized) and every event.
[Collection(BuddyApiCollection.Name)]
public sealed class PrintTemplateSnapshotTests(BuddyApiFixture fixture)
{
    [Fact]
    public async Task The_snapshot_matches_a_full_replay_for_a_personal_template()
    {
        var family = await WorkLocationTestHelpers.CreateCoGuardiansAsync(fixture);
        var stil = await WorkLocationTestHelpers.AddLocationAsync(fixture, family.FirstToken, "Stil");
        var template = await PrintTemplateTestHelpers.CreateAsync(fixture, family.FirstToken);

        await Send(family.FirstToken, _ => _.Patch.Json(new { Name = "Skoleuge" }).ToUrl($"/print-templates/{template.Id}/name"));
        await Send(family.FirstToken, _ => _.Patch.Json(new { PaperSize = 1, DefaultStartWeekday = 0, ShowWeekNumber = false }).ToUrl($"/print-templates/{template.Id}/layout"));
        await PrintTemplateTestHelpers.ReplaceRowsAsync(fixture, family.FirstToken, template.Id,
        [
            new { Kind = PrintTemplateTestHelpers.WorkLocation, Label = "Far på Stil", HeightWeight = 1, GuardianId = family.FirstId, WorkLocationId = stil.Id },
            new { Kind = PrintTemplateTestHelpers.Pickup, Label = "Hente", HeightWeight = 2, ChildId = family.Child.Id },
            PrintTemplateTestHelpers.BlankRow(),
        ]);
        await Send(family.FirstToken, _ => _.Put.Json(new { Colors = new[] { new { GuardianId = family.SecondId, Color = "#f43f5e" } } }).ToUrl($"/print-templates/{template.Id}/colors"));

        await AssertSnapshotMatchesReplay(template.Id, expectDeleted: false);
    }

    [Fact]
    public async Task The_snapshot_matches_a_full_replay_for_a_deleted_group_template()
    {
        var (_, ownerToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var groupId = await GroupTestHelpers.CreateGroupAsync(fixture, ownerToken, "Familien");
        var template = await PrintTemplateTestHelpers.CreateAsync(fixture, ownerToken, "Fælles", groupId);

        await Send(ownerToken, _ => _.Delete.Url($"/print-templates/{template.Id}"), status: 204);

        var snapshot = await AssertSnapshotMatchesReplay(template.Id, expectDeleted: true);
        Assert.Equal(new PrintTemplateOwner.Group(new GroupId(groupId)), snapshot.Owner);
    }

    private async Task Send(string token, Action<Scenario> request, int status = 200) =>
        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            request(_);
            _.StatusCodeShouldBe(status);
        });

    private async Task<PrintTemplate> AssertSnapshotMatchesReplay(Guid templateId, bool expectDeleted)
    {
        var store = fixture.Host.Services.GetRequiredService<IPrintTemplateEventStore>();
        var id = new PrintTemplateId(templateId);

        var replayed = PrintTemplate.Rehydrate(await store.ReadAsync(id, CancellationToken.None));
        var snapshot = await store.FindSnapshotAsync(id, CancellationToken.None);

        Assert.NotNull(replayed);
        Assert.NotNull(snapshot);
        Assert.Equal(expectDeleted, snapshot.IsDeleted);
        Assert.Equivalent(replayed, snapshot, strict: true);

        return snapshot;
    }
}
